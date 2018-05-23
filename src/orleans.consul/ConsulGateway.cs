using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Consul;

namespace src
{
    public class ConsulFactory
    {
        private volatile ConsulClient _consul;
        private static object _lock = new object();

        public ConsulClient Create()
        {
            if (_consul != null) return _consul;
            lock (_lock)
            {
                if (_consul != null) return _consul;
                var consul = new ConsulClient();
                _consul = consul;
                return consul;
            }
        }
    }

    public struct ConsulSiloAddress
    {
        public string Address { get; set; }
        public string Generation { get; set; }
        public int Port { get; set; }
    }

    public class ConsulGateway : IDisposable
    {
        private const string GenerationTag = "generation";
        private readonly ConsulFactory _factory;
        private List<IListener> _listeners = new List<IListener>();
        private ulong _index;
        private bool _isListening;

        public ConsulGateway(ConsulFactory factory)
        {
            _factory = factory;
        }

        public void Dispose()
        {
            
        }

        public Task SetProperty(string ping, string value)
        {
            var consul = _factory.Create();
            return consul.KV.Put(new KVPair(ping)
            {
                Value = Encoding.UTF8.GetBytes(value)
            });
        }

        public async Task<string> GetProperty(string ping)
        {
            var consul = _factory.Create();
            var property = await consul.KV.Get("ping");
            return Encoding.UTF8.GetString(property.Response.Value);
        }

        public async Task EnsureRegistered(string serviceName, string generationTag, int port)
        {
            var consul = _factory.Create();
            await consul.Agent.ServiceRegister(new AgentServiceRegistration
            {
                ID = $"{serviceName}-{port}",
                Address = serviceName,
                Port = port,
                Name = serviceName,
                Tags = new string[] {$"{GenerationTag}:{generationTag}"},
            });
        }

        public async Task<List<ConsulSiloAddress>> LookupRegistered(string serviceName)
        {
            var consul = _factory.Create();
            var records = await consul.Catalog.Service(serviceName);
            var recordsResponse = records.Response;

            var addresses = ToConsulSiloAddresses(recordsResponse);

            return addresses;
        }

        private static List<ConsulSiloAddress> ToConsulSiloAddresses(CatalogService[] recordsResponse)
        {
            var addresses = new List<ConsulSiloAddress>();
            foreach (var service in recordsResponse)
            {
                var tagPart = ExtractGeneration(service);
                var address = new ConsulSiloAddress
                {
                    Address = service.Address,
                    Port = service.ServicePort,
                    Generation = tagPart
                };
                addresses.Add(address);
            }

            return addresses;
        }

        private static string ExtractGeneration(CatalogService service)
        {
            string tagPart = null;
            foreach (var serviceTag in service.ServiceTags)
            {
                var tagParts = serviceTag.Split(':');
                if (tagParts[0] == GenerationTag)
                {
                    tagPart = tagParts[1];
                }
            }

            return tagPart;
        }

        public async Task Cleanup()
        {
            var consul = _factory.Create();
            var services = await consul.Agent.Services();
            foreach (var service in services.Response)
            {
                await consul.Agent.ServiceDeregister(service.Key);
            }
        }

        public void Subscribe(IListener listener)
        {
            _listeners.Add(listener);
            if (_isListening) return;
            Task.Run(Listener);
        }

        private async Task Listener()
        {
            _index = 0;
            try
            {
                var consul = _factory.Create();
                while (true)
                {
                    var records = await consul.Catalog.Service("service1", "", new QueryOptions {WaitIndex = _index});
                    _index = records.LastIndex;
                    var siloAddresses = ToConsulSiloAddresses(records.Response);
       
                    UpdateListeners(siloAddresses);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await Task.Delay(50);
            }
        }

        private void UpdateListeners(List<ConsulSiloAddress> tags)
        {
            foreach (var listener in _listeners)
            {
                try
                {
                    listener.Update(tags);

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }

    public interface IListener
    {
        void Update(List<ConsulSiloAddress> tags);
    }
}

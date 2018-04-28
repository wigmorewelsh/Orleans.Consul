using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Consul;

namespace src
{
    public class ConsulFactory
    {
        private static volatile ConsulClient _consul;
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

    public class ConsulGateway : IDisposable
    {
        private const string GenerationTag = "generation";
        private readonly ConsulFactory _factory;

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

        public async Task EnsureRegistered(string serviceName, string generationTag)
        {
            var consul = _factory.Create();
            await consul.Agent.ServiceRegister(new AgentServiceRegistration
            {
                Address = serviceName,
                Name = serviceName,
                Tags = new string[] {$"{GenerationTag}:{generationTag}"}
            });
        }

        public async Task<string> LookupRegistered(string serviceName)
        {
            var consul = _factory.Create();
            var records = await consul.Catalog.Service(serviceName);

            var tag = "";
            foreach (var service in records.Response)
            {
                foreach (var serviceTag in service.ServiceTags)
                {
                    var tagParts = serviceTag.Split(':');
                    if (tagParts[0] == GenerationTag)
                    {
                        tag = tagParts[1];
                    }
                }
            }

            return tag;
        }
    }
}

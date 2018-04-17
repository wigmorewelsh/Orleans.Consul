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
    }
}

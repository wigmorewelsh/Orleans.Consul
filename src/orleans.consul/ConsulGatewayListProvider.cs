using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Messaging;
using Orleans.Runtime;

namespace src
{
    public class ConsulGatewayOptions
    {
        public string ClusterAddress { get; set; }

        public string ConsulAddress { get; set; }
    }

    public class ConsulGatewayListProvider : IGatewayListProvider, IGatewayListObservable, IDisposable
    {
        private ConsulClient _client;
        private HashSet<IGatewayListListener> _listeners;
        private IOptions<ConsulGatewayOptions> _options;
        private Task _task;
        private string _serviceName;
        private ulong _waitIndex;
        private ILogger<ConsulGatewayListProvider> _logger;
        private List<SiloAddress> _urls;

        public ConsulGatewayListProvider(ILogger<ConsulGatewayListProvider> logger, IOptions<ConsulGatewayOptions> options)
        {
            _logger = logger;
            _listeners = new HashSet<IGatewayListListener>();
            _options = options;
            _client = new ConsulClient(conf => conf.Address = new Uri(_options.Value.ConsulAddress));
            _serviceName = options.Value.ClusterAddress;
            _waitIndex = 0;
        }

        public bool SubscribeToGatewayNotificationEvents(IGatewayListListener listener)
        {
            var contains = _listeners.Contains(listener);
            if (contains)
                _listeners.Add(listener);
            return !contains;
        }

        public bool UnSubscribeFromGatewayNotificationEvents(IGatewayListListener listener)
        {
            var contains = _listeners.Contains(listener);
            if (contains)
                _listeners.Remove(listener);
            return contains;
        }

        public Task InitializeGatewayListProvider()
        {
            _task = Task.Run(UpdateLoop);
            return Task.FromResult(0);
        }

        private async Task UpdateLoop()
        {
            while (true)
            {
                var urls = await FetchGateways();

                _urls = urls;

                foreach (var listener in _listeners)
                    listener.GatewayListNotification(urls.Select(a => a.ToGatewayUri()));
            }
        }

        private async Task<List<SiloAddress>> FetchGateways()
        {
            var options = new QueryOptions { WaitIndex = _waitIndex };
            var service = await FetchServices(options);
            _waitIndex = service.LastIndex;
            var urls = new List<SiloAddress>();
            foreach (var inst in service.Response)
            {
                var ip = IPAddress.Parse(inst.ServiceAddress);
                var ep = new IPEndPoint(ip, inst.ServicePort);
                urls.Add(SiloAddress.New(ep, 0));
            }

            return urls;
        }

        private async Task<QueryResult<CatalogService[]>> FetchServices(QueryOptions options)
        {
            while (true)
            {
                try
                {
                    return await _client.Catalog.Service(_serviceName, "", options);
                }
                catch (Exception err)
                {
                    _logger.Error(99, "Waiting for new consul services timed out", err);
                }
            }
        }

        public async Task<IList<Uri>> GetGateways()
        {
            if (_urls == null)
            {
                var fetchGateways = await FetchGateways();
                return fetchGateways.Select(a => a.ToGatewayUri()).ToList();
            }

            return _urls.Select(a => a.ToGatewayUri()).ToList();
        }

        public TimeSpan MaxStaleness => TimeSpan.Zero;
        public bool IsUpdatable => true;

        public void Dispose()
        {
            _client?.Dispose();
            _task?.Dispose();
        }
    }
}

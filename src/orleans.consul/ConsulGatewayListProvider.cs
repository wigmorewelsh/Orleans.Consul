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

    public class ConsulGatewayListProvider : IGatewayListProvider, IGatewayListObservable, IDisposable, IListener
    {
        private HashSet<IGatewayListListener> _listeners;
        private IOptions<ConsulGatewayOptions> _options;
        private readonly ConsulGateway _consul;
        private string _serviceName;
        private ILogger<ConsulGatewayListProvider> _logger;
        private List<SiloAddress> _urls;

        public ConsulGatewayListProvider(
            ILogger<ConsulGatewayListProvider> logger,
            IOptions<ConsulGatewayOptions> options,
            ConsulGateway consul)
        {
            _logger = logger;
            _listeners = new HashSet<IGatewayListListener>();
            _options = options;
            _consul = consul;
            _serviceName = options.Value.ClusterAddress;
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
            _consul.Subscribe(this);
            return Task.FromResult(0);
        }

        public async Task<IList<Uri>> GetGateways()
        {
            var fetchGateways = await _consul.LookupRegistered(_serviceName);
            var addresses = new List<SiloAddress>();
            foreach (var gateway in fetchGateways)
            {
                var address = ToSlioAddress(gateway);
                addresses.Add(address);
            }
            return addresses.Select(a => a.ToGatewayUri()).ToList();
        }

        private static SiloAddress ToSlioAddress(ConsulSiloAddress gateway)
        {
            var ipAddress = IPAddress.Parse(gateway.Address);
            var ipEndPoint = new IPEndPoint(ipAddress, gateway.Port);
            var address = SiloAddress.New(ipEndPoint,
                int.Parse(gateway.Generation));
            return address;
        }

        public TimeSpan MaxStaleness => TimeSpan.Zero;
        public bool IsUpdatable => true;

        public void Dispose()
        {
            _consul?.Dispose();
        }

        public void Update(List<ConsulSiloAddress> tags)
        {
            var gateways = tags.Select(a => ToSlioAddress(a).ToGatewayUri()).ToList();
            foreach (var listener in _listeners)
                listener.GatewayListNotification(gateways);
        }
    }
}

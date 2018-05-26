using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans.Runtime;

namespace src
{
    public class ConsulMembershipOptions
    {
        public string ClusterAddress { get; internal set; }
        public string ConsulAddress { get; internal set; }
        public string LivenessAddress { get; internal set; }
    }

    public class ConsulMembershipOracle : IMembershipOracle, IDisposable, IListener
    {
        private readonly ILocalSiloDetails _siloDetails;
        private readonly IOptions<ConsulMembershipOptions> _options;
        private readonly ILogger<ConsulMembershipOracle> _logger;
        private readonly ConsulGateway _consul;
        private Task _task;
        private string _serviceName;
        private readonly HashSet<ISiloStatusListener> _subscribers = new HashSet<ISiloStatusListener>();
        private volatile Dictionary<SiloAddress, SiloStatus> _silos = new Dictionary<SiloAddress, SiloStatus>();
        private bool _running;
        private string _gatewayName;

        public ConsulMembershipOracle(
            ILocalSiloDetails siloDetails, 
            IOptions<ConsulMembershipOptions> options,
            ILogger<ConsulMembershipOracle> logger,
            ConsulGateway consul)
        {
            _siloDetails = siloDetails;
            _options = options;
            _logger = logger;
            _consul = consul;
            _serviceName = options.Value.LivenessAddress;
            _gatewayName = options.Value.ClusterAddress;
            _silos[this.SiloAddress] = SiloStatus.Created;
        }

        public async Task Start()
        {
            _logger.Info("ConsulOracle starting on {silo}", SiloAddress);
            
            _consul.Subscribe(this, _gatewayName);

            var tags = await _consul.LookupRegistered(_gatewayName);

            foreach (var address in tags)
            {
                _silos[ToSlioAddress(address)] = SiloStatus.Active;
            }

            UpdateStatus(SiloStatus.Joining);
        }

        public async Task BecomeActive()
        {
            _logger.Info("ConsulOracle making {silo} active", SiloAddress);
            UpdateStatus(SiloStatus.Active);

            await _consul.EnsureRegistered(_gatewayName,
                _siloDetails.SiloAddress.Generation.ToString(),
                _siloDetails.SiloAddress.Endpoint.Port);

            await _consul.EnsureRegistered(_serviceName,
                _siloDetails.SiloAddress.Generation.ToString(),
                _siloDetails.GatewayAddress.Endpoint.Port);
        }

        private void UpdateStatus(SiloStatus status)
        {
            var updated = false;
            lock (_lock)
            {
                if (!_silos.ContainsKey(this.SiloAddress)) return;
                var existing = _silos[this.SiloAddress];
                if (existing != status)
                {
                    updated = true;
                    _silos[this.SiloAddress] = status;
                }
            }

            if (updated)
            {
                foreach (var listener in _subscribers)
                    listener.SiloStatusChangeNotification(this.SiloAddress, status);
            }
        }

        public Task ShutDown()
        {
            UpdateStatus(SiloStatus.ShuttingDown);
            return Task.FromResult(0);
        }

        public Task Stop()
        {
            UpdateStatus(SiloStatus.Stopping);
            _running = false;
            return Task.FromResult(0);
        }

        public async Task KillMyself()
        {
            _logger.Info("ConsulOracle making {silo} dead", SiloAddress);
            UpdateStatus(SiloStatus.Dead);

            _consul.Unsubscribe(this, _gatewayName);

            await _consul.EnsureDeRegistered(_gatewayName,
                _siloDetails.SiloAddress.Generation.ToString(),
                _siloDetails.SiloAddress.Endpoint.Port);

            await _consul.EnsureDeRegistered(_serviceName,
                _siloDetails.SiloAddress.Generation.ToString(),
                _siloDetails.GatewayAddress.Endpoint.Port);
        }

        public SiloStatus GetApproximateSiloStatus(SiloAddress siloAddress)
        {
            if (!_silos.ContainsKey(siloAddress))
                return SiloStatus.None;
                
            return _silos[siloAddress];
        }

        public Dictionary<SiloAddress, SiloStatus> GetApproximateSiloStatuses(bool onlyActive = false)
        {
            if (onlyActive)
            {
                var active = new Dictionary<SiloAddress, SiloStatus>();
                foreach (var silo in _silos)
                    if (silo.Value == SiloStatus.Active)
                        active.Add(silo.Key, silo.Value);

                return active;
            }

            return _silos;
        }

        public IReadOnlyList<SiloAddress> GetApproximateMultiClusterGateways()
        {
            var silos = new List<SiloAddress>();
            foreach (var silo in _silos)
                silos.Add(silo.Key);

            return silos;
        }

        public bool TryGetSiloName(SiloAddress siloAddress, out string siloName)
        {
            var containsKey = _silos.ContainsKey(siloAddress);
            siloName = SiloName;//probably wrong
            return containsKey;
        }

        public bool IsFunctionalDirectory(SiloAddress siloAddress)
        {
            return _silos.ContainsKey(siloAddress);
        }

        public bool IsDeadSilo(SiloAddress address)
        {
            if (address.Equals(this.SiloAddress)) return false;
            var status = this.GetApproximateSiloStatus(address);
            return status == SiloStatus.Dead;
        }

        public bool SubscribeToSiloStatusEvents(ISiloStatusListener observer)
        {
            return _subscribers.Add(observer);
        }

        public bool UnSubscribeFromSiloStatusEvents(ISiloStatusListener observer)
        {
            return _subscribers.Remove(observer);
        }

        public SiloStatus CurrentStatus => this.GetApproximateSiloStatus(this.SiloAddress);
        public string SiloName => _siloDetails.Name;
        public SiloAddress SiloAddress => _siloDetails.SiloAddress;

        public bool CheckHealth(DateTime lastCheckTime)
        {
            return true;
        }

        public void Dispose()
        {
            _task?.Dispose();
            _consul?.Dispose();
        }

        private static SiloAddress ToSlioAddress(ConsulSiloAddress gateway)
        {
            var ipAddress = IPAddress.Parse(gateway.Address);
            var ipEndPoint = new IPEndPoint(ipAddress, gateway.Port);
            var address = SiloAddress.New(ipEndPoint,
                int.Parse(gateway.Generation));
            return address;
        }

        private static object _lock = new Object();

        public void Update(List<ConsulSiloAddress> tags)
        {
            var gateways = tags.Select(a => ToSlioAddress(a)).ToList();

            foreach (var silo in gateways)
            {
                if(_silos.ContainsKey(silo) && _silos[silo] == SiloStatus.Active)
                    continue;

                _logger.Info("ConsulOracle new silo found {othersilo} adding to list on {silo}", silo, SiloAddress);

                _silos[silo] = SiloStatus.Active;

                foreach (var listener in _subscribers)
                    listener.SiloStatusChangeNotification(silo, SiloStatus.Active);
            }

            foreach (var silo in _silos.ToList())
            {
                if (!gateways.Contains(silo.Key) && silo.Value == SiloStatus.Active)
                {
                    _logger.Info("ConsulOracle silo removed {othersilo} marking as dead on {silo}", silo, SiloAddress);
                    _silos[silo.Key] = SiloStatus.Dead;

                    foreach (var listener in _subscribers)
                        listener.SiloStatusChangeNotification(silo.Key, SiloStatus.Dead);
                }
            }
        }
    }
}

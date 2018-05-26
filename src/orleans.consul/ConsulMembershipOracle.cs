using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Consul;
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
            ConsulGateway consul)
        {
            _siloDetails = siloDetails;
            _options = options;
            _consul = consul;
            _serviceName = options.Value.LivenessAddress;
            _gatewayName = options.Value.ClusterAddress;
        }

        public async Task Start()
        {
            _consul.Subscribe(this, _gatewayName);

            UpdateStatus(SiloStatus.Joining);

            var tags = await _consul.LookupRegistered(_serviceName);

            _silos = tags.Select(a => ToSlioAddress(a)).ToDictionary(el => el, _ => SiloStatus.Active);
        }

        public async Task BecomeActive()
        {
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
            UpdateStatus(SiloStatus.Dead);

            _consul.Unsubscribe(this, _gatewayName);

            await _consul.EnsureDeRegistered(_gatewayName,
                _siloDetails.SiloAddress.Generation.ToString(),
                _siloDetails.SiloAddress.Endpoint.Port);

            await _consul.EnsureDeRegistered(_serviceName,
                _siloDetails.SiloAddress.Generation.ToString(),
                _siloDetails.GatewayAddress.Endpoint.Port);


            if (_silos.ContainsKey(_siloDetails.SiloAddress))
                _silos.Remove(_siloDetails.SiloAddress);
        }

        public SiloStatus GetApproximateSiloStatus(SiloAddress siloAddress)
        {
            if (!_silos.ContainsKey(siloAddress))
                return SiloStatus.None;
                
            return _silos[siloAddress];
        }

        public Dictionary<SiloAddress, SiloStatus> GetApproximateSiloStatuses(bool onlyActive = false)
        {
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

        public bool IsDeadSilo(SiloAddress silo)
        {
            if (_siloDetails.GatewayAddress == silo)
                return false;

            return _silos.ContainsKey(silo);
        }

        public bool SubscribeToSiloStatusEvents(ISiloStatusListener observer)
        {
            return _subscribers.Add(observer);
        }

        public bool UnSubscribeFromSiloStatusEvents(ISiloStatusListener observer)
        {
            return _subscribers.Remove(observer);
        }

        public SiloStatus CurrentStatus { get; }
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
                if(_silos.ContainsKey(silo))
                    continue;

                _silos.Add(silo, SiloStatus.Active);

                foreach (var listener in _subscribers)
                    listener.SiloStatusChangeNotification(silo, SiloStatus.Active);
            }
        }
    }
}

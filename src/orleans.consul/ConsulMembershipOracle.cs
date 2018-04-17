using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
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

    public class ConsulMembershipOracle : IMembershipOracle, IDisposable
    {
        private readonly ILocalSiloDetails _siloDetails;
        private readonly IOptions<ConsulMembershipOptions> _options;
        private Task _task;
        private ConsulClient _client;
        private ulong _waitIndex = 0;
        private string _serviceName;
        private readonly HashSet<ISiloStatusListener> _subscribers = new HashSet<ISiloStatusListener>();
        private Dictionary<SiloAddress, SiloStatus> _silos = new Dictionary<SiloAddress, SiloStatus>();
        private bool _running;

        private CancellationToken _cancelToken = new CancellationToken();


        public ConsulMembershipOracle(ILocalSiloDetails siloDetails, IOptions<ConsulMembershipOptions> options)
        {
            _siloDetails = siloDetails;
            _options = options;
            _serviceName = options.Value.LivenessAddress;
            _client = new ConsulClient(conf => conf.Address = new Uri(options.Value.ConsulAddress));
        }

        public Task Start()
        {
            _running = true;
            _task = Task.Run(UpdateLoop);
            return Task.FromResult(0);
        }

        private async Task UpdateLoop()
        {
            while (true)
            {
                if (!_running) return;
                var service = await _client.Catalog.Service(_serviceName, "", new QueryOptions { WaitIndex = _waitIndex }, _cancelToken);
                _waitIndex = service.LastIndex;

                var silos = new List<SiloAddress>();
                foreach (var inst in service.Response)
                {
                    var ip = IPAddress.Parse(inst.ServiceAddress);
                    var ep = new IPEndPoint(ip, inst.ServicePort);
                    silos.Add(SiloAddress.New(ep, 0));
                }

                _silos = silos.ToDictionary(el => el, _ => SiloStatus.Active);

                foreach (var subscriber in _subscribers)
                {
                    foreach (var silo in silos)
                    {
                        subscriber.SiloStatusChangeNotification(silo, SiloStatus.Active);
                    }
                }
            }
        }

        public Task BecomeActive()
        {
            return Task.FromResult(0);
        }

        public Task ShutDown()
        {
            return Task.FromResult(0);
        }

        public Task Stop()
        {
            _running = false;
            return Task.FromResult(0);
        }

        public Task KillMyself()
        {
            if (_silos.ContainsKey(_siloDetails.SiloAddress))
                _silos.Remove(_siloDetails.SiloAddress);

            return Task.FromResult(0);
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
            if (_siloDetails.SiloAddress == silo)
                return true;

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
            _client?.Dispose();
        }
    }
}

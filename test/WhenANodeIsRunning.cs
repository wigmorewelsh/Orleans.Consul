using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using src;
using Shouldly;
using Xunit;

namespace test
{
    [Collection("Consul")]
    public class WhenANodeIsRunning : IDisposable
    {
        private ConsulServer _server;

        public WhenANodeIsRunning(ConsulServer server)
        {
            _server = server;

        }

        [Fact]
        public async Task dd()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "1", 1235);

            var listener = new Listener();
            client.Subscribe(listener);

            listener.Services.Count.ShouldBe(0);

            await client.EnsureRegistered("service1", "2", 1235);

            await Task.Delay(TimeSpan.FromMilliseconds(50));

            listener.Services.Count.ShouldBe(1);

            await client.EnsureRegistered("service1", "2", 1444);

            await Task.Delay(TimeSpan.FromMilliseconds(50));

            listener.Services.Count.ShouldBe(2);
        }

        public void Dispose()
        {
            _server?.Cleanup();
        }
    }

    public class Listener : IListener
    {
        public List<string> Services { get; private set; } = new List<string>();
        public void Update(List<string> tags)
        {
            Services = tags;
        }
    }
}

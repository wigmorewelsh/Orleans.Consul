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
    public class WhenANodeStartsUp
    {
        private ConsulServer _server;

        public WhenANodeStartsUp(ConsulServer server)
        {
            _server = server;
        }

        [Fact]
        public async Task AndNoNodesAreRegistered()
        {
            var client = new ConsulGateway(new ConsulFactory());

            var records = await client.LookupRegistered("service1");
            records.ShouldBeEmpty();
        }

        [Fact]
        public async Task ShouldAddAServiceRecord()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "123");

            var records = await client.LookupRegistered("service1");
            records.ShouldBe("123");
        }

        [Fact]
        public async Task WhenARecordAlreadyExistsShouldUpdateGenerationTag()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "123");

            await client.EnsureRegistered("service1", "1234");

            var records = await client.LookupRegistered("service1");
            records.ShouldBe("1234");
        }

        [Fact]
        public async Task ShouldAddAServiceRecordB()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "1234");

            var records = await client.LookupRegistered("service1");
            records.ShouldBe("1234");
        }

        [Fact]
        public async Task WhenLookingUpWithDifferentNameShouldBeEmpty()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "1234");

            var records = await client.LookupRegistered("service2");
            records.ShouldBeEmpty();
        }
    }
}

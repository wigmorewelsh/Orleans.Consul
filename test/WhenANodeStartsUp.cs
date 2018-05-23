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
    public class WhenANodeStartsUp : IDisposable
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
            await client.EnsureRegistered("service1", "123", 1235);

            var records = await client.LookupRegistered("service1");
            records.Count.ShouldBe(1);
            records[0].Generation.ShouldBe("123");
        }

        [Fact]
        public async Task WhenARecordAlreadyExistsShouldUpdateGenerationTag()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "123", 1235);

            await client.EnsureRegistered("service1", "1234", 1235);

            var records = await client.LookupRegistered("service1");
            records.Count.ShouldBe(1);
            records[0].Generation.ShouldBe("1234");
        }

        [Fact]
        public async Task ShouldAddAServiceRecordB()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "1234", 1235);

            var records = await client.LookupRegistered("service1");
            records.Count.ShouldBe(1);
            records[0].Generation.ShouldBe("1234");
        }

        [Fact]
        public async Task WhenLookingUpWithDifferentNameShouldBeEmpty()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "1234", 1235);

            var records = await client.LookupRegistered("service2");
            records.ShouldBeEmpty();
        }

        [Fact]
        public async Task ServicesWithDifferentPortsShouldHaveSeparateRecords()
        {
            var client = new ConsulGateway(new ConsulFactory());
            await client.EnsureRegistered("service1", "123", 1111);


            var client2 = new ConsulGateway(new ConsulFactory());
            await client2.EnsureRegistered("service1", "1234", 2222);

            var records = await client.LookupRegistered("service1");
            records.Count.ShouldBe(2);
        }

        public void Dispose()
        {
            _server?.Cleanup();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using aln;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Orleans;
using Orleans.Configuration;
using Orleans.Consul;
using Orleans.Hosting;
using Orleans.TestingHost;
using Shouldly;
using Xunit;

namespace test
{
    [Collection("Consul")]
    public class ClusterIntegrationTests
    {
        private ConsulServer _server;

        public ClusterIntegrationTests(ConsulServer server)
        {
            _server = server;
        }


        [Fact]
        public async Task StartupSingleNodeCluster()
        {

            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();

            builder.Options.InitialSilosCount = 1;
            var host = builder.Build();
            await host.DeployAsync();

            var ff = host.GrainFactory.GetGrain<IName>(0);

            var name = await ff.SayHello();
            name.ShouldBe("test");

            host.StopAllSilos();
        }

        [Fact]
        public async Task StartupTwoNodeCluster()
        {

            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();

            builder.Options.InitialSilosCount = 2;
            var host = builder.Build();
            await host.DeployAsync();


            for (int i = 0; i < 10; i++)
            {
                var ff = host.GrainFactory.GetGrain<IName>(i);

                var name = await ff.SayHello();
                name.ShouldBe("test");
            }


            host.StopAllSilos();
        }

        public class SiloConfigurator : ISiloBuilderConfigurator
        {
            public void Configure(ISiloHostBuilder hostBuilder)
            {
                var rand = new Random().Next();

                hostBuilder.Configure<ClusterOptions>(options => options.ClusterId = "docker")
                    .Configure<ClusterMembershipOptions>(options => { options.ValidateInitialConnectivity = false; })
                    .UseConsulServiceClustering(clusterAddress: $"cluster{rand}", livenessAddress: $"liveness{rand}", consulAddress: "http://127.0.0.1:8500")

                    ;

                hostBuilder.AddMemoryGrainStorageAsDefault();
                
                hostBuilder
                    .ConfigureApplicationParts(parts =>
                    {
                        parts.AddApplicationPart(typeof(aln.Program).Assembly);
                        parts.AddApplicationPart(typeof(IName).Assembly);
                    });
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using aln;
using Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Orleans;
using Orleans.Hosting;
using Orleans.TestingHost;
using Xunit;

namespace test
{
    public class Class1
    {
        [Fact]
        public async Task test()
        {

            var builder = new TestClusterBuilder();
            builder.AddSiloBuilderConfigurator<SiloConfigurator>();
            var host = builder.Build();

            await host.DeployAsync();

            var ff = host.GrainFactory.GetGrain<IName>(0);

            await ff.SayHello();

            host.StopAllSilos();
        }

        public class SiloConfigurator : ISiloBuilderConfigurator
        {
            public void Configure(ISiloHostBuilder hostBuilder)
            {
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

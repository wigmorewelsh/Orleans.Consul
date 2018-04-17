
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Messaging;
using Orleans.Runtime;
using src;

namespace Orleans.Consul
{
    public static class BuilderExt
    {
        public static ISiloHostBuilder UseConsulServiceClustering(this ISiloHostBuilder builder, string clusterAddress, string livenessAddress, string consulAddress)
        {
            return builder.ConfigureServices(
                services =>
                {
                    services.Configure<ConsulMembershipOptions>(conf =>
                    {
                        conf.LivenessAddress = livenessAddress;
                        conf.ClusterAddress = clusterAddress;
                        conf.ConsulAddress = consulAddress;
                    });

                    services.Configure<ConsulGatewayOptions>(conf =>
                    {
                        conf.ClusterAddress = clusterAddress;
                        conf.ConsulAddress = consulAddress;
                    });

                    services.AddSingleton<IMembershipOracle, ConsulMembershipOracle>();
                    services.TryAddSingleton<ISiloStatusOracle>(provider => provider.GetService<IMembershipOracle>());
                    services.AddSingleton<IGatewayListProvider, ConsulGatewayListProvider>();
                });
        }

        public static IClientBuilder UseConsulServiceClustering(this IClientBuilder builder, string clusterAddress, string consulAddress)
        {
            return builder.ConfigureServices(services =>
                {
                    services.Configure<ConsulGatewayOptions>(conf =>
                    {
                        conf.ClusterAddress = clusterAddress;
                        conf.ConsulAddress = consulAddress;
                    });

                    services.AddSingleton<IGatewayListProvider, ConsulGatewayListProvider>();
                });
        }
    }
}

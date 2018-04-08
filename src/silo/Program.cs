using System;
using System.Net;
using System.Threading.Tasks;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Logging;
using Microsoft.Extensions.Logging;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Interfaces;
using Orleans.Consul;

namespace aln
{
    public class Config {
        public int SiloPort {get;set;} = 11111;
        public int GatewayPort {get;set;} = 30000;
        public string ClusterAddress { get; set; } = "orleans-gateway";
        public string LivenessAddress { get; set; } = "orleans-liveness";
    }

  public class Namer : Grain, IName
  {
    public Task SayHello()
    {
        return Task.FromResult(0);
    }
  }

  public class Program
    {
        private static ISiloHost silo;
        private static readonly ManualResetEvent siloStopped = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

            var configRoot = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build(); 
            var config = new Config();
            configRoot.Bind(config);

            silo = new SiloHostBuilder()
                .Configure<ClusterOptions>(options => options.ClusterId = "docker" )
                .Configure<ClusterMembershipOptions>(options => {
                    options.ValidateInitialConnectivity = false;
                })
                .UseConsulServiceClustering(clusterAddress: config.ClusterAddress, livenessAddress: config.LivenessAddress, consulAddress: "http://127.0.0.1:8500")
                .Configure<EndpointOptions>(options => options.AdvertisedIPAddress = IPAddress.Parse("0.0.0.0"))
                .ConfigureEndpoints(IPAddress.Parse("0.0.0.0"), siloPort: config.SiloPort, gatewayPort: config.GatewayPort)
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(Program).Assembly).WithReferences())
                .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddConsole())
                .Build();


            Task.Run(StartSilo);

            siloStopped.WaitOne();
        }

        private static async Task StartSilo()
        {
            await silo.StartAsync();
            Console.WriteLine("Silo started");
        }

        private static async Task StopSilo()
        {
            await silo.StopAsync();
            Console.WriteLine("Silo stopped");
            siloStopped.Set();
        }
    }
}

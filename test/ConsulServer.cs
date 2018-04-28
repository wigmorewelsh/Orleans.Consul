using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.PlatformAbstractions;
using src;
using Xunit;
using Shouldly;

namespace test
{
    [Collection("Consul")]
    public class ConsulTests
    {
        private readonly ConsulServer _server;

        public ConsulTests(ConsulServer server)
        {
            _server = server;
        }

        [Fact]
        public async Task Test()
        {
            var cli = new ConsulGateway(new ConsulFactory());
            await cli.SetProperty("ping", "pong");

            var dat = await cli.GetProperty("ping");

            dat.ShouldBe("pong");
        }
    }


    [CollectionDefinition("Consul")]
    public class DatabaseCollection : ICollectionFixture<ConsulServer>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class ConsulServer : IDisposable
    {
        private static Process _process;

        public ConsulServer()
        {
            spinUp().Wait(TimeSpan.FromSeconds(5));
        }

        public void Cleanup()
        {
            cleanup().Wait(TimeSpan.FromSeconds(5));
        }

        private static async Task cleanup()
        {
            using (var gateway = new ConsulGateway(new ConsulFactory()))
            {
                await gateway.Cleanup();
            }
        }

        public void Dispose()
        {
            _process?.Kill();
        }

        private static async Task spinUp()
        {
            // First, ping to see if consul is running.

            var isRunning = false;

            try
            {
                using (var gateway = new ConsulGateway(new ConsulFactory()))
                {
                    await gateway.SetProperty("ping", "value");
                    isRunning = true;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("No Consul agent found, proceeding to start one up");
            }

            if (isRunning) return;


            var basePath = AppContext.BaseDirectory;
            while (!File.Exists(Path.Combine(basePath, "ConsulServer.cs")))
                basePath = Directory.GetParent(basePath).FullName;

            string executable = null;

            executable = Path.Combine(basePath, "consul.exe");


            var processStartInfo = new ProcessStartInfo(executable, "agent -dev");
            processStartInfo.CreateNoWindow = true;
            processStartInfo.UseShellExecute = false;
            _process = Process.Start(processStartInfo);

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using (var gateway = new ConsulGateway(new ConsulFactory()))
                    {
                        await gateway.SetProperty("ping", "value");
                        break;
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(100);
                }
            }

        }
    }
}

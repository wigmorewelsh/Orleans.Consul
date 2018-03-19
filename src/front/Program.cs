using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace front
{
    public class Config {
        public int Port {get;set;} = 5000;
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            
            var configRoot = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build(); 
            var config = new Config();
            configRoot.Bind(config);

            return WebHost.CreateDefaultBuilder(args)
                .UseUrls($"http://0.0.0.0:{config.Port}")
                .UseStartup<Startup>()
                .Build();
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Consul;

namespace front
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton<IClusterClient>(CreateClusterClient);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }

        public string ClusterAddress { get; set; } = "orleans-liveness";

        private IClusterClient CreateClusterClient(IServiceProvider serviceProvider)
        {
            var client = new ClientBuilder()
                .UseConsulServiceClustering(clusterAddress: ClusterAddress, consulAddress: "http://127.0.0.1:8500")
                .ConfigureApplicationParts(parts => parts.AddApplicationPart(typeof(IName).Assembly))
                .Configure<ClusterOptions>(options => options.ClusterId = "docker")
                .Build();
            client.Connect().Wait();
            return client;
        }

        private static async Task StartClientWithRetries(IClusterClient client)
        {
            for (var i=0; i<5; i++)
            {
                try
                {
                    await client.Connect();
                    return;
                }
                catch(Exception)
                { }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Contract.Models;
using GrandCentralDispatch.Contract.Resolvers;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Metrics;
using GrandCentralDispatch.Monitoring.Extensions;
using GrandCentralDispatch.Options;
using Microsoft.AspNetCore.Hosting;
using Newtonsoft.Json;
using Host = GrandCentralDispatch.Models.Host;

namespace GrandCentralDispatch.Cluster
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMonitoringService();
            services.ConfigureCluster(clusterOptions =>
                {
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCD_CLUSTER_NODES")))
                    {
                        var hosts = JsonConvert.DeserializeObject<List<Host>>(
                            Environment.GetEnvironmentVariable("GCD_CLUSTER_NODES"));
                        clusterOptions.Hosts = hosts.ToHashSet();
                    }
                    else
                    {
                        var hosts = Configuration.GetSection("GCD_CLUSTER_NODES")
                            .GetChildren()
                            .Select(x => new Models.Host(x.GetValue<string>("MachineName"),
                                x.GetValue<int>("Port")))
                            .ToHashSet();
                        clusterOptions.Hosts = hosts;
                    }

                    clusterOptions.ExecuteRemotely = true;
                    clusterOptions.NodeQueuingStrategy = NodeQueuingStrategy.Healthiest;
                    clusterOptions.PersistenceEnabled = true;
                },
                circuitBreakerOptions => { });

            // Set-up the cluster
            services.AddCluster<Payload, Uri, string, string>(
                sp => new PayloadResolver(sp.GetService<ILoggerFactory>()),
                sp => new UriResolver(sp.GetService<ILoggerFactory>(), sp.GetService<IHttpClientFactory>()),
                sp => new RequestResolver(sp.GetService<ILoggerFactory>()));

            services.AddRemoteCluster<string, string>();

            services.AddControllers().AddMonitoringMetrics();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseMonitoring(app.ApplicationServices.GetServices<IExposeMetrics>());
            app.UseHttpsRedirection();
            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
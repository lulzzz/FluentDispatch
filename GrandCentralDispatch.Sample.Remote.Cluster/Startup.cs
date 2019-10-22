using System;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Sample.Remote.Contract.Models;
using GrandCentralDispatch.Sample.Remote.Contract.Resolvers;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Metrics;
using GrandCentralDispatch.Monitoring.Extensions;
using GrandCentralDispatch.Options;
using Microsoft.AspNetCore.Hosting;

namespace GrandCentralDispatch.Sample.Remote.Cluster
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
                    clusterOptions.ExecuteRemotely = true;
                    var hosts = Configuration.GetSection("Nodes")
                        .GetChildren()
                        .Select(x => new Models.Host(x.GetValue<string>("Address"),
                            x.GetValue<int>("Port")))
                        .ToHashSet();
                    clusterOptions.Hosts = hosts;
                    clusterOptions.NodeQueuingStrategy = NodeQueuingStrategy.Healthiest;
                    clusterOptions.PersistenceEnabled = true;
                },
                circuitBreakerOptions => { });

            // Set-up the cluster
            services.AddRemoteCluster<Payload, Uri, string, string>(
                sp => new PayloadResolver(sp.GetService<ILoggerFactory>()),
                sp => new UriResolver(sp.GetService<ILoggerFactory>(), sp.GetService<IHttpClientFactory>()),
                sp => new RequestResolver(sp.GetService<ILoggerFactory>()));

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
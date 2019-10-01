using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Sample.Remote.Contract.Models;
using GrandCentralDispatch.Sample.Remote.Contract.Resolvers;
using GrandCentralDispatch.Sample.Remote.Contract.Services;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Monitoring.Extensions;
using GrandCentralDispatch.Options;

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
                        .Select(x => new Host(x.GetValue<string>("Address"),
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
                sp => new UriResolver(sp.GetService<ILoggerFactory>(), sp.GetService<IRestClient>()),
                sp => new RequestResolver(sp.GetService<ILoggerFactory>()));

            services.AddMvc().AddMonitoringMetrics().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseMonitoring();
            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
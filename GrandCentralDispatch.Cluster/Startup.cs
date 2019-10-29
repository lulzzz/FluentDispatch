using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Contract.Models;
using GrandCentralDispatch.Contract.Resolvers;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Options;
using Newtonsoft.Json;

namespace GrandCentralDispatch.Cluster
{
    public class Startup : Host.ClusterStartup
    {
        public Startup(IConfiguration configuration) : base(configuration)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.ConfigureCluster(clusterOptions =>
                {
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCD_CLUSTER_NODES")))
                    {
                        var hosts = JsonConvert.DeserializeObject<List<Models.Host>>(
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
                },
                circuitBreakerOptions => { });

            // Set-up the cluster
            services.AddCluster<Payload, Uri, string, string>(
                sp => new PayloadResolver(sp.GetService<ILoggerFactory>()),
                sp => new UriResolver(sp.GetService<ILoggerFactory>(), sp.GetService<IHttpClientFactory>()),
                sp => new RequestResolver(sp.GetService<ILoggerFactory>()));

            // Add an other cluster
            services.AddAsyncCluster<string, string>(sp => new HeaderResolver(sp.GetService<ILoggerFactory>()));

            base.ConfigureServices(services);
        }
    }
}
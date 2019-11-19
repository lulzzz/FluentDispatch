using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FluentDispatch.Contract.Models;
using FluentDispatch.Contract.Models.Tensorflow;
using FluentDispatch.Contract.Resolvers;
using FluentDispatch.Contract.Services.ElasticSearch;
using FluentDispatch.Extensions;
using FluentDispatch.Options;
using Newtonsoft.Json;

namespace FluentDispatch.Cluster
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
                    if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLUENTDISPATCH_CLUSTER_NODES")))
                    {
                        var hosts = JsonConvert.DeserializeObject<List<FluentDispatch.Models.Host>>(
                            Environment.GetEnvironmentVariable("FLUENTDISPATCH_CLUSTER_NODES"));
                        clusterOptions.Hosts = hosts.ToHashSet();
                    }
                    else
                    {
                        var hosts = Configuration.GetSection("FLUENTDISPATCH_CLUSTER_NODES")
                            .GetChildren()
                            .Select(x => new FluentDispatch.Models.Host(x.GetValue<string>("MachineName"),
                                x.GetValue<int>("Port")))
                            .ToHashSet();
                        clusterOptions.Hosts = hosts;
                    }

                    clusterOptions.ExecuteRemotely = true;
                    clusterOptions.NodeQueuingStrategy = NodeQueuingStrategy.Healthiest;
                },
                circuitBreakerOptions => { });

            // Set-up the cluster
            services.AddCluster<MovieDetails, MovieReview, MovieDetails, MovieReviewSentimentPrediction>(
                sp => new MetadataResolver(sp.GetService<ILoggerFactory>(), sp.GetService<IConfiguration>()),
                sp => new SentimentPredictionResolver(sp.GetService<ILoggerFactory>()),
                sp => new IndexerResolver(sp.GetService<ILoggerFactory>(), sp.GetService<IElasticSearchService>()));

            base.ConfigureServices(services);
        }
    }
}
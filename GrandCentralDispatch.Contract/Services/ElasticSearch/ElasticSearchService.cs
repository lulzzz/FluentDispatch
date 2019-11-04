using System;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using GrandCentralDispatch.Contract.Helpers;
using GrandCentralDispatch.Contract.Models.ElasticSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nest;

namespace GrandCentralDispatch.Contract.Services.ElasticSearch
{
    public class ElasticSearchService : IElasticSearchService
    {
        public Lazy<Task<IElasticClient>> Client { get; }
        private readonly ILogger _logger;

        public ElasticSearchService(ILoggerFactory loggerFactory, IConfiguration configuration)
        {
            _logger = loggerFactory.CreateLogger<ElasticSearchService>();
            var pool = new SingleNodeConnectionPool(new Uri(configuration["ELASTICSEARCH_ENDPOINT"]));
            var connSettings = new ConnectionSettings(pool)
                .DefaultMappingFor<Review>(m => m
                    .IndexName(Constants.ReviewIndexName)
                )
                .BasicAuthentication(configuration["ELASTICSEARCH_USER"], configuration["ELASTICSEARCH_PASSWORD"])
                .IncludeServerStackTraceOnError()
                .EnableHttpPipelining()
                .EnableHttpCompression();

            Client = new Lazy<Task<IElasticClient>>(async () =>
            {
                var client = new ElasticClient(connSettings);
                try
                {
                    var mapping =
                        new CreateIndexDescriptor(Constants.ReviewIndexName)
                            .Map<Review>(m => m
                                .AutoMap()
                            );

                    if (!(await client.Indices.ExistsAsync(Constants.ReviewIndexName)).Exists)
                    {
                        await client.Indices.CreateAsync(mapping);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ex.Message);
                }

                return client;
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }
}
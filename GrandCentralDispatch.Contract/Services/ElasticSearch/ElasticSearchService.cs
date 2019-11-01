using System;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using GrandCentralDispatch.Contract.Models;
using Microsoft.Extensions.Logging;
using Nest;

namespace GrandCentralDispatch.Contract.Services.ElasticSearch
{
    public class ElasticSearchService : IElasticSearchService
    {
        public Lazy<Task<IElasticClient>> Client { get; }
        private readonly ILogger _logger;

        public ElasticSearchService(ILogger logger)
        {
            _logger = logger;
            var pool = new SingleNodeConnectionPool(new Uri("http://localhost:9200"));
            var connSettings = new ConnectionSettings(pool)
                .DefaultMappingFor<Sentiment>(m => m
                    .IndexName("sentiment")
                )
                .BasicAuthentication("elastic", "admin")
                .IncludeServerStackTraceOnError()
                .EnableHttpPipelining()
                .EnableHttpCompression();

            Client = new Lazy<Task<IElasticClient>>(async () =>
            {
                var client = new ElasticClient(connSettings);
                try
                {
                    var workerMapping =
                        new CreateIndexDescriptor("sentiment")
                            .Map<Sentiment>(m => m
                                .AutoMap()
                            );

                    if (!(await client.Indices.ExistsAsync("sentiment")).Exists)
                    {
                        await client.Indices.CreateAsync(workerMapping);
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
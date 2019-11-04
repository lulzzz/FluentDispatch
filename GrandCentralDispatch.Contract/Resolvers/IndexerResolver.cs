using System;
using GrandCentralDispatch.Contract.Helpers;
using GrandCentralDispatch.Contract.Models;
using GrandCentralDispatch.Contract.Models.ElasticSearch;
using GrandCentralDispatch.Contract.Models.Tensorflow;
using GrandCentralDispatch.Contract.Services.ElasticSearch;
using MagicOnion;
using MessagePack;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Contract.Resolvers
{
    public sealed class IndexerResolver : DualFuncRemoteResolver<MovieDetails, MovieReviewSentimentPrediction>
    {
        private readonly ILogger _logger;
        private readonly IElasticSearchService _elasticSearchService;

        public IndexerResolver(ILoggerFactory loggerFactory,
            IElasticSearchService elasticSearchService)
        {
            _logger = loggerFactory.CreateLogger<IndexerResolver>();
            _elasticSearchService = elasticSearchService;
        }

        /// <summary>
        /// Process the result of both resolvers
        /// </summary>
        /// <param name="details"><see cref="MovieDetails"/></param>
        /// <param name="prediction"><see cref="MovieReviewSentimentPrediction"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{Nil}"/></returns>
        public override async UnaryResult<Nil> ProcessRemotely(MovieDetails details,
            MovieReviewSentimentPrediction prediction,
            NodeMetrics nodeMetrics)
        {
            _logger.LogInformation(
                $"Indexing results for movie {details.Title}.");
            var elasticClient = await _elasticSearchService.Client.Value;
            var elasticResponse = await elasticClient.IndexAsync(new Review
                {
                    Date = DateTimeOffset.Now,
                    Title = details.Title,
                    Overview = details.Overview,
                    Liked = prediction.Prediction[1] > 0.5
                },
                i => i.Index(Constants.ReviewIndexName));
            if (!elasticResponse.IsValid)
            {
                _logger.LogError(elasticResponse.OriginalException.Message);
            }

            return await UnaryResult(Nil.Default);
        }
    }
}
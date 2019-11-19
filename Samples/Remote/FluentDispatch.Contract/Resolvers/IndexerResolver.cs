using System;
using FluentDispatch.Contract.Helpers;
using FluentDispatch.Contract.Models;
using FluentDispatch.Contract.Models.ElasticSearch;
using FluentDispatch.Contract.Models.Tensorflow;
using FluentDispatch.Contract.Services.ElasticSearch;
using MagicOnion;
using MessagePack;
using Microsoft.Extensions.Logging;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;

namespace FluentDispatch.Contract.Resolvers
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
                    Liked = prediction.Prediction[0] < 0.5
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
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;

namespace FluentDispatch.Sample.Web.Resolvers
{
    internal sealed class HeaderResolver : PartialResolver<string, string>
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public HeaderResolver(IMemoryCache cache,
            ILoggerFactory loggerFactory)
        {
            _cache = cache;
            _logger = loggerFactory.CreateLogger<HeaderResolver>();
        }

        /// <summary>
        /// Process each new header
        /// </summary>
        /// <param name="header">Header</param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override Task<string> Process(string header,
            NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"New headers received, inserting to memory cache identified by request identifier from node {nodeMetrics.Id}...");
            _cache.Set(Guid.NewGuid(), header);
            return Task.FromResult(header);
        }
    }
}
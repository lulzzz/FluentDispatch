using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Sample.Web.Resolvers
{
    internal sealed class CookieResolver : PartialResolver<string, string>
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger _logger;

        public CookieResolver(IMemoryCache cache,
            ILoggerFactory loggerFactory)
        {
            _cache = cache;
            _logger = loggerFactory.CreateLogger<CookieResolver>();
        }

        /// <summary>
        /// Process each new cookies
        /// </summary>
        /// <param name="cookie">Cookie</param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override Task<string> Process(string cookie,
            NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"New cookies received, inserting to memory cache identified by request identifier from node {nodeMetrics.Id}...");
            _cache.Set(Guid.NewGuid(), cookie);
            return Task.FromResult(cookie);
        }
    }
}
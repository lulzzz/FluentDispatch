using System;
using System.Net.Http;
using System.Threading;
using MagicOnion;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Contract.Resolvers
{
    public sealed class UriResolver : Item2RemotePartialResolver<Uri, string>
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public UriResolver(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = loggerFactory.CreateLogger<UriResolver>();
        }

        /// <summary>
        /// Process each new URI and download its content
        /// </summary>
        /// <param name="item"><see cref="Uri"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{TResult}"/></returns>
        public override async UnaryResult<string> ProcessItem2Remotely(Uri item,
            NodeMetrics nodeMetrics)
        {
            _logger.LogInformation(
                $"New URI {item.AbsoluteUri} received, trying to download content from node {nodeMetrics.Id}...");
            var response =
                await _httpClient.GetAsync(item, CancellationToken.None);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
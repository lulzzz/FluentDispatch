using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;
using FluentDispatch.Sample.Web.Models;

namespace FluentDispatch.Sample.Web.Resolvers
{
    internal sealed class IpResolver : PartialResolver<IPAddress, Geolocation>
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public IpResolver(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
            _logger = loggerFactory.CreateLogger<IpResolver>();
        }

        /// <summary>
        /// Process each new IP address to resolve its geolocation
        /// </summary>
        /// <param name="item"><see cref="KeyValuePair{TKey,TValue}"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task<Geolocation> Process(IPAddress item,
            NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"New IP {item} received, trying to resolve geolocation from node {nodeMetrics.Id}...");
            var response = await _httpClient.GetAsync(new Uri($"http://ip-api.com/json/{item}"),
                cancellationToken);
            var geolocation = JsonConvert.DeserializeObject<Geolocation>(await response.Content.ReadAsStringAsync());
            return geolocation;
        }
    }
}
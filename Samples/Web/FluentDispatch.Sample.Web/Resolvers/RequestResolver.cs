using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;
using FluentDispatch.Sample.Web.Models;

namespace FluentDispatch.Sample.Web.Resolvers
{
    internal sealed class RequestResolver : DualResolver<Geolocation, string>
    {
        private readonly ILogger _logger;

        public RequestResolver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<RequestResolver>();
        }

        /// <summary>
        /// Process the result of both resolvers
        /// </summary>
        /// <param name="location"><see cref="Geolocation"/></param>
        /// <param name="header">Header value</param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(Geolocation location,
            string header,
            NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                $"Header {header} associated to location (lat: {location.Lat};lon: {location.Lon})");
            await Task.CompletedTask;
        }
    }
}
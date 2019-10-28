using System.Threading.Tasks;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;
using MagicOnion;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.Contract.Resolvers
{
    public class HeaderResolver : RemoteResolver<string, string>
    {
        private readonly ILogger _logger;

        public HeaderResolver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HeaderResolver>();
        }

        /// <summary>
        /// Process each new payload
        /// </summary>
        /// <param name="item"><see cref="string"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{TResult}"/></returns>
        public override async UnaryResult<string> ProcessRemotely(string item, NodeMetrics nodeMetrics)
        {
            _logger.LogInformation(
                $"New header {item} received from node {nodeMetrics.Id}...");
            return await Task.FromResult(item);
        }
    }
}
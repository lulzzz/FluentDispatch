using System.Threading.Tasks;
using MagicOnion;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Contract.Models;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Contract.Resolvers
{
    public sealed class PayloadResolver : Item1RemotePartialResolver<Payload, string>
    {
        private readonly ILogger _logger;

        public PayloadResolver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PayloadResolver>();
        }

        /// <summary>
        /// Process each new payload
        /// </summary>
        /// <param name="item"><see cref="Payload"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{string}"/></returns>
        public override async UnaryResult<string> ProcessItem1Remotely(Payload item,
            NodeMetrics nodeMetrics)
        {
            _logger.LogInformation(
                $"New payload {item.Body} received from node {nodeMetrics.Id}...");
            return await Task.FromResult(item.Body);
        }
    }
}
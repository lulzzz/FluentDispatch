using MagicOnion;
using MessagePack;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Contract.Resolvers
{
    public sealed class RequestResolver : DualFuncRemoteResolver<string, string>
    {
        private readonly ILogger _logger;

        public RequestResolver(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<RequestResolver>();
        }

        /// <summary>
        /// Process the result of both resolvers
        /// </summary>
        /// <param name="payload">Payload value</param>
        /// <param name="content">Downloaded content</param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns>True if handled correctly</returns>
        public override UnaryResult<Nil> ProcessRemotely(string payload,
            string content,
            NodeMetrics nodeMetrics)
        {
            _logger.LogInformation(
                $"Payload {payload} downloaded as {content}");
            return UnaryResult(Nil.Default);
        }
    }
}
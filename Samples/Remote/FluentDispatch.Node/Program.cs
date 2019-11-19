using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using FluentDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;

namespace FluentDispatch.Node
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var host = FluentDispatchNode<Startup>.CreateDefaultBuilder(true, LogLevel.Information,
                    typeof(Contract.Resolvers.MetadataResolver),
                    typeof(Contract.Resolvers.SentimentPredictionResolver),
                    typeof(Contract.Resolvers.IndexerResolver))
                .Build();
            await host.RunAsync();
        }
    }
}
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using GrandCentralDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.Node
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var host = GrandCentralDispatchNodeHost<Startup>.CreateDefaultBuilder(true, LogLevel.Information,
                    typeof(Contract.Resolvers.MetadataResolver),
                    typeof(Contract.Resolvers.SentimentPredictionResolver),
                    typeof(Contract.Resolvers.IndexerResolver))
                .Build();
            await host.RunAsync();
        }
    }
}
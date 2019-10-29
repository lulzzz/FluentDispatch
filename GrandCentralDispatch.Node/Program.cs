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
            var host = GrandCentralDispatchNodeHost<Startup>.CreateDefaultBuilder(true, LogLevel.Information,
                    typeof(Contract.Resolvers.PayloadResolver),
                    typeof(Contract.Resolvers.UriResolver),
                    typeof(Contract.Resolvers.RequestResolver),
                    typeof(Contract.Resolvers.HeaderResolver))
                .Build();
            await host.RunAsync();
        }
    }
}
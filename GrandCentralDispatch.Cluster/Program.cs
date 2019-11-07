using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using GrandCentralDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.Cluster
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var host = GrandCentralDispatchClusterHost<Startup>.CreateDefaultBuilder(true, LogLevel.Information, true)
                .Build();
            await host.RunAsync();
        }
    }
}
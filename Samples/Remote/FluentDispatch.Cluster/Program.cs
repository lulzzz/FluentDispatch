using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using FluentDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;

namespace FluentDispatch.Cluster
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var host = FluentDispatchCluster<Startup>.CreateDefaultBuilder(true, LogLevel.Information, true)
                .Build();
            await host.RunAsync();
        }
    }
}
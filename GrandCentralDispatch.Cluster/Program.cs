using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using GrandCentralDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.Cluster
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = GrandCentralDispatchClusterHost<Startup>.CreateDefaultBuilder(true, LogLevel.Information, true)
                .Build();
            await host.RunAsync();
        }
    }
}
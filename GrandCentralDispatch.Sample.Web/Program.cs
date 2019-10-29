using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using GrandCentralDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace GrandCentralDispatch.Sample.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = GrandCentralDispatchClusterHost<Startup>.CreateDefaultBuilder(true, LogLevel.Information, 5000, false)
                .Build();
            await host.RunAsync();
        }
    }
}
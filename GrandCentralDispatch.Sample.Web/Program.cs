using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using GrandCentralDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.Sample.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = GrandCentralDispatchHost<Startup>.CreateDefaultBuilder(true, LogLevel.Information, 5000, false)
                .Build();
            await host.RunAsync();
        }
    }
}
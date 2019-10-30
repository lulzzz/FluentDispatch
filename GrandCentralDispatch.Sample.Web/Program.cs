using System.Threading.Tasks;
using GrandCentralDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace GrandCentralDispatch.Sample.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = GrandCentralDispatchClusterHost<Startup>
                .CreateDefaultBuilder(true, LogLevel.Information, false, 5000)
                .Build();
            await host.RunAsync();
        }
    }
}
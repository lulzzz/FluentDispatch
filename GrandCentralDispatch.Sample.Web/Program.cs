using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using GrandCentralDispatch.Host.Hosting;

namespace GrandCentralDispatch.Sample.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = GrandCentralDispatchHost<Startup>.CreateDefaultBuilder(port: 5000).Build();
            await host.RunAsync();
        }
    }
}
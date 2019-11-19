using System.Threading.Tasks;
using FluentDispatch.Host.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace FluentDispatch.Sample.Web
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var host = FluentDispatchCluster<Startup>
                .CreateDefaultBuilder(true, LogLevel.Information, false, 5000)
                .Build();
            await host.RunAsync();
        }
    }
}
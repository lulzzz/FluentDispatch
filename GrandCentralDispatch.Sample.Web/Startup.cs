using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Sample.Web.Models;
using GrandCentralDispatch.Sample.Web.Resolvers;

namespace GrandCentralDispatch.Sample.Web
{
    public class Startup : Host.ClusterStartup
    {
        public Startup(IConfiguration configuration) : base(configuration)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpClient();

            // Configuring the clusters is mandatory
            services.ConfigureCluster(clusterOptions => { },
                circuitBreakerOptions => { });

            // This is an example of how to propagate informations between two differents resolvers without tied coupling them
            // The whole point here is to associate headers and geolocation from each incoming request
            // The RequestResolver will then manipulate the computed results from IpResolver and HeaderResolver
            services.AddCluster<IPAddress, string, Geolocation, string>(
                sp => new IpResolver(sp.GetService<IHttpClientFactory>(), sp.GetService<ILoggerFactory>()),
                sp => new HeaderResolver(sp.GetService<IMemoryCache>(), sp.GetService<ILoggerFactory>()),
                sp => new RequestResolver(sp.GetService<ILoggerFactory>()));
            base.ConfigureServices(services);
        }
    }
}
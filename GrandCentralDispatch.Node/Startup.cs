using GrandCentralDispatch.Contract.Services.ElasticSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrandCentralDispatch.Node
{
    public class Startup : Host.NodeStartup
    {
        public Startup(IConfiguration configuration) : base(configuration)
        {
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IElasticSearchService, ElasticSearchService>();
            services.AddHttpClient();
            base.ConfigureServices(services);
        }
    }
}
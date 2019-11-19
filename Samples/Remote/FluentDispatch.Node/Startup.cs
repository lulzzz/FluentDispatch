using FluentDispatch.Contract.Services.ElasticSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FluentDispatch.Node
{
    public class Startup : Host.NodeStartup
    {
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration) : base(configuration)
        {
            _configuration = configuration;
        }

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(_configuration);
            services.AddSingleton<IElasticSearchService, ElasticSearchService>();
            services.AddHttpClient();
            base.ConfigureServices(services);
        }
    }
}
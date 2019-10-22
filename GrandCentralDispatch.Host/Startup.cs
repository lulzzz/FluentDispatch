using GrandCentralDispatch.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using GrandCentralDispatch.Monitoring.Extensions;

namespace GrandCentralDispatch.Host
{
    public abstract class Startup
    {
        protected Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected IConfiguration Configuration { get; }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddMonitoringService();
            services.AddMvc().AddMonitoringMetrics().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        public virtual void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseMonitoring(app.ApplicationServices.GetServices<IExposeMetrics>());
            app.UseHttpsRedirection();
            app.UseMvc();
        }
    }
}
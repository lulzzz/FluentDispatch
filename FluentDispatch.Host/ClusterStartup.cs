﻿using FluentDispatch.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FluentDispatch.Monitoring.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace FluentDispatch.Host
{
    public abstract class ClusterStartup
    {
        protected ClusterStartup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected IConfiguration Configuration { get; }

        public virtual void ConfigureServices(IServiceCollection services)
        {
            services.AddMonitoringService();
            services.AddControllers().AddMonitoringMetrics();
        }

        public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
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
            app.UseRouting();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}
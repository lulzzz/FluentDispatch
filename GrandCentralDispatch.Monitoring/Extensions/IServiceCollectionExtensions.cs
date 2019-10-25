using System.Collections.Generic;
using App.Metrics;
using GrandCentralDispatch.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace GrandCentralDispatch.Monitoring.Extensions
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddMonitoringService(this IServiceCollection services)
        {
            services.AddMetricsReportingHostedService();
            services.AddMetricsTrackingMiddleware();
            services.AddMetricsEndpoints();
            return services;
        }

        public static IMvcBuilder AddMonitoringMetrics(this IMvcBuilder builder)
        {
            builder.AddMetrics();
            return builder;
        }

        public static IApplicationBuilder UseMonitoring(this IApplicationBuilder builder,
            IEnumerable<IExposeMetrics> exposedMetrics)
        {
            var metrics = builder.ApplicationServices.GetService<IMetricsRoot>();
            var monitoringEngine = new MonitoringEngine(metrics, exposedMetrics as IReadOnlyCollection<IExposeMetrics>);
            monitoringEngine.RegisterEngine();
            return builder;
        }
    }
}
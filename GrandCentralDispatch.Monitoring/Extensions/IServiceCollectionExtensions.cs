using App.Metrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using GrandCentralDispatch.Monitoring.Services.Background;

namespace GrandCentralDispatch.Monitoring.Extensions
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions
    /// </summary>
    public static class IServiceCollectionExtensions
    {
        public static IServiceCollection AddMonitoringService(this IServiceCollection services)
        {
            services.AddHostedService<MonitoringHostedService>();
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

        public static IApplicationBuilder UseMonitoring(this IApplicationBuilder builder)
        {
            var metrics = builder.ApplicationServices.GetService<IMetricsRoot>();
            MonitoringEngine.Metrics = metrics;
            return builder;
        }
    }
}
using System;
using App.Metrics;
using App.Metrics.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace GrandCentralDispatch.Monitoring.Extensions
{
    public static class IWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseMonitoring(this IWebHostBuilder builder, bool enabled)
        {
            builder
                .ConfigureMetricsWithDefaults(bld =>
                {
                    bld.Configuration.Configure(
                        options =>
                        {
                            options.Enabled = enabled;
                            options.ReportingEnabled = true;
                        });
                    bld.Report.ToInfluxDb(options =>
                    {
                        options.InfluxDb.BaseUri = new Uri(Environment.GetEnvironmentVariable("INFLUXDB"));
                        options.InfluxDb.Database = "grandcentraldispatchdb";
                        options.FlushInterval = TimeSpan.FromSeconds(5);
                        options.InfluxDb.CreateDataBaseIfNotExists = true;
                        options.HttpPolicy.Timeout = TimeSpan.FromSeconds(10);
                    });
                });

            builder.UseMetricsWebTracking();
            builder.UseMetrics<MetricsStartup>();
            return builder;
        }
    }
}
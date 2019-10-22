using System;
using System.Runtime.InteropServices;
using App.Metrics;
using App.Metrics.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace GrandCentralDispatch.Monitoring.Extensions
{
    public static class IWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseMonitoring(this IWebHostBuilder builder)
        {
            builder
                .ConfigureMetricsWithDefaults(bld =>
                {
                    bld.Configuration.Configure(
                        options =>
                        {
                            options.Enabled = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                            options.ReportingEnabled = true;
                        });
                    bld.Report.ToInfluxDb(options =>
                    {
                        options.InfluxDb.BaseUri = new Uri("http://127.0.0.1:8086");
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
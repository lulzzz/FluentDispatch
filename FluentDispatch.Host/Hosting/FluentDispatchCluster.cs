using System.IO;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using FluentDispatch.Monitoring.Extensions;
using System;

namespace FluentDispatch.Host.Hosting
{
    public static class FluentDispatchCluster<TStartup> where TStartup : ClusterStartup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IHostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder CreateDefaultBuilder(bool useSimpleConsoleLogger = true,
            bool enableMonitoring = false, int? port = null) =>
            CreateDefaultBuilder(useSimpleConsoleLogger, LogLevel.Debug, enableMonitoring, port);

        /// <summary>
        /// Initializes a new instance of the <see cref="IHostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <param name="useSimpleConsoleLogger"></param>
        /// <param name="minSimpleConsoleLoggerLogLevel"></param>
        /// <param name="port"></param>
        /// <param name="enableMonitoring">Enable Grafana monitoring (requires InfluxDb running locally)</param>
        /// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder CreateDefaultBuilder(bool useSimpleConsoleLogger,
            LogLevel minSimpleConsoleLoggerLogLevel, bool enableMonitoring, int? port = null)
        {
            var builder = new HostBuilder();
            builder.UseWindowsService();
            ConfigureHostConfigurationDefault(builder);
            ConfigureAppConfigurationDefault(builder);
            ConfigureServiceProvider(builder);
            ConfigureLoggingDefault(builder, useSimpleConsoleLogger, minSimpleConsoleLoggerLogLevel);
            ConfigureWebDefaults(builder, port, enableMonitoring);
            return builder;
        }

        private static void ConfigureServiceProvider(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                // Uses DOTNET_ environment variables and command line args
            });
        }

        private static void ConfigureWebDefaults(IHostBuilder builder, int? port, bool enableMonitoring)
        {
            builder.ConfigureWebHostDefaults(webHostBuilder =>
            {
                webHostBuilder.UseKestrel((hostingContext, options) =>
                {
                    if (port.HasValue)
                    {
                        options.Listen(IPAddress.Any, port.Value);
                    }
                    else
                    {
                        var listeningPort =
                            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCD_CLUSTER_LISTENING_PORT"))
                                ? (int.TryParse(Environment.GetEnvironmentVariable("GCD_CLUSTER_LISTENING_PORT"),
                                    out var parsedPort)
                                    ? parsedPort
                                    : hostingContext.Configuration.GetValue<int>("GCD_CLUSTER_LISTENING_PORT"))
                                : hostingContext.Configuration.GetValue<int>("GCD_CLUSTER_LISTENING_PORT");
                        options.Listen(IPAddress.Any, listeningPort);
                    }
                });
                webHostBuilder.UseMonitoring(enableMonitoring);
                webHostBuilder.UseStartup<TStartup>();
            });
        }

        private static void ConfigureHostConfigurationDefault(IHostBuilder builder)
        {
            builder.UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        private static void ConfigureAppConfigurationDefault(IHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            });
        }

        private static void ConfigureLoggingDefault(IHostBuilder builder, bool useSimpleConsoleLogger,
            LogLevel minSimpleConsoleLoggerLogLevel)
        {
            if (useSimpleConsoleLogger)
            {
                switch (minSimpleConsoleLoggerLogLevel)
                {
                    case LogLevel.Trace:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Verbose()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                    case LogLevel.Debug:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Debug()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                    case LogLevel.Information:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Information()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                    case LogLevel.Warning:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Warning()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                    case LogLevel.Error:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Error()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                    case LogLevel.Critical:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Fatal()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                    default:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Information()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                        );
                        break;
                }
            }
            else
            {
                var basePath =
                    $@"{Directory.GetParent(Assembly.GetExecutingAssembly().Location)}\logs";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }

                switch (minSimpleConsoleLoggerLogLevel)
                {
                    case LogLevel.Trace:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Verbose()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                    case LogLevel.Debug:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Debug()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                    case LogLevel.Information:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Information()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                    case LogLevel.Warning:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Warning()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                    case LogLevel.Error:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Error()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                    case LogLevel.Critical:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Fatal()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                    default:
                        builder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Information()
                            .Enrich.FromLogContext()
                            .WriteTo.File($@"{basePath}\log_.txt", rollingInterval: RollingInterval.Day, shared: true)
                        );
                        break;
                }
            }
        }
    }
}
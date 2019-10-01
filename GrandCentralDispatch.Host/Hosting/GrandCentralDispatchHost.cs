using System.IO;
using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using GrandCentralDispatch.Monitoring.Extensions;

namespace GrandCentralDispatch.Host.Hosting
{
    public static class GrandCentralDispatchHost<TStartup> where TStartup : Startup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IWebHostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <returns>The initialized <see cref="IWebHostBuilder"/>.</returns>
        public static IWebHostBuilder CreateDefaultBuilder(bool useSimpleConsoleLogger = true, int port = 8888) =>
            CreateDefaultBuilder(useSimpleConsoleLogger, LogLevel.Debug, port);

        /// <summary>
        /// Initializes a new instance of the <see cref="IWebHostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <param name="useSimpleConsoleLogger"></param>
        /// <param name="minSimpleConsoleLoggerLogLevel"></param>
        /// <param name="port"></param>
        /// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
        public static IWebHostBuilder CreateDefaultBuilder(bool useSimpleConsoleLogger,
            LogLevel minSimpleConsoleLoggerLogLevel, int port)
        {
            var builder = new WebHostBuilder();

            ConfigureHostConfigurationDefault(builder);
            ConfigureAppConfigurationDefault(builder);
            ConfigureLoggingDefault(builder, useSimpleConsoleLogger, minSimpleConsoleLoggerLogLevel);

            return builder.UseKestrel((hostingContext, options) => { options.Listen(IPAddress.Loopback, port); })
                .UseStartup<TStartup>().UseMonitoring();
        }

        private static void ConfigureHostConfigurationDefault(IWebHostBuilder builder)
        {
            builder.UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
        }

        private static void ConfigureAppConfigurationDefault(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;

                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);

                config.AddEnvironmentVariables();
            });
        }

        private static void ConfigureLoggingDefault(IWebHostBuilder builder, bool useSimpleConsoleLogger,
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
        }
    }
}
using System.IO;
using System.Net;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using MagicOnion.Hosting;
using Grpc.Core;
using System.Collections.Generic;
using MagicOnion.Server;
using System.Linq;
using FluentDispatch.Resolvers;

namespace FluentDispatch.Host.Hosting
{
    public static class FluentDispatchNode<TStartup> where TStartup : NodeStartup
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IHostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder CreateDefaultBuilder(bool useSimpleConsoleLogger = true,
            params Type[] resolvers) =>
            CreateDefaultBuilder(useSimpleConsoleLogger, LogLevel.Debug, resolvers);

        /// <summary>
        /// Initializes a new instance of the <see cref="IHostBuilder"/> class with pre-configured defaults.
        /// </summary>
        /// <param name="useSimpleConsoleLogger"></param>
        /// <param name="minSimpleConsoleLoggerLogLevel"></param>
        /// <param name="resolvers"></param>
        /// <returns>The initialized <see cref="IHostBuilder"/>.</returns>
        public static IHostBuilder CreateDefaultBuilder(
            bool useSimpleConsoleLogger,
            LogLevel minSimpleConsoleLoggerLogLevel,
            params Type[] resolvers)
        {
            var builder = MagicOnionHost.CreateDefaultBuilder();
            builder.UseWindowsService();
            ConfigureHostConfigurationDefault(builder);
            ConfigureServiceProvider(builder);
            ConfigureLoggingDefault(builder, useSimpleConsoleLogger, minSimpleConsoleLoggerLogLevel);
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.SetBasePath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            configurationBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            configurationBuilder.AddEnvironmentVariables();
            var configuration = configurationBuilder.Build();
            builder.UseMagicOnion(
                new List<ServerPort>
                {
                    new ServerPort(IPAddress.Any.ToString(),
                        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GCD_NODE_LISTENING_PORT"))
                            ? (int.TryParse(Environment.GetEnvironmentVariable("GCD_NODE_LISTENING_PORT"),
                                out var port)
                                ? port
                                : configuration.GetValue<int>("GCD_NODE_LISTENING_PORT"))
                            : configuration.GetValue<int>("GCD_NODE_LISTENING_PORT"), ServerCredentials.Insecure)
                },
                new MagicOnionOptions(true)
                {
                    MagicOnionLogger = new MagicOnionLogToGrpcLogger()
                },
                types: resolvers.Select(resolver =>
                {
                    var targetType = typeof(IResolver);
                    if (!targetType.GetTypeInfo().IsAssignableFrom(resolver.GetTypeInfo()))
                    {
                        throw new ArgumentException(nameof(resolver),
                            $"Type {resolver.Name} should implement IResolver interface.");
                    }

                    return resolver;
                }).Concat(new[]
                {
                    typeof(Hubs.Hub.NodeHub)
                }));
            builder.ConfigureServices(serviceCollection =>
            {
                var startup = (TStartup) Activator.CreateInstance(typeof(TStartup), configuration);
                startup.ConfigureServices(serviceCollection);
            });

            return builder;
        }

        private static void ConfigureServiceProvider(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                // Uses DOTNET_ environment variables and command line args
            });
        }

        private static void ConfigureHostConfigurationDefault(IHostBuilder builder)
        {
            builder.UseContentRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
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
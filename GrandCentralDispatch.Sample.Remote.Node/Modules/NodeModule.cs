using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using Autofac;
using Grpc.Core;
using MagicOnion.Hosting;
using MagicOnion.Server;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using GrandCentralDispatch.Sample.Remote.Contract.Services;

namespace GrandCentralDispatch.Sample.Remote.Node.Modules
{
    public class NodeModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.SetBasePath(Directory.GetCurrentDirectory());
            configurationBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            configurationBuilder.AddEnvironmentVariables();
            var configuration = configurationBuilder.Build();
            var basePath =
                $@"{Directory.GetParent(Assembly.GetAssembly(typeof(Program)).FullName).FullName}\logs";
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            var logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .Enrich.FromLogContext()
#if DEBUG
                .WriteTo.Console()
#else
                .WriteTo.File($@"{basePath}\log_node_.txt", rollingInterval: RollingInterval.Day, shared: true)
#endif
                .CreateLogger();

            builder.Register(c => logger).As<ILogger>().SingleInstance();
            builder.Register(c => MagicOnionHost.CreateDefaultBuilder()
                    .ConfigureServices((context, services) =>
                    {
                        services.AddSingleton<IRestClient, RestClient>();
                        services.AddSingleton<ILogger>(logger);
                        services.AddLogging(b =>
                        {
                            b.AddSerilog(logger);
                        });
                    })
                    .UseMagicOnion(
                        new List<ServerPort>
                        {
                            new ServerPort(IPAddress.Any.ToString(),
                                configuration.GetValue<int>("NodeListeningPort"), ServerCredentials.Insecure)
                        },
                        new MagicOnionOptions(true)
                        {
                            MagicOnionLogger = new MagicOnionLogToGrpcLogger()
                        },
                        types: new[]
                        {
                            typeof(Contract.Resolvers.PayloadResolver),
                            typeof(Contract.Resolvers.UriResolver),
                            typeof(Contract.Resolvers.RequestResolver),
                            typeof(Hubs.Hub.NodeHub)
                        }).Build())
                .As<IHost>()
                .SingleInstance();
        }
    }
}
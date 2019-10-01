using System.IO;
using System.Net;
using System.Reflection;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Serilog;
using GrandCentralDispatch.Monitoring.Extensions;

namespace GrandCentralDispatch.Sample.Remote.Cluster.Modules
{
    public class ApiModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var basePath = $@"{Directory.GetParent(Assembly.GetAssembly(typeof(Program)).FullName).FullName}\logs";
            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }

            builder.Register(c => new WebHostBuilder()
                    .ConfigureAppConfiguration((hostingContext, configurationBuilder) =>
                    {
                        configurationBuilder.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                        configurationBuilder.AddEnvironmentVariables();
                    })
                    .UseKestrel((hostingContext, options) =>
                    {
                        options.Listen(IPAddress.Loopback,
                            hostingContext.Configuration.GetValue<int>("ApiListeningPort"));
                    })
                    .UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Information()
                            .Enrich.FromLogContext()
#if DEBUG
                            .WriteTo.Console()
#else
                            .WriteTo.File($@"{basePath}\log_api_.txt", rollingInterval: RollingInterval.Day, shared: true)
#endif
                    )
                    .UseStartup<Startup>()
                    .UseMonitoring()
                    .UseContentRoot(Directory.GetCurrentDirectory())
                    .Build())
                .As<IWebHost>()
                .SingleInstance();
        }
    }
}
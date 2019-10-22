using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using GrandCentralDispatch.Monitoring.Extensions;
using Serilog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GrandCentralDispatch.Sample.Remote.Cluster
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await CreateWebHostBuilder(args).Build().RunAsync();
        }

        private static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            var builder = new HostBuilder();

            builder.UseContentRoot(Directory.GetCurrentDirectory());
            builder.ConfigureHostConfiguration(config =>
            {
                // Uses DOTNET_ environment variables and command line args
            });

            builder.ConfigureAppConfiguration((hostingContext, config) =>
                {
                    // JSON files, User secrets, environment variables and command line arguments
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    config.AddEnvironmentVariables();
                })
                .ConfigureLogging((hostingContext, logging) =>
                {
                    // Adds loggers for console, debug, event source, and EventLog (Windows only)
                })
                .UseDefaultServiceProvider((context, options) =>
                {
                    // Configures DI provider validation
                })
                .UseWindowsService()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    var basePath =
                        $@"{Directory.GetParent(Assembly.GetAssembly(typeof(Program)).FullName).FullName}\logs";
                    if (!Directory.Exists(basePath))
                    {
                        Directory.CreateDirectory(basePath);
                    }

                    webBuilder.UseKestrel((hostingContext, options) =>
                    {
                        options.Listen(IPAddress.Loopback,
                            hostingContext.Configuration.GetValue<int>("ApiListeningPort"));
                    });
                    webBuilder.UseMonitoring();
                    webBuilder.UseSerilog((hostingContext, loggerConfiguration) => loggerConfiguration
                            .MinimumLevel.Information()
                            .Enrich.FromLogContext()
#if DEBUG
                            .WriteTo.Console()
#else
                            .WriteTo.File($@"{basePath}\log_api_.txt", rollingInterval: RollingInterval.Day, shared: true)
#endif
                    );
                    webBuilder.UseStartup<Startup>();
                });

            return builder;
        }
    }
}
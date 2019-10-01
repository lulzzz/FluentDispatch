using System;
using System.IO;
using System.Reflection;
using Serilog;
using GrandCentralDispatch.Sample.Remote.Node.Services;
using Topshelf;

namespace GrandCentralDispatch.Sample.Remote.Node
{
    class Program
    {
        static void Main(string[] args)
        {
            var rc = HostFactory.Run(x =>
            {
                var basePath = $@"{Directory.GetParent(Assembly.GetAssembly(typeof(Program)).FullName).FullName}\logs";
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }

                x.Service<ICoreService>(c =>
                {
                    c.ConstructUsing(settings => new CoreService());

                    c.WhenStarted((s, host) =>
                    {
                        s.Start();
                        return true;
                    });

                    c.WhenStopped((s, host) =>
                    {
                        s.Stop();
                        return true;
                    });
                });

                x.SetServiceName("GrandCentralDispatch.Sample.Remote.Node");
                x.SetDisplayName("GrandCentralDispatch.Sample.Remote.Node");
                x.SetDescription("GCD node which receives the method calls from the cluster.");
                x.StartAutomatically();
                x.EnableServiceRecovery(serviceRecovery =>
                {
                    serviceRecovery.OnCrashOnly();
                    serviceRecovery.RestartService(0);
                });
                x.UseSerilog(new LoggerConfiguration()
                        .MinimumLevel.Information()
                        .Enrich.FromLogContext()
#if DEBUG
                        .WriteTo.Console()
#else
                        .WriteTo.File($@"{basePath}\log_service_.txt", rollingInterval: RollingInterval.Day, shared: true)
#endif
                );
            });

            var exitCode = (int) Convert.ChangeType(rc, rc.GetTypeCode());
            Environment.ExitCode = exitCode;
        }
    }
}
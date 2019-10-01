using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using GrandCentralDispatch.Monitoring.Helpers;

namespace GrandCentralDispatch.Monitoring.Services.Background
{
    public class MonitoringHostedService : IHostedService
    {
        private readonly Process _grafanaProcess;
        private readonly Process _influxDbProcess;
        private readonly ILogger _logger;

        public MonitoringHostedService(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory == null
                ? NullLogger<MonitoringHostedService>.Instance
                : loggerFactory.CreateLogger<MonitoringHostedService>();
            ExtractMonitoringTools();
            _influxDbProcess = RunInfluxDb();
            _grafanaProcess = RunGrafana();
            Task.Delay(2500).Wait();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _influxDbProcess?.Kill();
            _grafanaProcess?.Kill();
            return Task.CompletedTask;
        }

        private Process RunGrafana()
        {
            _logger.LogInformation("Launching Grafana...");
            var grafanaExecutable = Directory.GetFiles($@"{Constants.MonitoringFolder}\Grafana", "grafana-server.exe",
                SearchOption.AllDirectories);

            var config = Directory.GetFiles($@"{Constants.MonitoringFolder}\Grafana", "defaults.ini",
                SearchOption.AllDirectories);

            var home = Directory.GetDirectories($@"{Constants.MonitoringFolder}\Grafana").First();
            var pi = new ProcessStartInfo
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                FileName = grafanaExecutable.First(),
                Arguments = $@"--config={config.First()} --homepath={home}"
            };

            var grafanaProcess = Process.Start(pi);
            if (grafanaProcess != null)
            {
                ChildProcessTracker.AddProcess(grafanaProcess);
            }

            return grafanaProcess;
        }

        private Process RunInfluxDb()
        {
            _logger.LogInformation("Launching Influxdb...");
            var influxDbExecutable = Directory.GetFiles($@"{Constants.MonitoringFolder}\InfluxDb", "influxd.exe",
                SearchOption.AllDirectories);
            var pi = new ProcessStartInfo
            {
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                FileName = influxDbExecutable.First()
            };

            var influxDbProcess = Process.Start(pi);
            if (influxDbProcess != null)
            {
                ChildProcessTracker.AddProcess(influxDbProcess);
            }

            return influxDbProcess;
        }

        private void ExtractMonitoringTools()
        {
            if (!Directory.Exists(Constants.MonitoringFolder))
            {
                Directory.CreateDirectory(Constants.MonitoringFolder);
            }

            var grafanaFolder = $@"{Constants.MonitoringFolder}\Grafana";
            if (!Directory.Exists(grafanaFolder))
            {
                Directory.CreateDirectory(grafanaFolder);
            }

            var influxDbFolder = $@"{Constants.MonitoringFolder}\InfluxDb";
            if (!Directory.Exists(influxDbFolder))
            {
                Directory.CreateDirectory(influxDbFolder);
            }

            if (!File.Exists($@"{Constants.MonitoringFolder}\Grafana\Grafana.zip"))
            {
                _logger.LogInformation("Grafana not found! Extracting...");
                Helper.ExtractResource("GrandCentralDispatch.Monitoring.Tools.grafana-6.2.1.zip",
                    $@"{Constants.MonitoringFolder}\Grafana\Grafana.zip");
                _logger.LogInformation("Grafana extracted.");
            }

            if (!Directory.EnumerateDirectories($@"{Constants.MonitoringFolder}\Grafana").Any() ||
                !Directory.GetFiles($@"{Constants.MonitoringFolder}\Grafana", "grafana-server.exe",
                    SearchOption.AllDirectories).Any())
            {
                _logger.LogInformation("Installing Grafana...");
                ExtractZipFile($@"{Constants.MonitoringFolder}\Grafana\Grafana.zip",
                    $@"{Constants.MonitoringFolder}\Grafana");
                _logger.LogInformation("Grafana installed.");
            }

            if (!File.Exists($@"{Constants.MonitoringFolder}\InfluxDb\InfluxDb.zip"))
            {
                _logger.LogInformation("Influxdb not found! Extracting...");
                Helper.ExtractResource("GrandCentralDispatch.Monitoring.Tools.influxdb-1.7.6-1.zip",
                    $@"{Constants.MonitoringFolder}\InfluxDb\InfluxDb.zip");
                _logger.LogInformation("Influxdb extracted.");
            }

            if (!Directory.EnumerateDirectories($@"{Constants.MonitoringFolder}\InfluxDb").Any() ||
                !Directory.GetFiles($@"{Constants.MonitoringFolder}\InfluxDb", "influxd.exe",
                    SearchOption.AllDirectories).Any())
            {
                _logger.LogInformation("Installing Influxdb...");
                ExtractZipFile($@"{Constants.MonitoringFolder}\InfluxDb\InfluxDb.zip",
                    $@"{Constants.MonitoringFolder}\InfluxDb");
                _logger.LogInformation("Influxdb installed.");
            }
        }

        private void ExtractZipFile(string archiveFilenameIn, string outFolder)
        {
            ZipFile zf = null;
            try
            {
                var fs = File.OpenRead(archiveFilenameIn);
                zf = new ZipFile(fs);
                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                    {
                        continue;
                    }

                    var entryFileName = zipEntry.Name;
                    var buffer = new byte[4096];
                    var zipStream = zf.GetInputStream(zipEntry);
                    var fullZipToPath = Path.Combine(outFolder, entryFileName);
                    var directoryName = Path.GetDirectoryName(fullZipToPath);
                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    using (var streamWriter = File.Create(fullZipToPath))
                    {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true;
                    zf.Close();
                }
            }
        }
    }
}
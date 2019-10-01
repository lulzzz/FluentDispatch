using System.IO;

namespace GrandCentralDispatch.Monitoring.Helpers
{
    public class Constants
    {
        public static string MonitoringFolder { get; } =
            $@"{Path.GetTempPath()}GrandCentralDispatch-Cluster-Monitoring";
    }
}
using Quartz;

namespace GrandCentralDispatch.Sample.Remote.Cluster.Helpers
{
    public class Constants
    {
        public static readonly JobKey ApiJobKey =
            new JobKey("Api", "Api");
    }
}
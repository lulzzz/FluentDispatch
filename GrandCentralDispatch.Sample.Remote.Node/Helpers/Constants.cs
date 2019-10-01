using Quartz;

namespace GrandCentralDispatch.Sample.Remote.Node.Helpers
{
    public class Constants
    {
        public static readonly JobKey NodeJobKey =
            new JobKey("Node", "Node");
    }
}

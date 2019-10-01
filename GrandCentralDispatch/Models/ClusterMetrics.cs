namespace GrandCentralDispatch.Models
{
    public class ClusterMetrics
    {
        /// <summary>
        /// Current number of items processed per second by the cluster.
        /// </summary>
        public double CurrentThroughput { get; internal set; }

        public ClusterHealth Health { get; internal set; }
    }
}
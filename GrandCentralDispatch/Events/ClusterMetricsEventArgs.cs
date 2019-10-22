using System;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Events
{
    public class ClusterMetricsEventArgs : EventArgs
    {
        public ClusterMetrics ClusterMetrics { get; }

        public ClusterMetricsEventArgs(ClusterMetrics clusterMetrics)
        {
            ClusterMetrics = clusterMetrics;
        }
    }
}
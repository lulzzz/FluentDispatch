using System;
using FluentDispatch.Models;

namespace FluentDispatch.Events
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
using System;
using GrandCentralDispatch.Events;

namespace GrandCentralDispatch.Metrics
{
    public interface IExposeMetrics
    {
        /// <summary>
        /// Event raised when a node metric is submitted
        /// </summary>
        EventHandler<NodeMetricsEventArgs> NodeMetricSubmitted { get; set; }

        /// <summary>
        /// Event raised when a cluster metric is submitted
        /// </summary>
        EventHandler<ClusterMetricsEventArgs> ClusterMetricSubmitted { get; set; }
    }
}

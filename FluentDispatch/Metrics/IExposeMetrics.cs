using System;
using FluentDispatch.Events;

namespace FluentDispatch.Metrics
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

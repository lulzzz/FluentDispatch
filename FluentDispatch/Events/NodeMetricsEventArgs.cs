using System;
using FluentDispatch.Models;

namespace FluentDispatch.Events
{
    public class NodeMetricsEventArgs : EventArgs
    {
        public NodeMetrics NodeMetrics { get; }

        public NodeMetricsEventArgs(NodeMetrics nodeMetrics)
        {
            NodeMetrics = nodeMetrics;
        }
    }
}
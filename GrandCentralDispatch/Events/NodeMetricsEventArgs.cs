using System;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Events
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
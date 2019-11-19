using System;
using System.Collections.Concurrent;
using App.Metrics;
using App.Metrics.Histogram;
using App.Metrics.ReservoirSampling.Uniform;
using ConcurrentCollections;

namespace FluentDispatch.Monitoring
{
    internal static class MetricsRegistry
    {
        public static readonly ConcurrentDictionary<Guid, ConcurrentHashSet<HistogramOptions>> ClusterPerformanceCounters =
            new ConcurrentDictionary<Guid, ConcurrentHashSet<HistogramOptions>>();

        public static readonly ConcurrentDictionary<Guid, ConcurrentHashSet<HistogramOptions>> NodePerformanceCounters =
            new ConcurrentDictionary<Guid, ConcurrentHashSet<HistogramOptions>>();
    }
}
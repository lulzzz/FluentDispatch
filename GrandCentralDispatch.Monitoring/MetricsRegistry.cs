using System;
using System.Collections.Concurrent;
using App.Metrics;
using App.Metrics.Histogram;
using App.Metrics.ReservoirSampling.Uniform;
using ConcurrentCollections;
using GrandCentralDispatch.Monitoring.Helpers;

namespace GrandCentralDispatch.Monitoring
{
    internal static class MetricsRegistry
    {
        public static HistogramOptions ClusterThroughput => new HistogramOptions
        {
            Name = "Cluster Throughput",
            Reservoir = () => new DefaultAlgorithmRReservoir(),
            MeasurementUnit = Unit.Calls
        };

        public static readonly ConcurrentHashSet<HistogramOptions> ClusterPerformanceCounters =
            new ConcurrentHashSet<HistogramOptions>(KeyBasedEqualityComparer<HistogramOptions>.Create(x => x.Name));

        public static readonly ConcurrentDictionary<Guid, ConcurrentHashSet<HistogramOptions>> NodePerformanceCounters =
            new ConcurrentDictionary<Guid, ConcurrentHashSet<HistogramOptions>>();
    }
}
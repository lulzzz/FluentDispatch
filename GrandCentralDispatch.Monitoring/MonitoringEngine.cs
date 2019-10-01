using System;
using System.Collections.Generic;
using System.Linq;
using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using App.Metrics.ReservoirSampling.Uniform;
using ConcurrentCollections;
using GrandCentralDispatch.Monitoring.Helpers;

namespace GrandCentralDispatch.Monitoring
{
    public static class MonitoringEngine
    {
        public static IMetricsRoot Metrics;

        public static void SetClusterThroughput(double throughput)
        {
            Metrics?.Measure.Histogram.Update(MetricsRegistry.ClusterThroughput, Convert.ToInt64(throughput));
        }

        public static void SetNodeThroughput(Guid guid, string machineName, double throughput)
        {
            var tags = new MetricTags(new[] {"node_id", "machine_name"}, new[] {guid.ToString(), machineName});
            var histogram = new HistogramOptions
            {
                Context = "node",
                Tags = tags,
                Name = "Node Throughput",
                Reservoir = () => new DefaultAlgorithmRReservoir(),
                MeasurementUnit = Unit.None
            };

            Metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(throughput));
        }

        public static void SetNodeBufferSize(Guid guid, string machineName, int bufferSize)
        {
            var tags = new MetricTags(new[] {"node_id", "machine_name"}, new[] {guid.ToString(), machineName});
            var histogram = new HistogramOptions
            {
                Context = "node",
                Tags = tags,
                Name = "Node Buffer Size",
                Reservoir = () => new DefaultAlgorithmRReservoir(),
                MeasurementUnit = Unit.None
            };

            Metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(bufferSize));
        }

        public static void SetNodeItemsProcessed(Guid guid, string machineName, long itemsProcessed)
        {
            var tags = new MetricTags(new[] {"node_id", "machine_name"}, new[] {guid.ToString(), machineName});
            var gauge = new GaugeOptions
            {
                Context = "node",
                Tags = tags,
                Name = "Node Items Processed",
                MeasurementUnit = Unit.None
            };

            Metrics?.Measure.Gauge.SetValue(gauge, itemsProcessed);
        }

        public static void SetNodeItemEvicted(Guid guid, string machineName, long itemsEvicted)
        {
            var tags = new MetricTags(new[] {"node_id", "machine_name"}, new[] {guid.ToString(), machineName});
            var histogram = new HistogramOptions
            {
                Context = "node",
                Tags = tags,
                Name = "Node Items Evicted",
                Reservoir = () => new DefaultAlgorithmRReservoir(),
                MeasurementUnit = Unit.None
            };

            Metrics?.Measure.Histogram.Update(histogram, itemsEvicted);
        }

        public static void SetClusterPerformanceCounters(IDictionary<string, float> performanceCounters)
        {
            foreach (var performanceCounter in performanceCounters)
            {
                if (MetricsRegistry.ClusterPerformanceCounters.All(counter => counter.Name != performanceCounter.Key))
                {
                    var histogram = new HistogramOptions
                    {
                        Context = "Cluster",
                        Name = performanceCounter.Key,
                        Reservoir = () => new DefaultAlgorithmRReservoir(),
                        MeasurementUnit = Unit.None
                    };

                    MetricsRegistry.ClusterPerformanceCounters.Add(histogram);
                    Metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
                else
                {
                    var histogram =
                        MetricsRegistry.ClusterPerformanceCounters.Single(counter =>
                            counter.Name == performanceCounter.Key);
                    Metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
            }
        }

        public static void SetNodePerformanceCounters(Guid guid, string machineName,
            IDictionary<string, float> performanceCounters)
        {
            if (!MetricsRegistry.NodePerformanceCounters.Any() ||
                MetricsRegistry.NodePerformanceCounters.All(counter => counter.Key != guid))
            {
                MetricsRegistry.NodePerformanceCounters.TryAdd(guid,
                    new ConcurrentHashSet<HistogramOptions>(
                        KeyBasedEqualityComparer<HistogramOptions>.Create(x => x.Name)));
            }

            var nodePerformanceCounters = MetricsRegistry.NodePerformanceCounters.Single(node => node.Key == guid);
            foreach (var performanceCounter in performanceCounters)
            {
                if (nodePerformanceCounters.Value.All(counter => counter.Name != performanceCounter.Key))
                {
                    var tags = new MetricTags(new[] {"node_id", "machine_name"}, new[] {guid.ToString(), machineName});
                    var histogram = new HistogramOptions
                    {
                        Context = "node",
                        Tags = tags,
                        Name = performanceCounter.Key,
                        Reservoir = () => new DefaultAlgorithmRReservoir(),
                        MeasurementUnit = Unit.None
                    };

                    nodePerformanceCounters.Value.Add(histogram);
                    Metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
                else
                {
                    var histogram =
                        nodePerformanceCounters.Value.Single(counter =>
                            counter.Name == performanceCounter.Key);
                    Metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
            }
        }
    }
}
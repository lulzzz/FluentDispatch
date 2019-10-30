using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using App.Metrics;
using App.Metrics.Gauge;
using App.Metrics.Histogram;
using App.Metrics.ReservoirSampling.Uniform;
using ConcurrentCollections;
using GrandCentralDispatch.Events;
using GrandCentralDispatch.Metrics;
using GrandCentralDispatch.Monitoring.Helpers;

namespace GrandCentralDispatch.Monitoring
{
    public class MonitoringEngine : IDisposable
    {
        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private bool _disposed;

        private readonly IMetricsRoot _metrics;
        private readonly IReadOnlyCollection<IExposeMetrics> _exposedMetrics;
        private readonly Stack<IDisposable> _subscriptions = new Stack<IDisposable>();

        public MonitoringEngine(IMetricsRoot metrics, IReadOnlyCollection<IExposeMetrics> exposedMetrics)
        {
            _metrics = metrics;
            _exposedMetrics = exposedMetrics;
        }

        public void RegisterEngine()
        {
            foreach (var exposedMetric in _exposedMetrics)
            {
                var clusterMetricSubscription = Observable
                    .FromEventPattern<EventHandler<ClusterMetricsEventArgs>, ClusterMetricsEventArgs>
                        (h => exposedMetric.ClusterMetricSubmitted += h, h => exposedMetric.ClusterMetricSubmitted -= h)
                    .Subscribe(onClusterMetric =>
                    {
                        var clusterMetrics = onClusterMetric.EventArgs.ClusterMetrics;
                        SetClusterThroughput(clusterMetrics.Id, clusterMetrics.Health.MachineName,
                            clusterMetrics.CurrentThroughput);
                        SetClusterPerformanceCounters(clusterMetrics.Id, clusterMetrics.Health.MachineName,
                            clusterMetrics.Health
                                .PerformanceCounters);
                    });

                var nodeMetricSubscription = Observable
                    .FromEventPattern<EventHandler<NodeMetricsEventArgs>, NodeMetricsEventArgs>
                        (h => exposedMetric.NodeMetricSubmitted += h, h => exposedMetric.NodeMetricSubmitted -= h)
                    .Subscribe(onNodeMetric =>
                    {
                        var nodeMetrics = onNodeMetric.EventArgs.NodeMetrics;
                        SetNodePerformanceCounters(nodeMetrics.Id,
                            nodeMetrics.RemoteNodeHealth.MachineName,
                            nodeMetrics.RemoteNodeHealth.PerformanceCounters);
                        SetNodeThroughput(nodeMetrics.Id,
                            nodeMetrics.RemoteNodeHealth.MachineName,
                            nodeMetrics.CurrentThroughput);
                        SetNodeItemsProcessed(nodeMetrics.Id,
                            nodeMetrics.RemoteNodeHealth.MachineName,
                            nodeMetrics.TotalItemsProcessed);
                        SetNodeBufferSize(nodeMetrics.Id,
                            nodeMetrics.RemoteNodeHealth.MachineName,
                            nodeMetrics.BufferSize);
                        SetNodeItemEvicted(nodeMetrics.Id,
                            nodeMetrics.RemoteNodeHealth.MachineName,
                            nodeMetrics.ItemsEvicted);
                    });

                _subscriptions.Push(clusterMetricSubscription);
                _subscriptions.Push(nodeMetricSubscription);
            }
        }

        private void SetClusterThroughput(Guid guid, string machineName, double throughput)
        {
            var tags = new MetricTags(new[] {"cluster_id", "machine_name"}, new[] {guid.ToString(), machineName});
            var histogram = new HistogramOptions
            {
                Context = "cluster",
                Tags = tags,
                Name = "Cluster Throughput",
                Reservoir = () => new DefaultAlgorithmRReservoir(),
                MeasurementUnit = Unit.None
            };

            _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(throughput));
        }

        private void SetNodeThroughput(Guid guid, string machineName, double throughput)
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

            _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(throughput));
        }

        private void SetNodeBufferSize(Guid guid, string machineName, int bufferSize)
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

            _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(bufferSize));
        }

        private void SetNodeItemsProcessed(Guid guid, string machineName, long itemsProcessed)
        {
            var tags = new MetricTags(new[] {"node_id", "machine_name"}, new[] {guid.ToString(), machineName});
            var gauge = new GaugeOptions
            {
                Context = "node",
                Tags = tags,
                Name = "Node Items Processed",
                MeasurementUnit = Unit.None
            };

            _metrics?.Measure.Gauge.SetValue(gauge, itemsProcessed);
        }

        private void SetNodeItemEvicted(Guid guid, string machineName, long itemsEvicted)
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

            _metrics?.Measure.Histogram.Update(histogram, itemsEvicted);
        }

        private void SetClusterPerformanceCounters(Guid guid, string machineName,
            IDictionary<string, float> performanceCounters)
        {
            if (!MetricsRegistry.ClusterPerformanceCounters.Any() ||
                MetricsRegistry.ClusterPerformanceCounters.All(counter => counter.Key != guid))
            {
                MetricsRegistry.ClusterPerformanceCounters.TryAdd(guid,
                    new ConcurrentHashSet<HistogramOptions>(
                        KeyBasedEqualityComparer<HistogramOptions>.Create(x => x.Name)));
            }

            var clusterPerformanceCounters =
                MetricsRegistry.ClusterPerformanceCounters.Single(cluster => cluster.Key == guid);
            foreach (var performanceCounter in performanceCounters)
            {
                if (clusterPerformanceCounters.Value.All(counter => counter.Name != performanceCounter.Key))
                {
                    var tags = new MetricTags(new[] {"cluster_id", "machine_name"},
                        new[] {guid.ToString(), machineName});
                    var histogram = new HistogramOptions
                    {
                        Context = "cluster",
                        Tags = tags,
                        Name = performanceCounter.Key,
                        Reservoir = () => new DefaultAlgorithmRReservoir(),
                        MeasurementUnit = Unit.None
                    };

                    clusterPerformanceCounters.Value.Add(histogram);
                    _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
                else
                {
                    var histogram =
                        clusterPerformanceCounters.Value.Single(counter =>
                            counter.Name == performanceCounter.Key);
                    _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
            }
        }

        private void SetNodePerformanceCounters(Guid guid, string machineName,
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
                    _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
                else
                {
                    var histogram =
                        nodePerformanceCounters.Value.Single(counter =>
                            counter.Name == performanceCounter.Key);
                    _metrics?.Measure.Histogram.Update(histogram, Convert.ToInt64(performanceCounter.Value));
                }
            }
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~MonitoringEngine()
        {
            Dispose(false);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose timer
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }
            }

            _disposed = true;
        }
    }
}
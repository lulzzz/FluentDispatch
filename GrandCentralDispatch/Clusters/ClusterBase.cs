using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime;
using System.Threading;
using LazyCache.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Cache;
using GrandCentralDispatch.Database;
using GrandCentralDispatch.Helpers;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Monitoring;
using GrandCentralDispatch.Options;
using System.Runtime.InteropServices;

namespace GrandCentralDispatch.Clusters
{
    public class ClusterBase
    {
        /// <summary>
        /// Tracker used by <see cref="Random"/>
        /// </summary>
        private static int _tracker;

        /// <summary>
        /// Subscription
        /// </summary>
        private readonly IDisposable _computeClusterHealthSubscription;

        /// <summary>
        /// <see cref="ICollection{TInput}"/>
        /// </summary>
        private readonly ICollection<PerformanceCounter> _performanceCounters;

        /// <summary>
        /// <see cref="ILogger"/>
        /// </summary>
        protected readonly ILogger Logger;

        /// <summary>
        /// <see cref="Options.ClusterOptions"/>
        /// </summary>
        protected readonly ClusterOptions ClusterOptions;

        /// <summary>
        /// Use to generate a true random based on CPU ticks
        /// </summary>
        protected readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() =>
        {
            var seed = (int) (Environment.TickCount & 0xFFFFFF00 | (byte) (Interlocked.Increment(ref _tracker) % 255));
            var random = new Random(seed);
            return random;
        });

        /// <summary>
        /// Node bulk progress
        /// </summary>
        protected readonly IProgress<double> Progress;

        /// <summary>
        /// Persistent cache to avoid dropped data on system crash
        /// </summary>
        protected readonly IAppCache PersistentCache;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        protected bool Disposed;

        /// <summary>
        /// <see cref="System.Threading.CancellationTokenSource"/>
        /// </summary>
        protected CancellationTokenSource CancellationTokenSource;

        protected ClusterBase(IProgress<double> progress,
            CancellationTokenSource cts,
            ClusterOptions clusterOptions,
            ILogger logger,
            ILoggerFactory loggerFactory)
        {
            Logger = logger;
            Logger.LogInformation(@"
#     ______                     __   ______           __             __   ____  _                  __       __  
#    / ____/________ _____  ____/ /  / ____/__  ____  / /__________ _/ /  / __ \(_)________  ____ _/ /______/ /_ 
#   / / __/ ___/ __ `/ __ \/ __  /  / /   / _ \/ __ \/ __/ ___/ __ `/ /  / / / / / ___/ __ \/ __ `/ __/ ___/ __ \
#  / /_/ / /  / /_/ / / / / /_/ /  / /___/  __/ / / / /_/ /  / /_/ / /  / /_/ / (__  ) /_/ / /_/ / /_/ /__/ / / /
#  \____/_/   \__,_/_/ /_/\__,_/   \____/\___/_/ /_/\__/_/   \__,_/_/  /_____/_/____/ .___/\__,_/\__/\___/_/ /_/ 
#                                                                                  /_/                           ");

            ClusterOptions = clusterOptions;
            Progress = progress ?? new Progress<double>();
            CancellationTokenSource = cts ?? new CancellationTokenSource();
            _performanceCounters = Helper.GetPerformanceCounters(clusterOptions.EnablePerformanceCounters);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                PersistentCache = new CachingService(new PersistentCacheProvider(
                    SQLiteDatabase.Connection.GetAwaiter().GetResult(),
                    new LazyCache.CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions
                    { SizeLimit = ClusterOptions.MaxItemsInPersistentCache }))),
                    loggerFactory));
            }
            else
            {
                PersistentCache = new CachingService(new DummyCacheProvider());
            }

            ClusterMetrics = new ClusterMetrics();
            var interval = new Subject<Unit>();
            var scheduler = Scheduler.Default;
            _computeClusterHealthSubscription = interval.Select(_ => Observable.Interval(TimeSpan.FromSeconds(5)))
                .Switch()
                .ObserveOn(scheduler).Subscribe(e => ComputeClusterHealth());

            interval.OnNext(Unit.Default);
        }

        /// <summary>
        /// Log cluster options on startup
        /// </summary>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="nodeCount">Number of nodes</param>
        protected void LogClusterOptions(CircuitBreakerOptions circuitBreakerOptions, int nodeCount)
        {
            var version = Helper.GetVersion();
            Logger.LogInformation($@"
Initializing cluster v{version}...
# Number of nodes: {nodeCount}
{(ClusterOptions.ExecuteRemotely ? $"# Executing remotely with configured hosts: {Environment.NewLine}{string.Join(Environment.NewLine, ClusterOptions.Hosts.Select(host => $"{host.MachineName}:{host.Port}"))}" : "# Executing locally")}
# Processing type: {Enum.GetName(typeof(ClusterProcessingType), ClusterOptions.ClusterProcessingType)}
# Queuing type: {Enum.GetName(typeof(NodeQueuingStrategy), ClusterOptions.NodeQueuingStrategy)}
# Node throttling: {ClusterOptions.NodeThrottling * ClusterOptions.Window.TotalMilliseconds / 1000} op/s
# Limiting CPU Usage: {ClusterOptions.LimitCpuUsage} %
# Number of retry attempts on failing items: {ClusterOptions.RetryAttempt}
# Evict items on throttling: {ClusterOptions.EvictItemsWhenNodesAreFull}
{(ClusterOptions.PersistenceEnabled ? $"# Persistence enabled with maximum items in memory: {ClusterOptions.MaxItemsInPersistentCache}" : "# Persistence disabled")}
# Aggressively GC collection: {ClusterOptions.AggressivelyGcCollect}
");

            Logger.LogInformation($@"
Setting cluster circuit breaker options...
# Duration of break: {circuitBreakerOptions.CircuitBreakerDurationOfBreak:g}
# Failure threshold: {circuitBreakerOptions.CircuitBreakerFailureThreshold}
# Minimum throughput: {circuitBreakerOptions.CircuitBreakerMinimumThroughput}
# Sampling duration: {circuitBreakerOptions.CircuitBreakerSamplingDuration}
");
        }

        /// <summary>
        /// Stop the processing for the cluster.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource?.Cancel(true);
        }

        /// <summary>
        /// Resume the processing for the cluster.
        /// </summary>
        public void Resume()
        {
            CancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// <see cref="ClusterMetrics"/>
        /// </summary>
        public ClusterMetrics ClusterMetrics { get; }

        /// <summary>
        /// Compute node health
        /// </summary>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        protected void ComputeNodeHealth(NodeMetrics nodeMetrics)
        {
            MonitoringEngine.SetNodePerformanceCounters(nodeMetrics.Id,
                nodeMetrics.RemoteNodeHealth.MachineName,
                nodeMetrics.RemoteNodeHealth.PerformanceCounters);
            MonitoringEngine.SetNodeThroughput(nodeMetrics.Id,
                nodeMetrics.RemoteNodeHealth.MachineName,
                nodeMetrics.CurrentThroughput);
            MonitoringEngine.SetNodeItemsProcessed(nodeMetrics.Id,
                nodeMetrics.RemoteNodeHealth.MachineName,
                nodeMetrics.TotalItemsProcessed);
            MonitoringEngine.SetNodeBufferSize(nodeMetrics.Id,
                nodeMetrics.RemoteNodeHealth.MachineName,
                nodeMetrics.BufferSize);
            MonitoringEngine.SetNodeItemEvicted(nodeMetrics.Id,
                nodeMetrics.RemoteNodeHealth.MachineName,
                nodeMetrics.ItemsEvicted);
        }

        /// <summary>
        /// Compute cluster health
        /// </summary>
        protected virtual void ComputeClusterHealth()
        {
            var clusterHealth = new ClusterHealth();
            foreach (var performanceCounter in _performanceCounters)
            {
                if (!clusterHealth.PerformanceCounters.ContainsKey(performanceCounter.CounterName) &&
                    !clusterHealth.PerformanceCounters.TryAdd(performanceCounter.CounterName,
                        performanceCounter.NextValue()))
                {
                    Logger.LogWarning($"Could not add performance counter {performanceCounter.CounterName}.");
                }
            }

            ClusterMetrics.Health = clusterHealth;
            MonitoringEngine.SetClusterThroughput(ClusterMetrics.CurrentThroughput);
            MonitoringEngine.SetClusterPerformanceCounters(ClusterMetrics.Health.PerformanceCounters);
            if (ClusterOptions.AggressivelyGcCollect)
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect();
            }
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~ClusterBase()
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
            if (Disposed)
                return;

            if (disposing)
            {
                _computeClusterHealthSubscription?.Dispose();
                CancellationTokenSource?.Cancel(true);
            }

            Disposed = true;
        }
    }
}
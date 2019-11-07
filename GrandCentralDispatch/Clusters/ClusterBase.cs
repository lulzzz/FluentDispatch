using System;
using System.Collections.Generic;
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
using GrandCentralDispatch.Options;
using System.Threading.Tasks;
using GrandCentralDispatch.Events;
using GrandCentralDispatch.PerformanceCounters;
using Polly;

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
        /// Monitor performance counters
        /// </summary>
        private readonly CounterMonitor _counterMonitor = new CounterMonitor();

        /// <summary>
        /// Performance counters
        /// </summary>
        private readonly IDictionary<string, double> _performanceCounters = new Dictionary<string, double>();

        /// <summary>
        /// Node bulk progress
        /// </summary>
        protected readonly IProgress<double> Progress;

        /// <summary>
        /// Persistent cache to avoid dropped data on system crash
        /// </summary>
        protected IAppCache PersistentCache { get; private set; }

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
            var policyResult = Policy.Handle<Exception>().RetryAsync().ExecuteAndCaptureAsync(async () =>
            {
                PersistentCache = new CachingService(new PersistentCacheProvider(
                    await SQLiteDatabase.Connection,
                    new LazyCache.CachingService(new MemoryCacheProvider(new MemoryCache(new MemoryCacheOptions
                        {SizeLimit = ClusterOptions.MaxItemsInPersistentCache}))),
                    loggerFactory));
            }).GetAwaiter().GetResult();
            if (policyResult.Outcome == OutcomeType.Failure)
            {
                PersistentCache = new CachingService(new DummyCacheProvider());
                Logger.LogError(
                    $"Error while creating persistence cache: {policyResult.FinalException?.Message ?? string.Empty}.");
            }

            ClusterMetrics = new ClusterMetrics(Guid.NewGuid());
            var interval = new Subject<Unit>();
            var scheduler = Scheduler.Default;
            _computeClusterHealthSubscription = interval.Select(_ => Observable.Interval(TimeSpan.FromSeconds(5)))
                .Switch()
                .ObserveOn(scheduler).Subscribe(e => ComputeClusterHealth());

            interval.OnNext(Unit.Default);
            _counterMonitor.CounterUpdate += OnCounterUpdate;
            var monitorTask = new Task(() =>
            {
                try
                {
                    _counterMonitor.Start(Logger);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Error while listening to counters: {ex.Message}");
                }
            });
            monitorTask.Start();
        }

        /// <summary>
        /// Update performance counters
        /// </summary>
        /// <param name="args"><see cref="CounterEventArgs"/></param>
        private void OnCounterUpdate(CounterEventArgs args)
        {
            if (!_performanceCounters.ContainsKey(args.DisplayName))
            {
                _performanceCounters.Add(args.DisplayName, args.Value);
            }
            else
            {
                _performanceCounters[args.DisplayName] = args.Value;
            }
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
        /// Event raised when a node metric is submitted
        /// </summary>
        public EventHandler<NodeMetricsEventArgs> NodeMetricSubmitted { get; set; }

        /// <summary>
        /// Event raised when a cluster metric is submitted
        /// </summary>
        public EventHandler<ClusterMetricsEventArgs> ClusterMetricSubmitted { get; set; }

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
            var nodeMetricSubmitted = NodeMetricSubmitted;
            nodeMetricSubmitted?.Invoke(this, new NodeMetricsEventArgs(nodeMetrics));
        }

        /// <summary>
        /// Compute cluster health
        /// </summary>
        protected virtual void ComputeClusterHealth()
        {
            var clusterHealth = new ClusterHealth();
            foreach (var performanceCounter in _performanceCounters.ToList())
            {
                if (!clusterHealth.PerformanceCounters.ContainsKey(performanceCounter.Key) &&
                    !clusterHealth.PerformanceCounters.TryAdd(performanceCounter.Key,
                        Convert.ToInt64(performanceCounter.Value)))
                {
                    Logger.LogWarning($"Could not add performance counter {performanceCounter.Key}.");
                }
            }

            ClusterMetrics.Health = clusterHealth;
            var clusterMetricSubmitted = ClusterMetricSubmitted;
            clusterMetricSubmitted?.Invoke(this, new ClusterMetricsEventArgs(ClusterMetrics));
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
                _counterMonitor.CounterUpdate -= OnCounterUpdate;
                _counterMonitor.Stop();
                _computeClusterHealthSubscription?.Dispose();
                CancellationTokenSource?.Cancel(true);
            }

            Disposed = true;
        }
    }
}
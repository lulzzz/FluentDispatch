using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Options;

namespace GrandCentralDispatch.Processors
{
    internal abstract class Processor
    {
        private readonly IDisposable _timerSubscription;
        private readonly IDisposable _evictedItemsSubscription;

        private long _itemsEvicted;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        protected bool Disposed;

        protected readonly ILogger Logger;
        protected readonly AsyncCircuitBreakerPolicy CircuitBreakerPolicy;
        protected readonly ClusterOptions ClusterOptions;
        protected readonly ISubject<int> EvictedItemsSubject;
        protected readonly ThreadPriority ThreadPriority;

        protected long _totalItemsProcessed;

        protected Processor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger)
        {
            Logger = logger;
            CircuitBreakerPolicy = circuitBreakerPolicy;
            ClusterOptions = clusterOptions;
            var interval = new Subject<Unit>();
            var scheduler = Scheduler.Default;
            _timerSubscription = interval.Select(_ => Observable.Interval(TimeSpan.FromSeconds(5)))
                .Switch()
                .Select(duration => Observable.FromAsync(ComputeMetrics))
                .Switch()
                .ObserveOn(scheduler).Subscribe();

            interval.OnNext(Unit.Default);
            switch (ClusterOptions.LimitCpuUsage)
            {
                case int limit when limit >= 60 && limit < 80:
                    ThreadPriority = ThreadPriority.AboveNormal;
                    break;
                case int limit when limit >= 40:
                    ThreadPriority = ThreadPriority.Normal;
                    break;
                case int limit when limit >= 20:
                    ThreadPriority = ThreadPriority.BelowNormal;
                    break;
                case int limit when limit >= 0:
                    ThreadPriority = ThreadPriority.Lowest;
                    break;
                default:
                    ThreadPriority = ThreadPriority.Normal;
                    break;
            }

            EvictedItemsSubject = new Subject<int>();
            _evictedItemsSubscription = EvictedItemsSubject.Subscribe(itemEvicted => { _itemsEvicted = itemEvicted; });
        }

        /// <summary>
        /// Destructor
        /// </summary>
        ~Processor()
        {
            Dispose(false);
        }

        /// <summary>
        /// Compute metrics
        /// </summary>
        /// <returns></returns>
        protected virtual async Task ComputeMetrics()
        {
            CpuUsage = await ComputeCpuUsageForProcess();
        }

        /// <summary>
        /// Compute CPU usage
        /// </summary>
        /// <returns>CPU usage</returns>
        private async Task<double> ComputeCpuUsageForProcess()
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;

            await Task.Delay(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;

            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return cpuUsageTotal * 100;
        }

        /// <summary>
        /// Indicates how many items have been submitted to the node
        /// </summary>
        /// <returns>Number of items</returns>
        protected long TotalItemsProcessed() => _totalItemsProcessed;

        /// <summary>
        /// Indicates how many items have been evicted
        /// </summary>
        /// <returns>Number of evicted items</returns>
        protected long ItemsEvicted() => _itemsEvicted;

        /// <summary>
        /// Indicates the current CPU usage
        /// </summary>
        protected double CpuUsage;

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
                _timerSubscription?.Dispose();
                _evictedItemsSubscription?.Dispose();
            }

            Disposed = true;
        }
    }
}
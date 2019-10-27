using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Options;

namespace GrandCentralDispatch.Processors.Unary
{
    /// <summary>
    /// Processor which dequeue messages sequentially.
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    internal abstract class UnarySequentialProcessor<TInput> : UnaryAbstractProcessor<TInput>
    {
        /// <summary>
        /// <see cref="UnarySequentialProcessor{TInput}"/>
        /// </summary>
        /// <param name="circuitBreakerPolicy"><see cref="CircuitBreakerPolicy"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected UnarySequentialProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            IProgress<double> progress,
            CancellationTokenSource cts,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            // We observe new items on an EventLoopScheduler which is backed by a dedicated background thread
            // Then we limit number of items to be processed by a sliding window
            // Then we process items asynchronously, with a circuit breaker policy
            ItemsSubjectSubscription = SynchronizedItemsSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Limit(() => ClusterOptions.NodeThrottling,
                    ClusterOptions.Window,
                    TaskPoolScheduler.Default,
                    ItemsBuffer,
                    ClusterOptions.EvictItemsWhenNodesAreFull,
                    EvictedItemsSubject,
                    Logger)
                .Select(batch =>
                {
                    return Observable.FromAsync(() =>
                    {
                        // ExecuteAndCaptureAsync let items to be "captured" in a way they never throw any exception, but are gracefully handle by a circuit breaker policy on non-success attempt
                        return CircuitBreakerPolicy.ExecuteAndCaptureAsync(
                            ct => Process(batch.ToList(), progress, ct), cts.Token);
                    });
                })
                // Dequeue sequentially
                .Concat()
                .Subscribe(unit =>
                    {
                        if (unit.Outcome == OutcomeType.Failure)
                        {
                            Logger.LogCritical(
                                unit.FinalException != null
                                    ? $"Could not process bulk: {unit.FinalException.Message}."
                                    : "An error has occured while processing the bulk.");
                        }
                    },
                    ex => Logger.LogError(ex.Message));

            // We observe new items on an EventLoopScheduler which is backed by a dedicated background thread
            // Then we limit number of items to be processed by a sliding window
            // Then we process items asynchronously, with a circuit breaker policy
            ItemsExecutorSubjectSubscription = SynchronizedItemsExecutorSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Limit(() => ClusterOptions.NodeThrottling,
                    ClusterOptions.Window,
                    TaskPoolScheduler.Default,
                    ItemsExecutorBuffer,
                    ClusterOptions.EvictItemsWhenNodesAreFull,
                    EvictedItemsSubject,
                    Logger)
                .Select(batch =>
                {
                    return Observable.FromAsync(() =>
                    {
                        // ExecuteAndCaptureAsync let items to be "captured" in a way they never throw any exception, but are gracefully handled by a circuit breaker policy on non-success attempt
                        return CircuitBreakerPolicy.ExecuteAndCaptureAsync(
                            ct => Process(batch.ToList(), progress, ct), cts.Token);
                    });
                })
                // Dequeue sequentially
                .Concat()
                .Subscribe(unit =>
                    {
                        if (unit.Outcome == OutcomeType.Failure)
                        {
                            Logger.LogCritical(
                                unit.FinalException != null
                                    ? $"Could not process bulk: {unit.FinalException.Message}."
                                    : "An error has occured while processing the bulk.");
                        }
                    },
                    ex => Logger.LogError(ex.Message));
        }
    }
}
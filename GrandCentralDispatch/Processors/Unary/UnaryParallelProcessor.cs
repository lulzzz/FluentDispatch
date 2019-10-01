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
    /// Processor which dequeue messages in parallel.
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    internal abstract class UnaryParallelProcessor<TInput> : UnaryAbstractProcessor<TInput>
    {
        /// <summary>
        /// <see cref="UnaryParallelProcessor{TInput}"/>
        /// </summary>
        /// <param name="circuitBreakerPolicy"><see cref="CircuitBreakerPolicy"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected UnaryParallelProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            IProgress<double> progress,
            CancellationTokenSource cts,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
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
                        // ExecuteAndCaptureAsync let items to be "captured" in a way they never throw any exception, but are gracefully handled by a circuit breaker policy on non-success attempt
                        return CircuitBreakerPolicy.ExecuteAndCaptureAsync(
                            ct => Process(batch.ToList(), progress, ct), cts.Token);
                    });
                })
                // Dequeue in parallel
                .Merge()
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
                // Dequeue in parallel
                .Merge()
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
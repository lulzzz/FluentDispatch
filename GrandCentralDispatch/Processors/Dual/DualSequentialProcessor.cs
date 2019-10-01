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

namespace GrandCentralDispatch.Processors.Dual
{
    /// <summary>
    /// Processor which dequeue messages sequentially.
    /// </summary>
    /// <typeparam name="TInput1"><see cref="TInput1"/></typeparam>
    /// <typeparam name="TInput2"><see cref="TInput2"/></typeparam>
    internal abstract class DualSequentialProcessor<TInput1, TInput2> : DualAbstractProcessor<TInput1, TInput2>
    {
        /// <summary>
        /// <see cref="DualSequentialProcessor{TInput1,TInput2}"/>
        /// </summary>
        /// <param name="circuitBreakerPolicy"><see cref="CircuitBreakerPolicy"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected DualSequentialProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            IProgress<double> progress,
            CancellationTokenSource cts,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            Items1SubjectSubscription = SynchronizedItems1Subject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Limit(() => ClusterOptions.NodeThrottling,
                    ClusterOptions.Window,
                    TaskPoolScheduler.Default,
                    Items1Buffer,
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

            Items2SubjectSubscription = SynchronizedItems2Subject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Limit(() => ClusterOptions.NodeThrottling,
                    ClusterOptions.Window,
                    TaskPoolScheduler.Default,
                    Items2Buffer,
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

            Items1ExecutorSubjectSubscription = SynchronizedItems1ExecutorSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Limit(() => ClusterOptions.NodeThrottling,
                    ClusterOptions.Window,
                    TaskPoolScheduler.Default,
                    Items1ExecutorBuffer,
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

            Items2ExecutorSubjectSubscription = SynchronizedItems2ExecutorSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Limit(() => ClusterOptions.NodeThrottling,
                    ClusterOptions.Window,
                    TaskPoolScheduler.Default,
                    Items2ExecutorBuffer,
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
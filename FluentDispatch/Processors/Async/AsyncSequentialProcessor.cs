using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using FluentDispatch.Extensions;
using FluentDispatch.Options;
using FluentDispatch.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FluentDispatch.Processors.Async
{
    /// <summary>
    /// Processor which dequeue messages sequentially.
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    /// <typeparam name="TAsync"><see cref="AsyncItem{TInput,TOutput}"/></typeparam>
    internal abstract class
        AsyncSequentialProcessor<TInput, TOutput, TAsync> : AsyncAbstractQueueProcessor<TInput, TOutput, TAsync>
        where TAsync : AsyncItem<TInput, TOutput>
    {
        /// <summary>
        /// <see cref="AsyncSequentialProcessor{TInput,TOutput,TAsync}"/>
        /// </summary>
        /// <param name="circuitBreakerPolicy"><see cref="CircuitBreakerPolicy"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected AsyncSequentialProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
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

        /// <summary>
        /// Push a new item to the queue.
        /// </summary>
        /// <param name="item"><see cref="AsyncPredicateItem{TInput,TOutput}"/></param>
        protected Task<TOutput> AddAsync(TAsync item)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            item.CancellationToken.Register(() => { item.TaskCompletionSource.TrySetCanceled(); });
            SynchronizedItemsSubject.OnNext(item);
            return item.TaskCompletionSource.Task;
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="AsyncPredicateItem{TInput,TOutput}"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<TAsync> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);
    }
}
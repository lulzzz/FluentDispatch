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
using System.Threading.Tasks;
using GrandCentralDispatch.Models;
using System.Collections.Generic;

namespace GrandCentralDispatch.Processors.Async
{
    /// <summary>
    /// Processor which dequeue messages in parallel.
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    internal abstract class AsyncParallelProcessor<TInput, TOutput, T> : AsyncAbstractQueueProcessor<TInput, TOutput, T> where T : AsyncItem<TInput, TOutput>
    {
        /// <summary>
        /// <see cref="AsyncParallelProcessor{TInput,TOutput,T}"/>
        /// </summary>
        /// <param name="circuitBreakerPolicy"><see cref="CircuitBreakerPolicy"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected AsyncParallelProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
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
                { IsBackground = true, Priority = ThreadPriority }))
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
        }

        /// <summary>
        /// Push a new item to the queue.
        /// </summary>
        /// <param name="item"><see cref="AsyncPredicateItem{TInput,TOutput}"/></param>
        public Task<TOutput> AddAsync(T item)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItemsSubject.OnNext(item);
            item.CancellationToken.Register(() =>
            {
                item.TaskCompletionSource.TrySetCanceled();
            });

            return item.TaskCompletionSource.Task;
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<T> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);
    }
}
using System;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Options;
using GrandCentralDispatch.Models;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Processors.Async
{
    /// <summary>
    /// Processor which executes asynchronously incoming items.
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    /// <typeparam name="TAsync"><see cref="TAsync"/></typeparam>
    internal abstract class AsyncProcessor<TInput, TOutput, TAsync> : AsyncAbstractProcessor<TInput, TOutput, TAsync>
        where TAsync : AsyncItem<TInput, TOutput>
    {
        /// <summary>
        /// <see cref="AsyncProcessor{TInput,TOutput,TAsync}"/>
        /// </summary>
        /// <param name="circuitBreakerPolicy"><see cref="CircuitBreakerPolicy"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        protected AsyncProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            CancellationTokenSource cts,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            // We observe new items on an EventLoopScheduler which is backed by a dedicated background thread
            // Then we process items asynchronously, with a circuit breaker policy
            ItemsSubjectSubscription = SynchronizedItemsSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)
                    {IsBackground = true, Priority = ThreadPriority}))
                .Select(item =>
                {
                    return Observable.FromAsync(() =>
                    {
                        // ExecuteAndCaptureAsync let items to be "captured" in a way they never throw any exception, but are gracefully handled by a circuit breaker policy on non-success attempt
                        return CircuitBreakerPolicy.ExecuteAndCaptureAsync(
                            ct => Process(item, ct), cts.Token);
                    });
                })
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
        /// Process an incoming item
        /// </summary>
        /// <param name="item"><see cref="AsyncItem{TInput,TOutput}"/></param>
        protected Task<TOutput> ProcessAsync(TAsync item)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            item.CancellationToken.Register(() => { item.TaskCompletionSource.TrySetCanceled(); });
            SynchronizedItemsSubject.OnNext(item);
            return item.TaskCompletionSource.Task;
        }

        /// <summary>
        /// The processor.
        /// </summary>
        /// <param name="item"><see cref="AsyncItem{TInput,TOutput}"/> to process</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(TAsync item, CancellationToken cancellationToken);
    }
}
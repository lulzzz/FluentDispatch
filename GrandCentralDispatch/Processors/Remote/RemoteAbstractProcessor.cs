using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Options;
using GrandCentralDispatch.Models;
using System;
using System.Reactive.Subjects;

namespace GrandCentralDispatch.Processors.Remote
{
    internal abstract class RemoteAbstractProcessor<TInput, TOutput> : Processor, IRemoteProcessor<TInput, TOutput>
    {
        protected readonly ISubject<RemoteItem<TInput, TOutput>> SynchronizedItemsSubject;

        protected IDisposable ItemsSubjectSubscription;

        protected RemoteAbstractProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            ISubject<RemoteItem<TInput, TOutput>> itemsSubject = new Subject<RemoteItem<TInput, TOutput>>();

            // _synchronized is a thread-safe object in which we can push items concurrently
            SynchronizedItemsSubject = Subject.Synchronize(itemsSubject);
        }

        /// <summary>
        /// Process an incoming item
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="item"><see cref="RemoteItem{TInput,TOutput}"/></param>
        public Task<TOutput> ProcessAsync(RemoteItem<TInput, TOutput> item)
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
        /// <param name="item"><see cref="TInput"/> to process</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(RemoteItem<TInput, TOutput> item, CancellationToken cancellationToken);

        /// <summary>
        /// Dispose timer
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (Disposed)
                return;

            if (disposing)
            {
                ItemsSubjectSubscription?.Dispose();
            }

            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Options;

namespace GrandCentralDispatch.Processors.Async
{
    internal abstract class AsyncAbstractProcessor<TInput, TOutput> : Processor, IAsyncProcessor<TInput, TOutput>
    {
        protected readonly ISubject<AsyncItem<TInput, TOutput>> SynchronizedItemsSubject;

        protected readonly ConcurrentQueue<AsyncItem<TInput, TOutput>> ItemsBuffer =
            new ConcurrentQueue<AsyncItem<TInput, TOutput>>();

        protected IDisposable ItemsSubjectSubscription;

        protected AsyncAbstractProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            ISubject<AsyncItem<TInput, TOutput>> itemsSubject = new Subject<AsyncItem<TInput, TOutput>>();

            // _synchronized is a thread-safe object in which we can push items concurrently
            SynchronizedItemsSubject = Subject.Synchronize(itemsSubject);
        }

        /// <summary>
        /// Push a new item to the queue.
        /// </summary>
        /// <param name="item"><see cref="AsyncItem{TInput,TOutput}"/></param>
        public Task<TOutput> AddAsync(AsyncItem<TInput, TOutput> item)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItemsSubject.OnNext(item);
            return item.TaskCompletionSource.Task;
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<AsyncItem<TInput, TOutput>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// Indicates if current processor is full.
        /// </summary>
        /// <returns>True if full</returns>
        protected bool IsFull() => ItemsBuffer.Count >= ClusterOptions.NodeThrottling;

        /// <summary>
        /// Get current buffer size
        /// </summary>
        /// <returns>Buffer size</returns>
        protected int GetBufferSize() => ItemsBuffer.Count;

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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Options;

namespace GrandCentralDispatch.Processors.Unary
{
    /// <summary>
    /// Processor
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    internal abstract class UnaryAbstractProcessor<TInput> : Processor, IUnaryProcessor<TInput>
    {
        protected readonly ISubject<TInput> SynchronizedItemsSubject;
        protected readonly ISubject<Func<TInput>> SynchronizedItemsExecutorSubject;

        protected readonly ConcurrentQueue<TInput> ItemsBuffer = new ConcurrentQueue<TInput>();

        protected readonly ConcurrentQueue<Func<TInput>> ItemsExecutorBuffer =
            new ConcurrentQueue<Func<TInput>>();

        protected IDisposable ItemsSubjectSubscription;
        protected IDisposable ItemsExecutorSubjectSubscription;

        protected UnaryAbstractProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            ISubject<TInput> itemsSubject = new Subject<TInput>();
            ISubject<Func<TInput>> itemsExecutorSubject = new Subject<Func<TInput>>();

            // _synchronized is a thread-safe object in which we can push items concurrently
            SynchronizedItemsSubject = Subject.Synchronize(itemsSubject);
            SynchronizedItemsExecutorSubject = Subject.Synchronize(itemsExecutorSubject);
        }

        /// <summary>
        /// Push a new item to the queue.
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <remarks>This call does not block the calling thread</remarks>
        public void Add(TInput item)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItemsSubject.OnNext(item);
        }

        /// <summary>
        /// Push a new item to the queue.
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <remarks>This call does not block the calling thread</remarks>
        public void Add(Func<TInput> item)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItemsExecutorSubject.OnNext(item);
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<TInput> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<Func<TInput>> bulk, IProgress<double> progress,
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
                ItemsExecutorSubjectSubscription?.Dispose();
            }

            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
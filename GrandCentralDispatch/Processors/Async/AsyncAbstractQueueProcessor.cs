using System;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Options;
using System.Collections.Concurrent;

namespace GrandCentralDispatch.Processors.Async
{
    internal abstract class AsyncAbstractQueueProcessor<TInput, TOutput, T> : Processor where T : AsyncItem<TInput, TOutput>
    {
        protected readonly ISubject<T> SynchronizedItemsSubject;

        protected readonly ConcurrentQueue<T> ItemsBuffer =
            new ConcurrentQueue<T>();

        protected IDisposable ItemsSubjectSubscription;

        protected AsyncAbstractQueueProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            ISubject<T> itemsSubject = new Subject<T>();

            // SynchronizedItemsQueueSubject is a thread-safe object in which we can push items concurrently
            SynchronizedItemsSubject = Subject.Synchronize(itemsSubject);
        }

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

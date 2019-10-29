using System;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Options;

namespace GrandCentralDispatch.Processors.Async
{
    internal abstract class AsyncAbstractProcessor<TInput, TOutput, TAsync> : Processor
        where TAsync : AsyncItem<TInput, TOutput>
    {
        protected readonly ISubject<TAsync> SynchronizedItemsSubject;

        protected IDisposable ItemsSubjectSubscription;

        protected AsyncAbstractProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            ISubject<TAsync> itemsSubject = new Subject<TAsync>();

            // SynchronizedItemsSubject is a thread-safe object in which we can push items concurrently
            SynchronizedItemsSubject = Subject.Synchronize(itemsSubject);
        }

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
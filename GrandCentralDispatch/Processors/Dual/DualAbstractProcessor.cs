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

namespace GrandCentralDispatch.Processors.Dual
{
    /// <summary>
    /// Processor.
    /// </summary>
    /// <typeparam name="TInput1"><see cref="TInput1"/></typeparam>
    /// <typeparam name="TInput2"><see cref="TInput2"/></typeparam>
    internal abstract class DualAbstractProcessor<TInput1, TInput2> : Processor, IDualProcessor<TInput1, TInput2>
    {
        protected readonly ISubject<LinkedItem<TInput1>> SynchronizedItems1Subject;
        protected readonly ISubject<LinkedItem<TInput2>> SynchronizedItems2Subject;
        protected readonly ISubject<LinkedFuncItem<TInput1>> SynchronizedItems1ExecutorSubject;
        protected readonly ISubject<LinkedFuncItem<TInput2>> SynchronizedItems2ExecutorSubject;

        protected readonly ConcurrentQueue<LinkedItem<TInput1>> Items1Buffer =
            new ConcurrentQueue<LinkedItem<TInput1>>();

        protected readonly ConcurrentQueue<LinkedFuncItem<TInput1>> Items1ExecutorBuffer =
            new ConcurrentQueue<LinkedFuncItem<TInput1>>();

        protected readonly ConcurrentQueue<LinkedItem<TInput2>> Items2Buffer =
            new ConcurrentQueue<LinkedItem<TInput2>>();

        protected readonly ConcurrentQueue<LinkedFuncItem<TInput2>> Items2ExecutorBuffer =
            new ConcurrentQueue<LinkedFuncItem<TInput2>>();

        protected IDisposable Items1SubjectSubscription;
        protected IDisposable Items1ExecutorSubjectSubscription;
        protected IDisposable Items2SubjectSubscription;
        protected IDisposable Items2ExecutorSubjectSubscription;

        protected DualAbstractProcessor(AsyncCircuitBreakerPolicy circuitBreakerPolicy,
            ClusterOptions clusterOptions,
            ILogger logger) : base(circuitBreakerPolicy, clusterOptions, logger)
        {
            ISubject<LinkedItem<TInput1>> items1Subject = new Subject<LinkedItem<TInput1>>();
            ISubject<LinkedFuncItem<TInput1>> items1ExecutorSubject = new Subject<LinkedFuncItem<TInput1>>();
            ISubject<LinkedItem<TInput2>> items2Subject = new Subject<LinkedItem<TInput2>>();
            ISubject<LinkedFuncItem<TInput2>> items2ExecutorSubject = new Subject<LinkedFuncItem<TInput2>>();

            // SynchronizedItems are thread-safe objects in which we can push items concurrently
            SynchronizedItems1Subject = Subject.Synchronize(items1Subject);
            SynchronizedItems1ExecutorSubject = Subject.Synchronize(items1ExecutorSubject);
            SynchronizedItems2Subject = Subject.Synchronize(items2Subject);
            SynchronizedItems2ExecutorSubject = Subject.Synchronize(items2ExecutorSubject);
        }

        /// <summary>
        /// Push a new element to the queue.
        /// </summary>
        /// <param name="item1"><see cref="TInput1"/></param>
        /// <remarks>This call does not block the calling thread</remarks>
        public void Add(LinkedItem<TInput1> item1)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItems1Subject.OnNext(item1);
        }

        /// <summary>
        /// Push a new element to the queue.
        /// </summary>
        /// <param name="item2"><see cref="TInput2"/></param>
        /// <remarks>This call does not block the calling thread</remarks>
        public void Add(LinkedItem<TInput2> item2)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItems2Subject.OnNext(item2);
        }

        /// <summary>
        /// Push a new element to the queue.
        /// </summary>
        /// <param name="item1"><see cref="TInput1"/></param>
        /// <remarks>This call does not block the calling thread</remarks>
        public void Add(LinkedFuncItem<TInput1> item1)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItems1ExecutorSubject.OnNext(item1);
        }

        /// <summary>
        /// Push a new element to the queue.
        /// </summary>
        /// <param name="item2"><see cref="TInput2"/></param>
        /// <remarks>This call does not block the calling thread</remarks>
        public void Add(LinkedFuncItem<TInput2> item2)
        {
            Interlocked.Increment(ref _totalItemsProcessed);
            SynchronizedItems2ExecutorSubject.OnNext(item2);
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<LinkedItem<TInput1>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<LinkedItem<TInput2>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<LinkedFuncItem<TInput1>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected abstract Task Process(IList<LinkedFuncItem<TInput2>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken);

        /// <summary>
        /// Indicates if current processor is full.
        /// </summary>
        /// <returns>True if full</returns>
        protected bool IsFull() =>
            Items1Buffer.Count + Items2Buffer.Count + Items1ExecutorBuffer.Count + Items2ExecutorBuffer.Count >=
            ClusterOptions.NodeThrottling;

        /// <summary>
        /// Get current buffer size
        /// </summary>
        /// <returns>Buffer size</returns>
        protected int GetBufferSize() => Items1Buffer.Count + Items2Buffer.Count + Items1ExecutorBuffer.Count +
                                         Items2ExecutorBuffer.Count;

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
                Items1SubjectSubscription?.Dispose();
                Items2SubjectSubscription?.Dispose();
                Items1ExecutorSubjectSubscription?.Dispose();
                Items2ExecutorSubjectSubscription?.Dispose();
            }

            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
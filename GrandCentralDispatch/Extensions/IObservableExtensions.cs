using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.Extensions
{
    internal static class IObservableExtensions
    {
        /// <summary>
        /// Actually it acts as a dynamic sliding window which evicts items from the source once a limit has been reached (using count()).
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <param name="source"><see cref="IObservable{T}"/></param>
        /// <param name="throttling">How many items have to be processed per <see cref="TimeSpan"/> unit of time</param>
        /// <param name="window">Window to process <see cref="throttling"/> items</param>
        /// <param name="scheduler"><see cref="IScheduler"/></param>
        /// <param name="buffer">The buffer which holds temporarly items</param>
        /// <param name="evictsItems">Evicts items when buffer is full</param>
        /// <param name="evictSubject">Notify when items are evicted</param>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <returns><see cref="IObservable{T}"/></returns>
        public static IObservable<IEnumerable<TSource>> Limit<TSource>(
            this IObservable<TSource> source,
            Func<int> throttling,
            TimeSpan window,
            IScheduler scheduler,
            ConcurrentQueue<TSource> buffer,
            bool evictsItems,
            ISubject<int> evictSubject,
            ILogger logger)
        {
            return Observable.Create<IEnumerable<TSource>>(
                observer =>
                {
                    var guard = new object();
                    var evictedItems = new ConcurrentQueue<TSource>();
                    var sourceSub = source
                        .Subscribe(x =>
                            {
                                // We lock the buffer so that we wait for the enqueuing process to be finished before allowing access to it
                                lock (guard)
                                {
                                    buffer.Enqueue(x);
                                }
                            },
                            observer.OnError,
                            observer.OnCompleted);

                    var timer = Observable.Interval(window, scheduler)
                        .Subscribe(_ =>
                        {
                            var bulk = new List<TSource>();
                            // We lock the buffer so that we wait for the dequeuing process to be finished before allowing access to it
                            lock (guard)
                            {
                                if (buffer.Count > throttling())
                                {
                                    var evictedCount = buffer.Count - throttling();
                                    evictSubject.OnNext(evictedCount);
                                    logger.LogWarning(
                                        $"Buffer is full, evicting {evictedCount} items...");
                                }
                                else if (!evictsItems && buffer.Count < throttling() && evictedItems.Any())
                                {
                                    logger.LogInformation("Trying to add previous evicted items...");
                                    while (evictedItems.Any() && buffer.Count < throttling())
                                    {
                                        if (evictedItems.TryDequeue(out var element))
                                        {
                                            buffer.Enqueue(element);
                                        }
                                    }
                                }

                                // Dequeue when number of items exceed threshold
                                while (!evictsItems && buffer.Any() && buffer.Count > throttling())
                                {
                                    if (buffer.TryDequeue(out var evictedItem))
                                    {
                                        evictedItems.Enqueue(evictedItem);
                                    }
                                }

                                // Enqueue items to the output
                                while (buffer.Any() && bulk.Count <= throttling())
                                {
                                    if (buffer.TryDequeue(out var element))
                                    {
                                        bulk.Add(element);
                                    }
                                }
                            }

                            // Produce a new bulk
                            observer.OnNext(bulk.AsEnumerable());
                        });

                    return new CompositeDisposable(sourceSub, timer);
                });
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Grpc.Core;
using MagicOnion.Client;
using Microsoft.Extensions.Logging;
using Polly;
using GrandCentralDispatch.Cache;
using GrandCentralDispatch.Extensions;
using GrandCentralDispatch.Hubs.Hub;
using GrandCentralDispatch.Hubs.Receiver;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Options;
using GrandCentralDispatch.Processors.Dual;
using GrandCentralDispatch.Remote;

namespace GrandCentralDispatch.Nodes.Dual
{
    /// <summary>
    /// Node which process items sequentially.
    /// </summary>
    /// <typeparam name="TInput1">Item to be processed</typeparam>
    /// <typeparam name="TInput2">Item to be processed</typeparam>
    /// <typeparam name="TOutput1">Item to be processed</typeparam>
    /// <typeparam name="TOutput2">Item to be processed</typeparam>
    internal sealed class DualSequentialDispatcherNode<TInput1, TInput2, TOutput1, TOutput2> :
        DualSequentialProcessor<TInput1, TInput2>,
        IDualDispatcherNode<TInput1, TInput2>
    {
        /// <summary>
        /// <see cref="ILogger"/>
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The <see cref="Task"/> to be applied to an item <see cref="TInput1"/> which produces a <see cref="TOutput1"/>.
        /// </summary>
        private readonly Func<TInput1, NodeMetrics, CancellationToken, Task<TOutput1>> _item1Resolver;

        /// <summary>
        /// The <see cref="Task"/> to be applied to an item <see cref="TInput2"/> which produces a <see cref="TOutput2"/>.
        /// </summary>
        private readonly Func<TInput2, NodeMetrics, CancellationToken, Task<TOutput2>> _item2Resolver;

        /// <summary>
        /// <see cref="IRemoteContract{TInput1}"/>
        /// </summary>
        private readonly IRemoteContract<TOutput1, TOutput2> _remoteContract;

        /// <summary>
        /// <see cref="IRemoteContract{TInput1}"/>
        /// </summary>
        private readonly IItem1RemotePartialContract<TInput1, TOutput1> _item1RemoteContract;

        /// <summary>
        /// <see cref="IRemoteContract{TInput1}"/>
        /// </summary>
        private readonly IItem2RemotePartialContract<TInput2, TOutput2> _item2RemoteContract;

        /// <summary>
        /// Synchronized subject
        /// </summary>
        private readonly ISubject<LinkedItem<TInput1>> _item1SynchronizedDispatcherSubject;

        /// <summary>
        /// Subscription
        /// </summary>
        private readonly IDisposable _item1SynchronizedDispatcherSubjectSubscription;

        /// <summary>
        /// Subject which dispatch load
        /// </summary>
        private readonly ISubject<LinkedItem<TInput2>> _item2SynchronizedDispatcherSubject;

        /// <summary>
        /// Subscription
        /// </summary>
        private readonly IDisposable _item2SynchronizedDispatcherSubjectSubscription;

        /// <summary>
        /// The process which transform <see cref="TInput1"/> to <see cref="TOutput1"/>
        /// </summary>
        private readonly
            TransformBlock<Tuple<Guid, TOutput1, CancellationTokenSource>, KeyValuePair<Guid, CancellationTokenSource>>
            _item1Source;

        /// <summary>
        /// The process which transform <see cref="TInput2"/> to <see cref="TOutput2"/>
        /// </summary>
        private readonly
            TransformBlock<Tuple<Guid, TOutput2, CancellationTokenSource>, KeyValuePair<Guid, CancellationTokenSource>>
            _item2Source;

        /// <summary>
        /// <see cref="ClusterOptions"/>
        /// </summary>
        private readonly ClusterOptions _clusterOptions;

        /// <summary>
        /// <see cref="INodeHub"/>
        /// </summary>
        private readonly INodeHub _nodeHub;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private readonly IDisposable _remoteNodeHealthSubscription;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Number of currently processed items for the current bulk
        /// </summary>
        private long _processedItems;

        /// <summary>
        /// Number of currently processed item executors for the current bulk
        /// </summary>
        private long _executorProcessedItems;

        /// <summary>
        /// Previously processed number of items
        /// </summary>
        private long _previouslyProcessedItems;

        /// <summary>
        /// Previously processed number of item executors
        /// </summary>
        private long _previouslyExecutorProcessedItems;

        /// <summary>
        /// <see cref="DualSequentialDispatcherNode{TInput1,TInput2,TOutput1,TOutput2}"/>
        /// </summary>
        /// <param name="persistentCache">Persistent cache to avoid dropped data on system crash</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="host"><see cref="Host"/></param>
        public DualSequentialDispatcherNode(
            IAppCache persistentCache,
            IProgress<double> progress,
            CancellationTokenSource cts,
            CircuitBreakerOptions circuitBreakerOptions,
            ClusterOptions clusterOptions,
            ILogger logger,
            Host host = null) : this(persistentCache, null, null, null, progress, cts, circuitBreakerOptions,
            clusterOptions, logger,
            host)
        {

        }

        /// <summary>
        /// <see cref="DualSequentialDispatcherNode{TInput1,TInput2,TOutput1,TOutput2}"/>
        /// </summary>
        /// <param name="persistentCache">Persistent cache to avoid dropped data on system crash</param>
        /// <param name="item1Resolver">The <see cref="Task"/> to be applied to an item</param>
        /// <param name="item2Resolver">The <see cref="Task"/> to be applied to an item</param>
        /// <param name="resolver">The <see cref="Task"/> to be applied to an item</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="host"><see cref="Host"/></param>
        public DualSequentialDispatcherNode(
            IAppCache persistentCache,
            Func<TInput1, NodeMetrics, CancellationToken, Task<TOutput1>> item1Resolver,
            Func<TInput2, NodeMetrics, CancellationToken, Task<TOutput2>> item2Resolver,
            Func<TOutput1, TOutput2, NodeMetrics, CancellationToken, Task> resolver,
            IProgress<double> progress,
            CancellationTokenSource cts,
            CircuitBreakerOptions circuitBreakerOptions,
            ClusterOptions clusterOptions,
            ILogger logger,
            Host host = null) : base(
            Policy.Handle<Exception>()
                .AdvancedCircuitBreakerAsync(circuitBreakerOptions.CircuitBreakerFailureThreshold,
                    circuitBreakerOptions.CircuitBreakerSamplingDuration,
                    circuitBreakerOptions.CircuitBreakerMinimumThroughput,
                    circuitBreakerOptions.CircuitBreakerDurationOfBreak,
                    onBreak: (ex, timespan, context) =>
                    {
                        logger.LogError(
                            $"Batch processor breaker: Breaking the circuit for {timespan.TotalMilliseconds}ms due to {ex.Message}.");
                    },
                    onReset: context =>
                    {
                        logger.LogInformation(
                            "Batch processor breaker: Succeeded, closed the circuit.");
                    },
                    onHalfOpen: () =>
                    {
                        logger.LogWarning(
                            "Batch processor breaker: Half-open, next call is a trial.");
                    }), clusterOptions, progress, cts, logger)
        {
            var persistentCache1 = persistentCache;
            _logger = logger;
            _item1Resolver = item1Resolver;
            _item2Resolver = item2Resolver;
            _clusterOptions = clusterOptions;

            ISubject<LinkedItem<TInput1>> item1DispatcherSubject = new Subject<LinkedItem<TInput1>>();
            _item1SynchronizedDispatcherSubject = Subject.Synchronize(item1DispatcherSubject);
            _item1SynchronizedDispatcherSubjectSubscription = _item1SynchronizedDispatcherSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)))
                .Select(item =>
                {
                    return Observable.FromAsync(() => persistentCache1.AddItem1Async(item.Key.ToString(), item.Entity,
                        item.CancellationTokenSource.Token));
                })
                .Merge()
                .Subscribe();

            ISubject<LinkedItem<TInput2>> item2DispatcherSubject = new Subject<LinkedItem<TInput2>>();
            _item2SynchronizedDispatcherSubject = Subject.Synchronize(item2DispatcherSubject);
            _item2SynchronizedDispatcherSubjectSubscription = _item2SynchronizedDispatcherSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)))
                .Select(item =>
                {
                    return Observable.FromAsync(() => persistentCache1.AddItem2Async(item.Key.ToString(), item.Entity,
                        item.CancellationTokenSource.Token));
                })
                .Merge()
                .Subscribe();

            if (_clusterOptions.ExecuteRemotely && host != null)
            {
                var channel = new Channel(host.MachineName, host.Port,
                    ChannelCredentials.Insecure);
                _remoteContract = MagicOnionClient.Create<IRemoteContract<TOutput1, TOutput2>>(channel);
                _item1RemoteContract = MagicOnionClient.Create<IItem1RemotePartialContract<TInput1, TOutput1>>(channel);
                _item2RemoteContract = MagicOnionClient.Create<IItem2RemotePartialContract<TInput2, TOutput2>>(channel);
                IRemoteNodeSubject nodeReceiver = new NodeReceiver(_logger);
                _remoteNodeHealthSubscription =
                    nodeReceiver.RemoteNodeHealthSubject.Subscribe(remoteNodeHealth =>
                    {
                        NodeMetrics.RemoteNodeHealth = remoteNodeHealth;
                    });
                _nodeHub = StreamingHubClient.Connect<INodeHub, INodeReceiver>(channel, (INodeReceiver) nodeReceiver);
            }

            NodeMetrics = new NodeMetrics(Guid.NewGuid());

            var item1ProcessSource = new ConcurrentDictionary<Guid, TOutput1>();
            var item2ProcessSource = new ConcurrentDictionary<Guid, TOutput2>();
            var joinBlock =
                new JoinBlock<KeyValuePair<Guid, CancellationTokenSource>, KeyValuePair<Guid, CancellationTokenSource>>(
                    new GroupingDataflowBlockOptions {Greedy = false});
            _item1Source =
                new TransformBlock<Tuple<Guid, TOutput1, CancellationTokenSource>,
                    KeyValuePair<Guid, CancellationTokenSource>
                >(source =>
                {
                    if (!item1ProcessSource.ContainsKey(source.Item1) &&
                        !item1ProcessSource.TryAdd(source.Item1, source.Item2))
                    {
                        _logger.LogError(
                            $"Could not add item of type {source.Item2.GetType().ToString()} and key {source.Item1.ToString()} to the buffer.");
                    }

                    return new KeyValuePair<Guid, CancellationTokenSource>(source.Item1, source.Item3);
                });
            _item2Source =
                new TransformBlock<Tuple<Guid, TOutput2, CancellationTokenSource>,
                    KeyValuePair<Guid, CancellationTokenSource>
                >(
                    source =>
                    {
                        if (!item2ProcessSource.ContainsKey(source.Item1) &&
                            !item2ProcessSource.TryAdd(source.Item1, source.Item2))
                        {
                            _logger.LogError(
                                $"Could not add item of type {source.Item2.GetType().ToString()} and key {source.Item1.ToString()} to the buffer.");
                        }

                        return new KeyValuePair<Guid, CancellationTokenSource>(source.Item1, source.Item3);
                    });

            var processBlock = new ActionBlock<Tuple<KeyValuePair<Guid, CancellationTokenSource>,
                KeyValuePair<Guid, CancellationTokenSource>>>(
                async combined =>
                {
                    var policy = Policy
                        .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                        .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                            retryAttempt =>
                                TimeSpan.FromSeconds(Math.Pow(10, retryAttempt)),
                            (exception, sleepDuration, retry, context) =>
                            {
                                if (retry >= _clusterOptions.RetryAttempt)
                                {
                                    _logger.LogError(
                                        $"Could not process item after {retry} retry times: {exception.Message}");
                                }
                            });

                    var policyResult = await policy.ExecuteAndCaptureAsync(async ct =>
                    {
                        try
                        {
                            if (CpuUsage > _clusterOptions.LimitCpuUsage)
                            {
                                var suspensionTime = (CpuUsage - _clusterOptions.LimitCpuUsage) / CpuUsage * 100;
                                await Task.Delay((int) suspensionTime, ct);
                            }

                            if (item1ProcessSource.ContainsKey(combined.Item1.Key) &&
                                item2ProcessSource.ContainsKey(combined.Item2.Key) &&
                                item1ProcessSource.TryGetValue(combined.Item1.Key, out var item1) &&
                                item2ProcessSource.TryGetValue(combined.Item2.Key, out var item2))
                            {
                                if (_clusterOptions.ExecuteRemotely)
                                {
                                    await _remoteContract.ProcessRemotely(item1, item2, NodeMetrics);
                                }
                                else
                                {
                                    await resolver(item1, item2, NodeMetrics, ct);
                                }

                                combined.Item1.Value.Cancel();
                                combined.Item2.Value.Cancel();
                            }
                        }
                        catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                        {
                            _logger.LogTrace("The item process has been cancelled.");
                        }
                    }, cts.Token).ConfigureAwait(false);

                    if (policyResult.Outcome == OutcomeType.Failure)
                    {
                        _logger.LogCritical(
                            policyResult.FinalException != null
                                ? $"Could not process item: {policyResult.FinalException.Message}."
                                : "An error has occured while processing the item.");
                    }

                    if (!item1ProcessSource.TryRemove(combined.Item1.Key, out _))
                    {
                        _logger.LogWarning(
                            $"Could not remove item of key {combined.Item1.ToString()} from the buffer.");
                    }

                    if (!item2ProcessSource.TryRemove(combined.Item2.Key, out _))
                    {
                        _logger.LogWarning(
                            $"Could not remove item of key {combined.Item2.ToString()} from the buffer.");
                    }
                });

            var options = new DataflowLinkOptions
            {
                PropagateCompletion = true
            };

            _item1Source.LinkTo(joinBlock.Target1, options);
            _item2Source.LinkTo(joinBlock.Target2, options);
            joinBlock.LinkTo(processBlock, options);
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(IList<LinkedItem<TInput1>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _previouslyProcessedItems = Interlocked.Exchange(ref _processedItems, 0L);
            if (!bulk.Any())
            {
                await ComputeStatistics();
                return;
            }

            foreach (var item in bulk)
            {
                var policy = Policy
                    .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                    .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(10, retryAttempt)),
                        (exception, sleepDuration, retry, context) =>
                        {
                            if (retry >= _clusterOptions.RetryAttempt)
                            {
                                _logger.LogError(
                                    $"Could not process item after {retry} retry times: {exception.Message}");
                            }
                        });

                var policyResult = await policy.ExecuteAndCaptureAsync(async ct =>
                {
                    try
                    {
                        if (CpuUsage > _clusterOptions.LimitCpuUsage)
                        {
                            var suspensionTime = (CpuUsage - _clusterOptions.LimitCpuUsage) / CpuUsage * 100;
                            await Task.Delay((int) suspensionTime, ct);
                        }

                        if (_clusterOptions.PersistenceEnabled)
                        {
                            _item1SynchronizedDispatcherSubject.OnNext(item);
                        }

                        if (_clusterOptions.ExecuteRemotely)
                        {
                            _item1Source.Post(new Tuple<Guid, TOutput1, CancellationTokenSource>(item.Key,
                                await _item1RemoteContract.ProcessItem1Remotely(item.Entity, NodeMetrics),
                                item.CancellationTokenSource));
                        }
                        else
                        {
                            _item1Source.Post(new Tuple<Guid, TOutput1, CancellationTokenSource>(item.Key,
                                await _item1Resolver(item.Entity, NodeMetrics, ct).WrapTaskForCancellation(ct),
                                item.CancellationTokenSource));
                        }
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        _logger.LogTrace("The item process has been cancelled.");
                    }
                    finally
                    {
                        Interlocked.Increment(ref _processedItems);
                        progress.Report((double) _processedItems / bulk.Count);
                    }
                }, cancellationToken).ConfigureAwait(false);

                if (policyResult.Outcome == OutcomeType.Failure)
                {
                    _logger.LogCritical(
                        policyResult.FinalException != null
                            ? $"Could not process item: {policyResult.FinalException.Message}."
                            : "An error has occured while processing the item.");
                }
            }

            await ComputeStatistics();
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(IList<LinkedItem<TInput2>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _previouslyProcessedItems = Interlocked.Exchange(ref _processedItems, 0L);
            if (!bulk.Any())
            {
                await ComputeStatistics();
                return;
            }

            foreach (var item in bulk)
            {
                var policy = Policy
                    .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                    .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(10, retryAttempt)),
                        (exception, sleepDuration, retry, context) =>
                        {
                            if (retry >= _clusterOptions.RetryAttempt)
                            {
                                _logger.LogError(
                                    $"Could not process item after {retry} retry times: {exception.Message}");
                            }
                        });

                var policyResult = await policy.ExecuteAndCaptureAsync(async ct =>
                {
                    try
                    {
                        if (CpuUsage > _clusterOptions.LimitCpuUsage)
                        {
                            var suspensionTime = (CpuUsage - _clusterOptions.LimitCpuUsage) / CpuUsage * 100;
                            await Task.Delay((int) suspensionTime, ct);
                        }

                        if (_clusterOptions.PersistenceEnabled)
                        {
                            _item2SynchronizedDispatcherSubject.OnNext(item);
                        }

                        if (_clusterOptions.ExecuteRemotely)
                        {
                            _item2Source.Post(new Tuple<Guid, TOutput2, CancellationTokenSource>(item.Key,
                                await _item2RemoteContract.ProcessItem2Remotely(item.Entity, NodeMetrics),
                                item.CancellationTokenSource));
                        }
                        else
                        {
                            _item2Source.Post(new Tuple<Guid, TOutput2, CancellationTokenSource>(item.Key,
                                await _item2Resolver(item.Entity, NodeMetrics, ct).WrapTaskForCancellation(ct),
                                item.CancellationTokenSource));
                        }
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        _logger.LogTrace("The item process has been cancelled.");
                    }
                    finally
                    {
                        Interlocked.Increment(ref _processedItems);
                        progress.Report((double) _processedItems / bulk.Count);
                    }
                }, cancellationToken).ConfigureAwait(false);

                if (policyResult.Outcome == OutcomeType.Failure)
                {
                    _logger.LogCritical(
                        policyResult.FinalException != null
                            ? $"Could not process item: {policyResult.FinalException.Message}."
                            : "An error has occured while processing the item.");
                }
            }

            await ComputeStatistics();
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(IList<LinkedFuncItem<TInput1>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _previouslyExecutorProcessedItems = Interlocked.Exchange(ref _executorProcessedItems, 0L);
            if (!bulk.Any())
            {
                await ComputeStatistics();
                return;
            }

            foreach (var item in bulk)
            {
                var policy = Policy
                    .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                    .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(10, retryAttempt)),
                        (exception, sleepDuration, retry, context) =>
                        {
                            if (retry >= _clusterOptions.RetryAttempt)
                            {
                                _logger.LogError(
                                    $"Could not process item after {retry} retry times: {exception.Message}");
                            }
                        });

                var policyResult = await policy.ExecuteAndCaptureAsync(async ct =>
                {
                    try
                    {
                        if (CpuUsage > _clusterOptions.LimitCpuUsage)
                        {
                            var suspensionTime = (CpuUsage - _clusterOptions.LimitCpuUsage) / CpuUsage * 100;
                            await Task.Delay((int) suspensionTime, ct);
                        }

                        var entity = item.Entity();
                        if (_clusterOptions.PersistenceEnabled)
                        {
                            _item1SynchronizedDispatcherSubject.OnNext(new LinkedItem<TInput1>(item.Key, entity,
                                item.CancellationTokenSource));
                        }

                        if (_clusterOptions.ExecuteRemotely)
                        {
                            _item1Source.Post(new Tuple<Guid, TOutput1, CancellationTokenSource>(item.Key,
                                await _item1RemoteContract.ProcessItem1Remotely(entity, NodeMetrics),
                                item.CancellationTokenSource));
                        }
                        else
                        {
                            _item1Source.Post(new Tuple<Guid, TOutput1, CancellationTokenSource>(item.Key,
                                await _item1Resolver(entity, NodeMetrics, ct).WrapTaskForCancellation(ct),
                                item.CancellationTokenSource));
                        }
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        _logger.LogTrace("The item process has been cancelled.");
                    }
                    finally
                    {
                        Interlocked.Increment(ref _executorProcessedItems);
                        progress.Report((double) _executorProcessedItems / bulk.Count);
                    }
                }, cancellationToken).ConfigureAwait(false);

                if (policyResult.Outcome == OutcomeType.Failure)
                {
                    _logger.LogCritical(
                        policyResult.FinalException != null
                            ? $"Could not process item: {policyResult.FinalException.Message}."
                            : "An error has occured while processing the item.");
                }
            }

            await ComputeStatistics();
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput1"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(IList<LinkedFuncItem<TInput2>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            _previouslyExecutorProcessedItems = Interlocked.Exchange(ref _executorProcessedItems, 0L);
            if (!bulk.Any())
            {
                await ComputeStatistics();
                return;
            }

            foreach (var item in bulk)
            {
                var policy = Policy
                    .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                    .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(10, retryAttempt)),
                        (exception, sleepDuration, retry, context) =>
                        {
                            if (retry >= _clusterOptions.RetryAttempt)
                            {
                                _logger.LogError(
                                    $"Could not process item after {retry} retry times: {exception.Message}");
                            }
                        });

                var policyResult = await policy.ExecuteAndCaptureAsync(async ct =>
                {
                    try
                    {
                        if (CpuUsage > _clusterOptions.LimitCpuUsage)
                        {
                            var suspensionTime = (CpuUsage - _clusterOptions.LimitCpuUsage) / CpuUsage * 100;
                            await Task.Delay((int) suspensionTime, ct);
                        }

                        var entity = item.Entity();
                        if (_clusterOptions.PersistenceEnabled)
                        {
                            _item2SynchronizedDispatcherSubject.OnNext(new LinkedItem<TInput2>(item.Key, entity,
                                item.CancellationTokenSource));
                        }

                        if (_clusterOptions.ExecuteRemotely)
                        {
                            _item2Source.Post(new Tuple<Guid, TOutput2, CancellationTokenSource>(item.Key,
                                await _item2RemoteContract.ProcessItem2Remotely(entity, NodeMetrics),
                                item.CancellationTokenSource));
                        }
                        else
                        {
                            _item2Source.Post(new Tuple<Guid, TOutput2, CancellationTokenSource>(item.Key,
                                await _item2Resolver(entity, NodeMetrics, ct).WrapTaskForCancellation(ct),
                                item.CancellationTokenSource));
                        }
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        _logger.LogTrace("The item process has been cancelled.");
                    }
                    finally
                    {
                        Interlocked.Increment(ref _executorProcessedItems);
                        progress.Report((double) _executorProcessedItems / bulk.Count);
                    }
                }, cancellationToken).ConfigureAwait(false);

                if (policyResult.Outcome == OutcomeType.Failure)
                {
                    _logger.LogCritical(
                        policyResult.FinalException != null
                            ? $"Could not process item: {policyResult.FinalException.Message}."
                            : "An error has occured while processing the item.");
                }
            }

            await ComputeStatistics();
        }

        /// <summary>
        /// Dispatch a <see cref="TInput1"/> to the node.
        /// </summary>
        /// <param name="item">Item to broadcast</param>
        public void Dispatch(LinkedItem<TInput1> item)
        {
            Add(item);
        }

        /// <summary>
        /// Dispatch a <see cref="TInput2"/> to the node.
        /// </summary>
        /// <param name="item">Item to broadcast</param>
        public void Dispatch(LinkedItem<TInput2> item)
        {
            Add(item);
        }

        /// <summary>
        /// Dispatch a <see cref="Func{TInput1}"/> to the node.
        /// </summary>
        /// <param name="itemProducer">Item producer to broadcast</param>
        public void Dispatch(LinkedFuncItem<TInput1> itemProducer)
        {
            Add(itemProducer);
        }

        /// <summary>
        /// Dispatch a <see cref="Func{TInput2}"/> to the node.
        /// </summary>
        /// <param name="itemProducer">Item producer to broadcast</param>
        public void Dispatch(LinkedFuncItem<TInput2> itemProducer)
        {
            Add(itemProducer);
        }

        /// <summary>
        /// <see cref="NodeMetrics"/>
        /// </summary>
        public NodeMetrics NodeMetrics { get; }

        /// <summary>
        /// Compute node statistics
        /// </summary>
        private async Task ComputeStatistics()
        {
            NodeMetrics.TotalItemsProcessed = TotalItemsProcessed();
            NodeMetrics.ItemsEvicted = ItemsEvicted();
            NodeMetrics.CurrentThroughput = _previouslyProcessedItems + _previouslyExecutorProcessedItems;
            NodeMetrics.BufferSize = GetBufferSize();
            NodeMetrics.Full = IsFull();
            if (_clusterOptions.ExecuteRemotely)
            {
                try
                {
                    await _nodeHub.HeartBeatAsync(NodeMetrics.Id);
                    NodeMetrics.Alive = true;
                    _logger.LogTrace(NodeMetrics.RemoteNodeHealth.ToString());
                }
                catch (Exception ex)
                {
                    NodeMetrics.Alive = false;
                    _logger.LogWarning(ex.Message);
                }
            }

            NodeMetrics.RefreshSubject.OnNext(NodeMetrics.Id);
        }

        /// <summary>
        /// Dispose
        /// </summary>
        /// <param name="disposing"></param>
        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _item1SynchronizedDispatcherSubjectSubscription?.Dispose();
                _item2SynchronizedDispatcherSubjectSubscription?.Dispose();
                _remoteNodeHealthSubscription?.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
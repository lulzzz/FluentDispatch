using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using MagicOnion.Client;
using Microsoft.Extensions.Logging;
using Polly;
using GrandCentralDispatch.Cache;
using GrandCentralDispatch.Hubs.Hub;
using GrandCentralDispatch.Hubs.Receiver;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Options;
using GrandCentralDispatch.Processors.Unary;
using GrandCentralDispatch.Remote;

namespace GrandCentralDispatch.Nodes.Remote.Unary
{
    /// <summary>
    /// Node which process items sequentially remotely.
    /// </summary>
    /// <typeparam name="TInput">Item to be processed</typeparam>
    internal sealed class UnarySequentialDispatcherRemoteNode<TInput> : UnarySequentialProcessor<TInput>,
        IUnaryDispatcherRemoteNode<TInput>
    {
        /// <summary>
        /// <see cref="ILogger"/>
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// <see cref="IRemoteContract{TInput}"/>
        /// </summary>
        private readonly IRemoteContract<TInput> _remoteContract;

        /// <summary>
        /// <see cref="ClusterOptions"/>
        /// </summary>
        private readonly ClusterOptions _clusterOptions;

        /// <summary>
        /// <see cref="INodeHub"/>
        /// </summary>
        private readonly INodeHub _nodeHub;

        /// <summary>
        /// Synchronized subject
        /// </summary>
        private readonly ISubject<PersistentItem<TInput>> _synchronizedDispatcherSubject;

        /// <summary>
        /// Subscription
        /// </summary>
        private readonly IDisposable _synchronizedDispatcherSubjectSubscription;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private readonly IDisposable _remoteNodeHealthSubscription;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Number of processed items for the current bulk
        /// </summary>
        private long _processedItems;

        /// <summary>
        /// Number of processed item executors for the current bulk
        /// </summary>
        private long _executorProcessedItems;

        /// <summary>
        /// <see cref="UnarySequentialDispatcherRemoteNode{TInput}"/>
        /// </summary>
        /// <param name="persistentCache">Persistent cache to avoid dropped data on system crash</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="host"><see cref="Host"/></param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        public UnarySequentialDispatcherRemoteNode(
            IAppCache persistentCache,
            IProgress<double> progress,
            Host host,
            CancellationTokenSource cts,
            CircuitBreakerOptions circuitBreakerOptions,
            ClusterOptions clusterOptions,
            ILogger logger) : base(
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
            _logger = logger;
            _clusterOptions = clusterOptions;

            ISubject<PersistentItem<TInput>> dispatcherSubject = new Subject<PersistentItem<TInput>>();
            _synchronizedDispatcherSubject = Subject.Synchronize(dispatcherSubject);
            _synchronizedDispatcherSubjectSubscription = _synchronizedDispatcherSubject
                .ObserveOn(new EventLoopScheduler(ts => new Thread(ts)))
                .Select(item =>
                {
                    return Observable.FromAsync(() => persistentCache.AddItemAsync(item.Entity,
                        item.CancellationTokenSource.Token));
                })
                .Merge()
                .Subscribe();

            var channel = new Channel(host.MachineName, host.Port,
                ChannelCredentials.Insecure);
            _remoteContract = MagicOnionClient.Create<IRemoteContract<TInput>>(channel);
            IRemoteNodeSubject nodeReceiver = new NodeReceiver(_logger);
            _remoteNodeHealthSubscription =
                nodeReceiver.RemoteNodeHealthSubject.Subscribe(remoteNodeHealth =>
                {
                    NodeMetrics.RemoteNodeHealth = remoteNodeHealth;
                });
            _nodeHub = StreamingHubClient.Connect<INodeHub, INodeReceiver>(channel, (INodeReceiver) nodeReceiver);

            NodeMetrics = new NodeMetrics(Guid.NewGuid());
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(IList<TInput> bulk, IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var currentProgress = 0;
            foreach (var item in bulk)
            {
                var policy = Policy
                    .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                    .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
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

                        var persistentCacheToken = new CancellationTokenSource();
                        var persistentItem = new PersistentItem<TInput>(item, persistentCacheToken);
                        if (_clusterOptions.PersistenceEnabled)
                        {
                            _synchronizedDispatcherSubject.OnNext(persistentItem);
                        }

                        await _remoteContract.ProcessRemotely(item, NodeMetrics);
                        persistentItem.CancellationTokenSource.Cancel();
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        _logger.LogTrace("The item process has been cancelled.");
                    }
                    finally
                    {
                        Interlocked.Increment(ref _processedItems);
                        currentProgress++;
                        progress.Report((double) currentProgress / bulk.Count);
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
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="bulk">Bulk of <see cref="TInput"/> to process</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(IList<Func<TInput>> bulk, IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var currentProgress = 0;
            foreach (var item in bulk)
            {
                var policy = Policy
                    .Handle<Exception>(ex => !(ex is TaskCanceledException || ex is OperationCanceledException))
                    .WaitAndRetryAsync(_clusterOptions.RetryAttempt,
                        retryAttempt =>
                            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
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

                        var entity = item();
                        var persistentCacheToken = new CancellationTokenSource();
                        var persistentItem = new PersistentItem<TInput>(entity, persistentCacheToken);
                        if (_clusterOptions.PersistenceEnabled)
                        {
                            _synchronizedDispatcherSubject.OnNext(persistentItem);
                        }

                        await _remoteContract.ProcessRemotely(entity, NodeMetrics);
                        persistentItem.CancellationTokenSource.Cancel();
                    }
                    catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                    {
                        _logger.LogTrace("The item process has been cancelled.");
                    }
                    finally
                    {
                        Interlocked.Increment(ref _executorProcessedItems);
                        currentProgress++;
                        progress.Report((double) currentProgress / bulk.Count);
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
        }

        /// <summary>
        /// Dispatch a <see cref="TInput"/> to the node.
        /// </summary>
        /// <param name="item">Item to broadcast</param>
        public void Dispatch(TInput item)
        {
            Add(item);
        }

        /// <summary>
        /// Dispatch a <see cref="Func{TInput}"/> to the node.
        /// </summary>
        /// <param name="itemProducer">Item producer to broadcast</param>
        public void Dispatch(Func<TInput> itemProducer)
        {
            Add(itemProducer);
        }

        /// <summary>
        /// Node metrics
        /// </summary>
        public NodeMetrics NodeMetrics { get; }

        /// <summary>
        /// Compute node statistics
        /// </summary>
        protected override async Task ComputeMetrics()
        {
            await base.ComputeMetrics();
            if (NodeMetrics == null) return;
            NodeMetrics.TotalItemsProcessed = TotalItemsProcessed();
            NodeMetrics.ItemsEvicted = ItemsEvicted();
            NodeMetrics.CurrentThroughput = Interlocked.Exchange(ref _processedItems, 0L) +
                                            Interlocked.Exchange(ref _executorProcessedItems, 0L);
            NodeMetrics.BufferSize = GetBufferSize();
            NodeMetrics.Full = IsFull();
            try
            {
                if (_nodeHub == null) return;
                await _nodeHub.HeartBeatAsync(NodeMetrics.Id);
                NodeMetrics.Alive = true;
                _logger.LogTrace(NodeMetrics.RemoteNodeHealth.ToString());
            }
            catch (Exception ex)
            {
                NodeMetrics.Alive = false;
                _logger.LogWarning(ex.Message);
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
                _synchronizedDispatcherSubjectSubscription?.Dispose();
                _remoteNodeHealthSubscription?.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
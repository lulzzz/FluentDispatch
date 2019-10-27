using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Options;
using Grpc.Core;
using MagicOnion.Client;
using GrandCentralDispatch.Hubs.Receiver;
using GrandCentralDispatch.Hubs.Hub;
using GrandCentralDispatch.Remote;
using GrandCentralDispatch.Processors.Remote;
using GrandCentralDispatch.Exceptions;

namespace GrandCentralDispatch.Nodes.Remote
{
    /// <summary>
    /// Node which process items remotely.
    /// </summary>
    /// <typeparam name="TInput">Item to be processed</typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal sealed class RemoteNode<TInput, TOutput> : RemoteProcessor<TInput, TOutput>,
        IRemoteNode<TInput, TOutput>
    {
        /// <summary>
        /// <see cref="ILogger"/>
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// <see cref="ClusterOptions"/>
        /// </summary>
        private readonly ClusterOptions _clusterOptions;

        /// <summary>
        /// <see cref="IRemoteContract{TInput}"/>
        /// </summary>
        private readonly IOutputRemoteContract<TInput, TOutput> _remoteContract;

        /// <summary>
        /// <see cref="INodeHub"/>
        /// </summary>
        private readonly INodeHub _nodeHub;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// <see cref="IDisposable"/>
        /// </summary>
        private readonly IDisposable _remoteNodeHealthSubscription;

        /// <summary>
        /// Number of currently processed items for the current bulk
        /// </summary>
        private long _processedItems;

        /// <summary>
        /// <see cref="RemoteNode{TInput,TOutput}"/>
        /// </summary>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        /// <param name="host"><see cref="Host"/></param>
        public RemoteNode(
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
                    }), clusterOptions, cts, logger)
        {
            _logger = logger;
            _clusterOptions = clusterOptions;
            if (_clusterOptions.ExecuteRemotely && host != null)
            {
                var channel = new Channel(host.MachineName, host.Port,
                    ChannelCredentials.Insecure);
                _remoteContract = MagicOnionClient.Create<IOutputRemoteContract<TInput, TOutput>>(channel);
                IRemoteNodeSubject nodeReceiver = new NodeReceiver(_logger);
                _remoteNodeHealthSubscription =
                    nodeReceiver.RemoteNodeHealthSubject.Subscribe(remoteNodeHealth =>
                    {
                        NodeMetrics.RemoteNodeHealth = remoteNodeHealth;
                    });
                _nodeHub = StreamingHubClient.Connect<INodeHub, INodeReceiver>(channel, (INodeReceiver)nodeReceiver);
            }

            NodeMetrics = new NodeMetrics(Guid.NewGuid());
        }

        /// <summary>
        /// The bulk processor.
        /// </summary>
        /// <param name="item"><see cref="TInput"/> to process</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(RemoteItem<TInput, TOutput> item, CancellationToken cancellationToken)
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
                            item.TaskCompletionSource.TrySetException(exception);
                        }
                    });

            var policyResult = await policy.ExecuteAndCaptureAsync(async ct =>
            {
                try
                {
                    if (CpuUsage > _clusterOptions.LimitCpuUsage)
                    {
                        var suspensionTime = (CpuUsage - _clusterOptions.LimitCpuUsage) / CpuUsage * 100;
                        await Task.Delay((int)suspensionTime, ct);
                    }

                    if (_clusterOptions.ExecuteRemotely)
                    {
                        var result = await _remoteContract.ProcessRemotely(item.Item, NodeMetrics);
                        item.TaskCompletionSource.TrySetResult(result);
                    }
                    else
                    {
                        item.TaskCompletionSource.TrySetException(new GrandCentralDispatchException($"{typeof(RemoteNode<TInput, TOutput>)} must be configured within a remotely setup cluster."));
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException || ex is OperationCanceledException)
                {
                    _logger.LogTrace("The item process has been cancelled.");
                    item.TaskCompletionSource.TrySetCanceled();
                }
                finally
                {
                    Interlocked.Increment(ref _processedItems);
                }
            }, cancellationToken).ConfigureAwait(false);

            if (policyResult.Outcome == OutcomeType.Failure)
            {
                item.TaskCompletionSource.TrySetException(policyResult.FinalException);
                _logger.LogCritical(
                    policyResult.FinalException != null
                        ? $"Could not process item: {policyResult.FinalException.Message}."
                        : "An error has occured while processing the item.");
            }
        }

        /// <summary>
        /// Execute a <see cref="Func{TInput}"/> against a remote the node.
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="item"><see cref="TInput"/></param>
        /// <returns><see cref="TOutput"/></returns>
        public async Task<TOutput> ExecuteAsync(TInput item)
        {
            var taskCompletionSource = new TaskCompletionSource<TOutput>();
            return await ProcessAsync(new RemoteItem<TInput, TOutput>(taskCompletionSource, item));
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
            NodeMetrics.CurrentThroughput = Interlocked.Exchange(ref _processedItems, 0L);
            NodeMetrics.BufferSize = 0;
            NodeMetrics.Full = false;
            if (_clusterOptions.ExecuteRemotely)
            {
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
                _remoteNodeHealthSubscription?.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
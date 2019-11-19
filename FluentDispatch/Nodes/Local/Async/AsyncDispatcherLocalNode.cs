using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using FluentDispatch.Models;
using FluentDispatch.Options;
using FluentDispatch.Processors.Async;

namespace FluentDispatch.Nodes.Local.Async
{
    /// <summary>
    /// Node which process items locally.
    /// </summary>
    /// <typeparam name="TInput">Item to be processed</typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal sealed class AsyncDispatcherLocalNode<TInput, TOutput> :
        AsyncProcessor<TInput, TOutput, AsyncPredicateItem<TInput, TOutput>>,
        IAsyncDispatcherLocalNode<TInput, TOutput>
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
        /// <see cref="IDisposable"/>
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Number of currently processed items for the current bulk
        /// </summary>
        private long _processedItems;

        /// <summary>
        /// <see cref="AsyncDispatcherLocalNode{TInput,TOutput}"/>
        /// </summary>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="logger"><see cref="ILogger"/></param>
        public AsyncDispatcherLocalNode(
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
                    }), clusterOptions, cts, logger)
        {
            _logger = logger;
            _clusterOptions = clusterOptions;
            NodeMetrics = new NodeMetrics(Guid.NewGuid());
        }

        /// <summary>
        /// The processor.
        /// </summary>
        /// <param name="item"><see cref="TInput"/> to process</param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected override async Task Process(AsyncPredicateItem<TInput, TOutput> item,
            CancellationToken cancellationToken)
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
                        await Task.Delay((int) suspensionTime, ct);
                    }

                    var result = await item.Selector(item.Item);
                    item.TaskCompletionSource.TrySetResult(result);
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
        /// Execute a <see cref="Func{TInput}"/> against the local node using a selector predicate.
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="selector"><see cref="Func{TResult}"/></param>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        public async Task<TOutput> ExecuteAsync(Func<TInput, Task<TOutput>> selector, TInput item,
            CancellationToken cancellationToken)
        {
            var taskCompletionSource = new TaskCompletionSource<TOutput>(TaskCreationOptions.RunContinuationsAsynchronously);
            return await ProcessAsync(
                new AsyncPredicateItem<TInput, TOutput>(taskCompletionSource, selector, item, cancellationToken));
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

            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GrandCentralDispatch.Exceptions;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Nodes.Async;
using GrandCentralDispatch.Nodes.Dual;
using GrandCentralDispatch.Nodes.Unary;
using GrandCentralDispatch.Options;
using GrandCentralDispatch.Resolvers;

namespace GrandCentralDispatch.Clusters
{
    /// <summary>
    /// The cluster which is in charge of distributing the load to the configured nodes.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    public class AsyncCluster<TInput, TOutput> : ClusterBase, IAsyncCluster<TInput, TOutput>
    {
        /// <summary>
        /// Async nodes of the cluster
        /// </summary>
        private readonly List<IAsyncDispatcherNode<TInput, TOutput>> _nodes =
            new List<IAsyncDispatcherNode<TInput, TOutput>>();

        /// <summary>
        /// <see cref="AsyncCluster{TInput,TOutput}"/>
        /// </summary>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="progress"><see cref="Progress{TInput}"/></param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        public AsyncCluster(
            IOptions<ClusterOptions> clusterOptions,
            IOptions<CircuitBreakerOptions> circuitBreakerOptions,
            IProgress<double> progress = null,
            CancellationTokenSource cts = null,
            ILoggerFactory loggerFactory = null) :
            this(progress, cts, clusterOptions.Value, circuitBreakerOptions.Value,
                loggerFactory)
        {

        }

        /// <summary>
        /// <see cref="AsyncCluster{TInput,TOutput}"/>
        /// </summary>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        private AsyncCluster(
            IProgress<double> progress,
            CancellationTokenSource cts,
            ClusterOptions clusterOptions,
            CircuitBreakerOptions circuitBreakerOptions,
            ILoggerFactory loggerFactory) : base(progress, cts, clusterOptions,
            loggerFactory == null
                ? NullLogger<Cluster<TInput>>.Instance
                : loggerFactory.CreateLogger<Cluster<TInput>>(), loggerFactory)
        {
            try
            {
                for (var i = 0; i < clusterOptions.ClusterSize; i++)
                {
                    if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Parallel)
                    {
                        _nodes.Add((IAsyncDispatcherNode<TInput, TOutput>) Activator.CreateInstance(
                            typeof(AsyncParallelDispatcherNode<TInput, TOutput>),
                            Progress,
                            CancellationTokenSource,
                            circuitBreakerOptions,
                            clusterOptions,
                            Logger));
                    }
                    else if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Sequential)
                    {
                        _nodes.Add((IAsyncDispatcherNode<TInput, TOutput>) Activator.CreateInstance(
                            typeof(AsyncSequentialDispatcherNode<TInput, TOutput>),
                            Progress,
                            CancellationTokenSource,
                            circuitBreakerOptions,
                            clusterOptions,
                            Logger));
                    }
                    else
                    {
                        throw new NotImplementedException(
                            $"{nameof(ClusterProcessingType)} of value {clusterOptions.ClusterProcessingType.ToString()} is not implemented.");
                    }
                }

                foreach (var node in _nodes)
                {
                    node.NodeMetrics.RefreshSubject.Subscribe(ComputeNodeHealth);
                }

                LogClusterOptions(circuitBreakerOptions, _nodes.Count);
                Logger.LogInformation("Cluster successfully initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        /// <summary>
        /// Compute node health
        /// </summary>
        /// <param name="guid">Node identifier</param>
        private void ComputeNodeHealth(Guid guid)
        {
            var node = _nodes.Single(n => n.NodeMetrics.Id == guid);
            ComputeNodeHealth(node.NodeMetrics);
        }

        /// <summary>
        /// Compute cluster health
        /// </summary>
        protected override void ComputeClusterHealth()
        {
            ClusterMetrics.CurrentThroughput = _nodes.Sum(node => node.NodeMetrics.CurrentThroughput);
            base.ComputeClusterHealth();
        }

        /// <summary>
        /// Dispatch an item to the cluster and wait for the result
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="selector"></param>
        /// <param name="item"><see cref="TInput"/></param>
        /// <returns><see cref="TOutput"/></returns>
        public async Task<TOutput> DispatchAsync(Func<TInput, Task<TOutput>> selector, TInput item)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed
                            ? node1
                            : node2);
                    return await node.DispatchAsync(selector, item);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    return await node.DispatchAsync(selector, item);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    return await node.DispatchAsync(selector, item);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                    throw new GrandCentralDispatchException(message);
                }
            }
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
                foreach (var node in _nodes)
                {
                    node?.Dispose();
                }
            }

            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// The cluster which is in charge of distributing the load to the configured nodes.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    public class Cluster<TInput> : ClusterBase, ICluster<TInput>
    {
        /// <summary>
        /// Nodes of the cluster
        /// </summary>
        private readonly List<IUnaryDispatcherNode<TInput>> _nodes = new List<IUnaryDispatcherNode<TInput>>();

        /// <summary>
        /// <see cref="Cluster{TInput}"/>
        /// </summary>
        /// <param name="resolver"><see cref="Resolver{TInput}"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="progress"><see cref="Progress{TInput}"/></param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        public Cluster(
            FuncResolver<TInput> resolver,
            IOptions<ClusterOptions> clusterOptions,
            IOptions<CircuitBreakerOptions> circuitBreakerOptions,
            IProgress<double> progress = null,
            CancellationTokenSource cts = null,
            ILoggerFactory loggerFactory = null) :
            this(resolver, progress, cts, clusterOptions.Value, circuitBreakerOptions.Value,
                loggerFactory)
        {

        }

        /// <summary>
        /// <see cref="Cluster{TInput}"/>
        /// </summary>
        /// <param name="funcResolver">Resolve the action to execute asynchronously for each items when they are dequeued.</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        private Cluster(
            FuncResolver<TInput> funcResolver,
            IProgress<double> progress,
            CancellationTokenSource cts,
            ClusterOptions clusterOptions,
            CircuitBreakerOptions circuitBreakerOptions,
            ILoggerFactory loggerFactory) : base(progress, cts, clusterOptions,
            loggerFactory == null
                ? NullLogger<Cluster<TInput>>.Instance
                : loggerFactory.CreateLogger<Cluster<TInput>>(), loggerFactory)
        {
            try
            {
                if (ClusterOptions.ExecuteRemotely)
                {
                    if (!ClusterOptions.Hosts.Any())
                    {
                        throw new GrandCentralDispatchException(
                            "Hosts must be provided if cluster option ExecuteRemotely is true.");
                    }

                    foreach (var host in ClusterOptions.Hosts)
                    {
                        if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Parallel)
                        {
                            _nodes.Add((IUnaryDispatcherNode<TInput>) Activator.CreateInstance(
                                typeof(UnaryParallelDispatcherNode<TInput>),
                                PersistentCache,
                                funcResolver.GetItemFunc(),
                                Progress,
                                CancellationTokenSource,
                                circuitBreakerOptions,
                                clusterOptions,
                                Logger,
                                host));
                        }
                        else if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Sequential)
                        {
                            _nodes.Add((IUnaryDispatcherNode<TInput>) Activator.CreateInstance(
                                typeof(UnarySequentialDispatcherNode<TInput>),
                                PersistentCache,
                                funcResolver.GetItemFunc(),
                                Progress,
                                CancellationTokenSource,
                                circuitBreakerOptions,
                                clusterOptions,
                                Logger,
                                host));
                        }
                        else
                        {
                            throw new NotImplementedException(
                                $"{nameof(ClusterProcessingType)} of value {clusterOptions.ClusterProcessingType.ToString()} is not implemented.");
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < clusterOptions.ClusterSize; i++)
                    {
                        if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Parallel)
                        {
                            _nodes.Add((IUnaryDispatcherNode<TInput>) Activator.CreateInstance(
                                typeof(UnaryParallelDispatcherNode<TInput>),
                                PersistentCache,
                                funcResolver.GetItemFunc(),
                                Progress,
                                CancellationTokenSource,
                                circuitBreakerOptions,
                                clusterOptions,
                                Logger,
                                null));
                        }
                        else if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Sequential)
                        {
                            _nodes.Add((IUnaryDispatcherNode<TInput>) Activator.CreateInstance(
                                typeof(UnarySequentialDispatcherNode<TInput>),
                                PersistentCache,
                                funcResolver.GetItemFunc(),
                                Progress,
                                CancellationTokenSource,
                                circuitBreakerOptions,
                                clusterOptions,
                                Logger,
                                null));
                        }
                        else
                        {
                            throw new NotImplementedException(
                                $"{nameof(ClusterProcessingType)} of value {clusterOptions.ClusterProcessingType.ToString()} is not implemented.");
                        }
                    }
                }

                foreach (var node in _nodes)
                {
                    node.NodeMetrics.RefreshSubject.Subscribe(ComputeNodeHealth);
                }

                LogClusterOptions(circuitBreakerOptions, _nodes.Count);
                var persistedItems =
                    PersistentCache.CacheProvider.RetrieveItemsAsync<TInput>().GetAwaiter().GetResult().ToList();
                PersistentCache.CacheProvider.FlushDatabaseAsync().GetAwaiter().GetResult();
                if (persistedItems.Any())
                {
                    Logger.LogInformation("Cluster was shutdown while items remained to be processed. Submitting...");
                    foreach (var item in persistedItems)
                    {
                        Dispatch(item);
                    }

                    Logger.LogInformation("Remaining items have been successfully processed.");
                }

                Logger.LogInformation("Cluster successfully initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        /// <summary>
        /// Compute node health
        /// </summary>
        /// <param name="guid">Node identifier</param>
        private void ComputeNodeHealth(Guid guid)
        {
            var node = _nodes.Single(n => n.NodeMetrics.Id == guid);
            ComputeNodeHealth(node.NodeMetrics);
        }

        /// <summary>
        /// Compute cluster health
        /// </summary>
        protected override void ComputeClusterHealth()
        {
            ClusterMetrics.CurrentThroughput = _nodes.Sum(node => node.NodeMetrics.CurrentThroughput);
            base.ComputeClusterHealth();
        }

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="item">The item to process</param>
        public void Dispatch(TInput item)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed
                            ? node1
                            : node2);
                    node.Dispatch(item);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    node.Dispatch(item);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    node.Dispatch(item);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                }
            }
        }

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="itemProducer">The item producer to process</param>
        public void Dispatch(Func<TInput> itemProducer)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed
                            ? node1
                            : node2);
                    node.Dispatch(itemProducer);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    node.Dispatch(itemProducer);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    node.Dispatch(itemProducer);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                }
            }
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
                foreach (var node in _nodes)
                {
                    node?.Dispose();
                }
            }

            Disposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// The cluster which is in charge of distributing the load to the configured nodes.
    /// </summary>
    /// <typeparam name="TInput1"></typeparam>
    /// <typeparam name="TInput2"></typeparam>
    /// <typeparam name="TOutput1"></typeparam>
    /// <typeparam name="TOutput2"></typeparam>
    public class Cluster<TInput1, TInput2, TOutput1, TOutput2> : ClusterBase, ICluster<TInput1, TInput2>
    {
        /// <summary>
        /// Nodes of the cluster
        /// </summary>
        private readonly List<IDualDispatcherNode<TInput1, TInput2>> _nodes =
            new List<IDualDispatcherNode<TInput1, TInput2>>();

        /// <summary>
        /// Store the items keys in order to join them on their attributed node
        /// </summary>
        private readonly IMemoryCache _resolverCache;

        /// <summary>
        /// <see cref="MemoryCacheEntryOptions"/>
        /// </summary>
        private readonly MemoryCacheEntryOptions _cacheEntryOptions;

        /// <summary>
        /// <see cref="Cluster{TInput1, TInput2, TOutput2, TOutput2}"/>
        /// </summary>
        /// <param name="resolverCache"><see cref="IMemoryCache"/></param>
        /// <param name="item1PartialResolver"><see cref="FuncPartialResolver{TInput2,TOutput2}"/></param>
        /// <param name="item2PartialResolver"><see cref="FuncPartialResolver{TInput2,TOutput2}"/></param>
        /// <param name="dualResolver"><see cref="DualResolver{TOutput1,TOutput2}"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="progress"><see cref="Progress{TInput1}"/></param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        public Cluster(
            IMemoryCache resolverCache,
            PartialResolver<TInput1, TOutput1> item1PartialResolver,
            PartialResolver<TInput2, TOutput2> item2PartialResolver,
            DualResolver<TOutput1, TOutput2> dualResolver,
            IOptions<ClusterOptions> clusterOptions,
            IOptions<CircuitBreakerOptions> circuitBreakerOptions,
            IProgress<double> progress = null,
            CancellationTokenSource cts = null,
            ILoggerFactory loggerFactory = null) :
            this(resolverCache, item1PartialResolver, item2PartialResolver, dualResolver,
                progress, cts,
                clusterOptions.Value,
                circuitBreakerOptions.Value,
                loggerFactory)
        {

        }

        /// <summary>
        /// <see cref="Cluster{TInput1, TInput2, TOutput2, TOutput2}"/>
        /// </summary>
        /// <param name="resolverCache"><see cref="IMemoryCache"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="progress"><see cref="Progress{TInput1}"/></param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        public Cluster(
            IMemoryCache resolverCache,
            IOptions<ClusterOptions> clusterOptions,
            IOptions<CircuitBreakerOptions> circuitBreakerOptions,
            IProgress<double> progress = null,
            CancellationTokenSource cts = null,
            ILoggerFactory loggerFactory = null) :
            this(resolverCache, progress, cts,
                clusterOptions.Value,
                circuitBreakerOptions.Value,
                loggerFactory)
        {

        }

        /// <summary>
        /// <see cref="Cluster{TInput1, TInput2, TOutput2, TOutput2}"/>
        /// </summary>
        /// <param name="resolverCache"><see cref="IMemoryCache"/></param>
        /// <param name="item1PartialResolver">Resolve the action to execute asynchronously for each items when they are dequeued.</param>
        /// <param name="item2PartialResolver">Resolve the action to execute asynchronously for each items when they are dequeued.</param>
        /// <param name="dualResolver">Resolve the action to execute asynchronously for each items when they are dequeued.</param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        private Cluster(
            IMemoryCache resolverCache,
            FuncPartialResolver<TInput1, TOutput1> item1PartialResolver,
            FuncPartialResolver<TInput2, TOutput2> item2PartialResolver,
            FuncResolver<TOutput1, TOutput2> dualResolver,
            IProgress<double> progress,
            CancellationTokenSource cts,
            ClusterOptions clusterOptions,
            CircuitBreakerOptions circuitBreakerOptions,
            ILoggerFactory loggerFactory) : base(progress, cts, clusterOptions,
            loggerFactory == null
                ? NullLogger<Cluster<TInput1, TInput2, TOutput1, TOutput2>>.Instance
                : loggerFactory.CreateLogger<Cluster<TInput1, TInput2, TOutput1, TOutput2>>(), loggerFactory)
        {
            _resolverCache = resolverCache;
            _cacheEntryOptions = new MemoryCacheEntryOptions();
            _cacheEntryOptions
                .SetPriority(CacheItemPriority.Normal)
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetSize(1);
            try
            {
                for (var i = 0; i < clusterOptions.ClusterSize; i++)
                {
                    if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Parallel)
                    {
                        _nodes.Add((IDualDispatcherNode<TInput1, TInput2>) Activator.CreateInstance(
                            typeof(DualParallelDispatcherNode<TInput1, TInput2, TOutput1, TOutput2>),
                            PersistentCache,
                            item1PartialResolver.GetItemFunc(),
                            item2PartialResolver.GetItemFunc(),
                            dualResolver.GetItemFunc(),
                            Progress,
                            CancellationTokenSource,
                            circuitBreakerOptions,
                            clusterOptions,
                            Logger,
                            null));
                    }
                    else if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Sequential)
                    {
                        _nodes.Add((IDualDispatcherNode<TInput1, TInput2>) Activator.CreateInstance(
                            typeof(DualSequentialDispatcherNode<TInput1, TInput2, TOutput1, TOutput2>),
                            PersistentCache,
                            item1PartialResolver.GetItemFunc(),
                            item2PartialResolver.GetItemFunc(),
                            dualResolver.GetItemFunc(),
                            Progress,
                            CancellationTokenSource,
                            circuitBreakerOptions,
                            clusterOptions,
                            Logger,
                            null));
                    }
                    else
                    {
                        throw new NotImplementedException(
                            $"{nameof(ClusterProcessingType)} of value {clusterOptions.ClusterProcessingType.ToString()} is not implemented.");
                    }
                }

                foreach (var node in _nodes)
                {
                    node.NodeMetrics.RefreshSubject.Subscribe(ComputeNodeHealth);
                }

                LogClusterOptions(circuitBreakerOptions, _nodes.Count);
                var persistedItems1 = PersistentCache.CacheProvider.RetrieveItems1Async<TInput1>().GetAwaiter()
                    .GetResult().ToList();
                var persistedItems2 = PersistentCache.CacheProvider.RetrieveItems2Async<TInput2>().GetAwaiter()
                    .GetResult().ToList();
                PersistentCache.CacheProvider.FlushDatabaseAsync().GetAwaiter().GetResult();
                if (persistedItems1.Any() || persistedItems2.Any())
                {
                    Logger.LogInformation("Cluster was shutdown while items remained to be processed. Submitting...");
                    foreach (var (key, entity) in persistedItems1)
                    {
                        Dispatch(Guid.Parse(key), entity);
                    }

                    foreach (var (key, entity) in persistedItems2)
                    {
                        Dispatch(Guid.Parse(key), entity);
                    }

                    Logger.LogInformation("Remaining items have been successfully processed.");
                }

                Logger.LogInformation("Cluster successfully initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        /// <summary>
        /// <see cref="Cluster{TInput1, TInput2, TOutput2, TOutput2}"/>
        /// </summary>
        /// <param name="resolverCache"><see cref="IMemoryCache"/></param>
        /// <param name="progress">Progress of the current bulk</param>
        /// <param name="cts"><see cref="CancellationTokenSource"/></param>
        /// <param name="clusterOptions"><see cref="ClusterOptions"/></param>
        /// <param name="circuitBreakerOptions"><see cref="CircuitBreakerOptions"/></param>
        /// <param name="loggerFactory"><see cref="ILoggerFactory"/></param>
        private Cluster(
            IMemoryCache resolverCache,
            IProgress<double> progress,
            CancellationTokenSource cts,
            ClusterOptions clusterOptions,
            CircuitBreakerOptions circuitBreakerOptions,
            ILoggerFactory loggerFactory) : base(progress, cts, clusterOptions,
            loggerFactory == null
                ? NullLogger<Cluster<TInput1, TInput2, TOutput1, TOutput2>>.Instance
                : loggerFactory.CreateLogger<Cluster<TInput1, TInput2, TOutput1, TOutput2>>(), loggerFactory)
        {
            _resolverCache = resolverCache;
            _cacheEntryOptions = new MemoryCacheEntryOptions();
            _cacheEntryOptions
                .SetPriority(CacheItemPriority.Normal)
                .SetSlidingExpiration(TimeSpan.FromMinutes(10))
                .SetSize(1);
            try
            {
                if (ClusterOptions.ExecuteRemotely)
                {
                    if (!ClusterOptions.Hosts.Any())
                    {
                        throw new GrandCentralDispatchException(
                            "Hosts must be provided if cluster option ExecuteRemotely is true.");
                    }

                    foreach (var host in ClusterOptions.Hosts)
                    {
                        if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Parallel)
                        {
                            _nodes.Add((IDualDispatcherNode<TInput1, TInput2>) Activator.CreateInstance(
                                typeof(DualParallelDispatcherNode<TInput1, TInput2, TOutput1, TOutput2>),
                                PersistentCache,
                                Progress,
                                CancellationTokenSource,
                                circuitBreakerOptions,
                                clusterOptions,
                                Logger,
                                host));
                        }
                        else if (clusterOptions.ClusterProcessingType == ClusterProcessingType.Sequential)
                        {
                            _nodes.Add((IDualDispatcherNode<TInput1, TInput2>) Activator.CreateInstance(
                                typeof(DualSequentialDispatcherNode<TInput1, TInput2, TOutput1, TOutput2>),
                                PersistentCache,
                                Progress,
                                CancellationTokenSource,
                                circuitBreakerOptions,
                                clusterOptions,
                                Logger,
                                host));
                        }
                        else
                        {
                            throw new NotImplementedException(
                                $"{nameof(ClusterProcessingType)} of value {clusterOptions.ClusterProcessingType.ToString()} is not implemented.");
                        }
                    }
                }
                else
                {
                    throw new GrandCentralDispatchException("This cluster should be set-up with remote execution.");
                }

                foreach (var node in _nodes)
                {
                    node.NodeMetrics.RefreshSubject.Subscribe(ComputeNodeHealth);
                }

                LogClusterOptions(circuitBreakerOptions, _nodes.Count);
                var persistedItems1 = PersistentCache.CacheProvider.RetrieveItems1Async<TInput1>().GetAwaiter()
                    .GetResult().ToList();
                var persistedItems2 = PersistentCache.CacheProvider.RetrieveItems2Async<TInput2>().GetAwaiter()
                    .GetResult().ToList();
                PersistentCache.CacheProvider.FlushDatabaseAsync().GetAwaiter().GetResult();
                if (persistedItems1.Any() || persistedItems2.Any())
                {
                    Logger.LogInformation("Cluster was shutdown while items remained to be processed. Submitting...");
                    foreach (var (key, entity) in persistedItems1)
                    {
                        Dispatch(Guid.Parse(key), entity);
                    }

                    foreach (var (key, entity) in persistedItems2)
                    {
                        Dispatch(Guid.Parse(key), entity);
                    }

                    Logger.LogInformation("Remaining items have been successfully processed.");
                }

                Logger.LogInformation("Cluster successfully initialized.");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }

        /// <summary>
        /// Compute node health
        /// </summary>
        /// <param name="guid">Node identifier</param>
        private void ComputeNodeHealth(Guid guid)
        {
            var node = _nodes.Single(n => n.NodeMetrics.Id == guid);
            ComputeNodeHealth(node.NodeMetrics);
        }

        /// <summary>
        /// Compute cluster health
        /// </summary>
        protected override void ComputeClusterHealth()
        {
            ClusterMetrics.CurrentThroughput = _nodes.Sum(node => node.NodeMetrics.CurrentThroughput);
            base.ComputeClusterHealth();
        }

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="item">The item to process</param>
        public void Dispatch(Guid key, TInput1 item)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (_resolverCache.TryGetValue(key, out var value) && value is Guid affinityNodeGuid)
                {
                    var node = availableNodes.FirstOrDefault(n => n.NodeMetrics.Id == affinityNodeGuid);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput1>(key, item, persistentCacheToken);
                    node?.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed ? node1 : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput1>(key, item, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput1>(key, item, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput1>(key, item, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                }
            }
        }

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="item">The item to process</param>
        public void Dispatch(Guid key, TInput2 item)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (_resolverCache.TryGetValue(key, out var value) && value is Guid affinityNodeGuid)
                {
                    var node = availableNodes.FirstOrDefault(n => n.NodeMetrics.Id == affinityNodeGuid);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput2>(key, item, persistentCacheToken);
                    node?.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed ? node1 : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput2>(key, item, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput2>(key, item, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedItem<TInput2>(key, item, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                }
            }
        }

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="itemProducer">The item to process</param>
        public void Dispatch(Guid key, Func<TInput1> itemProducer)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (_resolverCache.TryGetValue(key, out var value) && value is Guid affinityNodeGuid)
                {
                    var node = availableNodes.FirstOrDefault(n => n.NodeMetrics.Id == affinityNodeGuid);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput1>(key, itemProducer, persistentCacheToken);
                    node?.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed ? node1 : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput1>(key, itemProducer, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput1>(key, itemProducer, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput1>(key, itemProducer, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                }
            }
        }

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="itemProducer">The item to process</param>
        public void Dispatch(Guid key, Func<TInput2> itemProducer)
        {
            var availableNodes = _nodes
                .Where(node =>
                    node.NodeMetrics.Alive && (!ClusterOptions.EvictItemsWhenNodesAreFull || !node.NodeMetrics.Full))
                .ToList();
            if (availableNodes.Any())
            {
                if (_resolverCache.TryGetValue(key, out var value) && value is Guid affinityNodeGuid)
                {
                    var node = availableNodes.FirstOrDefault(n => n.NodeMetrics.Id == affinityNodeGuid);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput2>(key, itemProducer, persistentCacheToken);
                    node?.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.BestEffort)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.TotalItemsProcessed <= node2.NodeMetrics.TotalItemsProcessed ? node1 : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput2>(key, itemProducer, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Randomized)
                {
                    var node = availableNodes.ElementAt(Random.Value.Next(0,
                        availableNodes.Count - 1));
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput2>(key, itemProducer, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else if (ClusterOptions.NodeQueuingStrategy == NodeQueuingStrategy.Healthiest)
                {
                    var node = availableNodes.Aggregate((node1, node2) =>
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Any(counter => counter.Key == "CPU Usage") &&
                        node1.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value <=
                        node2.NodeMetrics.RemoteNodeHealth.PerformanceCounters
                            .Single(counter => counter.Key == "CPU Usage").Value
                            ? node1
                            : node2);
                    _resolverCache.Set(key, node.NodeMetrics.Id, _cacheEntryOptions);
                    var persistentCacheToken = new CancellationTokenSource();
                    var persistentItem = new LinkedFuncItem<TInput2>(key, itemProducer, persistentCacheToken);
                    node.Dispatch(persistentItem);
                }
                else
                {
                    throw new NotImplementedException(
                        $"{nameof(NodeQueuingStrategy)} of value {ClusterOptions.NodeQueuingStrategy.ToString()} is not implemented.");
                }
            }
            else
            {
                if (_nodes.All(node => !node.NodeMetrics.Alive))
                {
                    const string message = "Could not dispatch item, nodes are offline.";
                    Logger.LogError(message);
                    throw new GrandCentralDispatchException(message);
                }
                else
                {
                    const string message = "Could not dispatch item, nodes are full.";
                    Logger.LogWarning(message);
                }
            }
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
                foreach (var node in _nodes)
                {
                    node?.Dispose();
                }
            }

            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
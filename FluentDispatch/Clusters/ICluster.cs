using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentDispatch.Metrics;
using FluentDispatch.Models;
using FluentDispatch.Nodes;
using FluentDispatch.Options;

namespace FluentDispatch.Clusters
{
    /// <summary>
    /// The cluster which is in charge of distributing the load to the configured nodes.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    public interface IAsyncCluster<TInput, TOutput> : IExposeMetrics, IDisposable
    {
        /// <summary>
        /// Execute an item against the cluster immediately
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="selector"><see cref="Func{TResult}"/></param>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> ExecuteAsync(Func<TInput, Task<TOutput>> selector, TInput item,
            CancellationToken cancellationToken);

        /// <summary>
        /// Dispatch an item to the cluster using a dispatcher queue (using the Window parameter from <see cref="ClusterOptions"/>)
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="selector"></param>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> DispatchAsync(Func<TInput, Task<TOutput>> selector, TInput item,
            CancellationToken cancellationToken);

        /// <summary>
        /// Execute an item against the cluster immediately
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> ExecuteAsync(TInput item, CancellationToken cancellationToken);

        /// <summary>
        /// Dispatch an item to the cluster using a dispatcher queue (using the Window parameter from <see cref="ClusterOptions"/>)
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> DispatchAsync(TInput item, CancellationToken cancellationToken);

        /// <summary>
        /// <see cref="ClusterMetrics"/>
        /// </summary>
        ClusterMetrics ClusterMetrics { get; }

        /// <summary>
        /// Get nodes from the cluster
        /// </summary>
        IReadOnlyCollection<INode> Nodes { get; }

        /// <summary>
        /// Add a node
        /// </summary>
        /// <param name="host">Specified host if the node is a remote node</param>
        void AddNode(Host host = null);

        /// <summary>
        /// Delete a node
        /// </summary>
        /// <param name="nodeId">Node Id</param>
        void DeleteNode(Guid nodeId);
    }

    /// <summary>
    /// The cluster which is in charge of distributing the load to the configured nodes.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    public interface ICluster<in TInput> : IExposeMetrics, IDisposable
    {
        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="item">The item to process</param>
        void Dispatch(TInput item);

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="item">The item to process</param>
        void Dispatch(Func<TInput> item);

        /// <summary>
        /// Stop the processing for the cluster.
        /// </summary>
        void Stop();

        /// <summary>
        /// Resume the processing for the cluster.
        /// </summary>
        void Resume();

        /// <summary>
        /// <see cref="ClusterMetrics"/>
        /// </summary>
        ClusterMetrics ClusterMetrics { get; }

        /// <summary>
        /// Get nodes from the cluster
        /// </summary>
        IReadOnlyCollection<INode> Nodes { get; }

        /// <summary>
        /// Add a node
        /// </summary>
        /// <param name="host">Specified host if the node is a remote node</param>
        void AddNode(Host host = null);

        /// <summary>
        /// Delete a node
        /// </summary>
        /// <param name="nodeId">Node Id</param>
        void DeleteNode(Guid nodeId);
    }

    /// <summary>
    /// The cluster which is in charge of distributing the load to the configured nodes.
    /// </summary>
    /// <typeparam name="TInput1"></typeparam>
    /// <typeparam name="TInput2"></typeparam>
    public interface ICluster<in TInput1, in TInput2> : IExposeMetrics, IDisposable
    {
        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="item1">The item to process</param>
        void Dispatch(Guid key, TInput1 item1);

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="item2">The item to process</param>
        void Dispatch(Guid key, TInput2 item2);

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="item1">The item to process</param>
        void Dispatch(Guid key, Func<TInput1> item1);

        /// <summary>
        /// Dispatch an item to the cluster, to be processed by the configured nodes.
        /// </summary>
        /// <remarks>
        /// This won't block the calling thread and this won't never throw any exception.
        /// A retry and circuit breaker policies will gracefully handle non successful attempts.
        /// </remarks>
        /// <param name="key">The item identifier</param>
        /// <param name="item2">The item to process</param>
        void Dispatch(Guid key, Func<TInput2> item2);

        /// <summary>
        /// Stop the processing for the cluster.
        /// </summary>
        void Stop();

        /// <summary>
        /// Resume the processing for the cluster.
        /// </summary>
        void Resume();

        /// <summary>
        /// <see cref="ClusterMetrics"/>
        /// </summary>
        ClusterMetrics ClusterMetrics { get; }

        /// <summary>
        /// Get nodes from the cluster
        /// </summary>
        IReadOnlyCollection<INode> Nodes { get; }

        /// <summary>
        /// Add a node
        /// </summary>
        /// <param name="host">Specified host if the node is a remote node</param>
        void AddNode(Host host = null);

        /// <summary>
        /// Delete a node
        /// </summary>
        /// <param name="nodeId">Node Id</param>
        void DeleteNode(Guid nodeId);
    }
}
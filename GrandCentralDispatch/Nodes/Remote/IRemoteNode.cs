using System;
using System.Threading.Tasks;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Nodes.Remote
{
    /// <summary>
    /// Node which process items.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal interface IRemoteNode<TInput, TOutput> : IDisposable
    {
        /// <summary>
        /// Execute a <see cref="Func{TInput}"/> against a remote the node.
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="item"><see cref="TInput"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> ExecuteAsync(TInput item);

        /// <summary>
        /// <see cref="NodeMetrics"/>
        /// </summary>
        NodeMetrics NodeMetrics { get; }
    }
}
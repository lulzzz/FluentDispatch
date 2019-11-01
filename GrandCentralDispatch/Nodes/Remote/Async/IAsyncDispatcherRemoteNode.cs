using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Nodes.Remote.Async
{
    /// <summary>
    /// Node which process items remotely.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal interface IAsyncDispatcherRemoteNode<in TInput, TOutput> : INode, IDisposable
    {
        /// <summary>
        /// Dispatch a <see cref="Func{TInput}"/> to the node.
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> ExecuteAsync(TInput item, CancellationToken cancellationToken);
    }
}
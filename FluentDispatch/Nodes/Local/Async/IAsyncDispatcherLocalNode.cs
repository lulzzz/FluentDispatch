using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDispatch.Nodes.Local.Async
{
    /// <summary>
    /// Node which process items.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal interface IAsyncDispatcherLocalNode<TInput, TOutput> : INode, IDisposable
    {
        /// <summary>
        /// Execute a <see cref="Func{TInput}"/> against the local node using a selector predicate.
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="selector"><see cref="Func{TResult}"/></param>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> ExecuteAsync(Func<TInput, Task<TOutput>> selector, TInput item,
            CancellationToken cancellationToken);
    }
}
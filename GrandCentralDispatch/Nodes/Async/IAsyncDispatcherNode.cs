using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Nodes.Async
{
    /// <summary>
    /// Node which process items.
    /// </summary>
    /// <typeparam name="TInput"></typeparam>
    /// <typeparam name="TOutput"></typeparam>
    internal interface IAsyncDispatcherNode<TInput, TOutput> : IDisposable
    {
        /// <summary>
        /// Dispatch a <see cref="Func{TInput}"/> to the node.
        /// </summary>
        /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
        /// <param name="selector"></param>
        /// <param name="item"><see cref="TInput"/></param>
        /// <returns><see cref="TOutput"/></returns>
        Task<TOutput> DispatchAsync(Func<TInput, Task<TOutput>> selector, TInput item);

        /// <summary>
        /// <see cref="NodeMetrics"/>
        /// </summary>
        NodeMetrics NodeMetrics { get; }
    }
}
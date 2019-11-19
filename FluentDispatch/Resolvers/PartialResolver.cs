using System;
using System.Threading;
using System.Threading.Tasks;
using FluentDispatch.Models;

namespace FluentDispatch.Resolvers
{
    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each new incoming item
    /// </summary>
    /// <typeparam name="TPartial1"><see cref="TPartial1"/></typeparam>
    /// <typeparam name="TPartial2"><see cref="TPartial2"/></typeparam>
    public class PartialResolver<TPartial1, TPartial2> : FuncPartialResolver<TPartial1, TPartial2>
    {
        /// <summary>
        /// Resolve <see cref="Process"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TPartial1, NodeMetrics, CancellationToken, Task<TPartial2>> GetItemFunc()
        {
            return Process;
        }

        /// <summary>
        /// Override this method to apply a specific process to each item
        /// </summary>
        /// <param name="item"><see cref="TPartial1"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected virtual Task<TPartial2> Process(TPartial1 item, NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }


    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class FuncPartialResolver<TInput, TOutput> : IResolver
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, CancellationToken, Task<TOutput>> GetItemFunc();
    }
}
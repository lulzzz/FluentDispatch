using System;
using System.Threading;
using System.Threading.Tasks;
using FluentDispatch.Models;

namespace FluentDispatch.Resolvers
{
    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each new incoming item
    /// </summary>
    /// <typeparam name="TOutput1"><see cref="TOutput1"/></typeparam>
    /// <typeparam name="TOutput2"><see cref="TOutput2"/></typeparam>
    public class DualResolver<TOutput1, TOutput2> : FuncResolver<TOutput1, TOutput2>
    {
        /// <summary>
        /// Resolve <see cref="Process"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TOutput1, TOutput2, NodeMetrics, CancellationToken, Task> GetItemFunc()
        {
            return Process;
        }

        /// <summary>
        /// Override this method to apply a specific process to each item
        /// </summary>
        /// <param name="item1"><see cref="TOutput1"/></param>
        /// <param name="item2"><see cref="TOutput2"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected virtual Task Process(TOutput1 item1, TOutput2 item2, NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
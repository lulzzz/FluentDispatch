using System;
using System.Threading;
using System.Threading.Tasks;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Resolvers
{
    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    public abstract class FuncResolver<TInput> : IResolver
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, CancellationToken, Task> GetItemFunc();
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each incoming new item
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    public class Resolver<TInput> : FuncResolver<TInput>
    {
        /// <summary>
        /// Resolve <see cref="Process"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TInput, NodeMetrics, CancellationToken, Task> GetItemFunc()
        {
            return Process;
        }

        /// <summary>
        /// Override this method to apply a specific process to each incoming item
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected virtual Task Process(TInput item, NodeMetrics nodeMetrics, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput1"/> and <see cref="TInput2"/>
    /// </summary>
    /// <typeparam name="TInput1"><see cref="TInput1"/></typeparam>
    /// <typeparam name="TInput2"><see cref="TInput2"/></typeparam>
    public abstract class FuncResolver<TInput1, TInput2> : IResolver
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput1, TInput2, NodeMetrics, CancellationToken, Task> GetItemFunc();
    }
}
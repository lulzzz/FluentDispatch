using System;
using System.Threading;
using System.Threading.Tasks;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Remote;

namespace GrandCentralDispatch.Resolvers
{
    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    public abstract class FuncResolver<TInput>
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
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    public abstract class FuncRemoteResolver<TInput> : ServiceBase<IRemoteContract<TInput>>, IRemoteContract<TInput>
    {
        /// <summary>
        /// Retrieve the remote processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc();

        public virtual UnaryResult<Nil> ProcessRemotely(TInput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each incoming new item
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    public class RemoteResolver<TInput> : FuncRemoteResolver<TInput>
    {
        /// <summary>
        /// Resolve <see cref="GetItemRemoteFunc"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TInput, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc()
        {
            return ProcessRemotely;
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput1"/> and <see cref="TInput2"/>
    /// </summary>
    /// <typeparam name="TInput1"><see cref="TInput1"/></typeparam>
    /// <typeparam name="TInput2"><see cref="TInput2"/></typeparam>
    public abstract class FuncResolver<TInput1, TInput2>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput1, TInput2, NodeMetrics, CancellationToken, Task> GetItemFunc();
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput1"/> and <see cref="TInput2"/>
    /// </summary>
    /// <typeparam name="TInput1"><see cref="TInput1"/></typeparam>
    /// <typeparam name="TInput2"><see cref="TInput2"/></typeparam>
    public abstract class FuncRemoteResolver<TInput1, TInput2> : ServiceBase<IRemoteContract<TInput1, TInput2>>,
        IRemoteContract<TInput1, TInput2>
    {
        /// <summary>
        /// Retrieve the remote processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput1, TInput2, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item1"><see cref="TInput1"/></param>
        /// <param name="item2"><see cref="TInput2"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        public virtual UnaryResult<Nil> ProcessRemotely(TInput1 item1, TInput2 item2,
            NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class FuncPartialResolver<TInput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, CancellationToken, Task<TOutput>> GetItemFunc();
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class RemoteFuncPartialResolver<TInput, TOutput> :
        ServiceBase<IOutputRemoteContract<TInput, TOutput>>,
        IOutputRemoteContract<TInput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, UnaryResult<TOutput>> GetItemRemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        public virtual UnaryResult<TOutput> ProcessRemotely(TInput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }
}
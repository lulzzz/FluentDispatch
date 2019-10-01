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
    /// Resolve the processing function which will be applied to each <see cref="TOutput"/>
    /// </summary>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class FuncResolver<TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TOutput, NodeMetrics, CancellationToken, Task> GetItemFunc();
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each incoming new item
    /// </summary>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public class Resolver<TOutput> : FuncResolver<TOutput>
    {
        /// <summary>
        /// Resolve <see cref="Process"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TOutput, NodeMetrics, CancellationToken, Task> GetItemFunc()
        {
            return Process;
        }

        /// <summary>
        /// Override this method to apply a specific process to each incoming item
        /// </summary>
        /// <param name="item"><see cref="TOutput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <param name="cancellationToken"><see cref="CancellationToken"/></param>
        /// <returns><see cref="Task"/></returns>
        protected virtual Task Process(TOutput item, NodeMetrics nodeMetrics, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TOutput"/>
    /// </summary>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class FuncRemoteResolver<TOutput> : ServiceBase<IRemoteContract<TOutput>>, IRemoteContract<TOutput>
    {
        /// <summary>
        /// Retrieve the remote processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TOutput, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc();

        public virtual UnaryResult<Nil> ProcessRemotely(TOutput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each incoming new item
    /// </summary>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public class RemoteResolver<TOutput> : FuncRemoteResolver<TOutput>
    {
        /// <summary>
        /// Resolve <see cref="GetItemRemoteFunc"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TOutput, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc()
        {
            return ProcessRemotely;
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TOutput1"/> and <see cref="TOutput2"/>
    /// </summary>
    /// <typeparam name="TOutput1"><see cref="TOutput1"/></typeparam>
    /// <typeparam name="TOutput2"><see cref="TOutput2"/></typeparam>
    public abstract class FuncResolver<TOutput1, TOutput2>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TOutput1, TOutput2, NodeMetrics, CancellationToken, Task> GetItemFunc();
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TOutput1"/> and <see cref="TOutput2"/>
    /// </summary>
    /// <typeparam name="TOutput1"><see cref="TOutput1"/></typeparam>
    /// <typeparam name="TOutput2"><see cref="TOutput2"/></typeparam>
    public abstract class FuncRemoteResolver<TOutput1, TOutput2> : ServiceBase<IRemoteContract<TOutput1, TOutput2>>,
        IRemoteContract<TOutput1, TOutput2>
    {
        /// <summary>
        /// Retrieve the remote processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TOutput1, TOutput2, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item1"><see cref="TOutput1"/></param>
        /// <param name="item2"><see cref="TOutput2"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{TResponse}"/></returns>
        public virtual UnaryResult<Nil> ProcessRemotely(TOutput1 item1, TOutput2 item2,
            NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TOutput"/>
    /// </summary>
    /// <typeparam name="TPartialOutput"><see cref="TPartialOutput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class FuncPartialResolver<TPartialOutput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TPartialOutput, NodeMetrics, CancellationToken, Task<TOutput>> GetItemFunc();
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TPartialOutput"/>
    /// </summary>
    /// <typeparam name="TPartialOutput"><see cref="TPartialOutput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class Item1RemoteFuncPartialResolver<TPartialOutput, TOutput> :
        ServiceBase<IItem1RemotePartialContract<TPartialOutput, TOutput>>,
        IItem1RemotePartialContract<TPartialOutput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TPartialOutput, NodeMetrics, UnaryResult<TOutput>> GetItem1RemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item"><see cref="TOutput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{TResponse}"/></returns>
        public virtual UnaryResult<TOutput> ProcessItem1Remotely(TPartialOutput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TPartialOutput"/>
    /// </summary>
    /// <typeparam name="TPartialOutput"><see cref="TPartialOutput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class Item2RemoteFuncPartialResolver<TPartialOutput, TOutput> :
        ServiceBase<IItem2RemotePartialContract<TPartialOutput, TOutput>>,
        IItem2RemotePartialContract<TPartialOutput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TPartialOutput, NodeMetrics, UnaryResult<TOutput>> GetItem2RemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item"><see cref="TPartialOutput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        /// <returns><see cref="UnaryResult{TResponse}"/></returns>
        public virtual UnaryResult<TOutput> ProcessItem2Remotely(TPartialOutput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }
}
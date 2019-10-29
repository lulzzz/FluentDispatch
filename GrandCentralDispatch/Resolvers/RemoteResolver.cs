using System;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Remote;
using MagicOnion;
using MagicOnion.Server;
using MessagePack;

namespace GrandCentralDispatch.Resolvers
{
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
    /// Generic resolver which enable overriding the default behavior for each incoming item
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    public class RemoteResolver<TInput> : FuncRemoteResolver<TInput>
    {
        /// <summary>
        /// Resolve <see cref="FuncRemoteResolver{TInput}.ProcessRemotely"/>
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
    public abstract class FuncRemoteResolver<TInput1, TInput2> : ServiceBase<IRemoteContract<TInput1, TInput2>>,
        IRemoteContract<TInput1, TInput2>
    {
        /// <summary>
        /// Retrieve the remote processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput1, TInput2, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each item remotely
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
    /// Generic resolver which enable overriding the default behavior for each new incoming item
    /// </summary>
    /// <typeparam name="TPartial1"><see cref="TPartial1"/></typeparam>
    /// <typeparam name="TPartial2"><see cref="TPartial2"/></typeparam>
    public class Item1RemotePartialResolver<TPartial1, TPartial2> : Item1RemoteFuncPartialResolver<TPartial1, TPartial2>
    {
        /// <summary>
        /// Resolve <see cref="Item1RemoteFuncPartialResolver{TPartial1,TPartial2}.ProcessItem1Remotely"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TPartial1, NodeMetrics, UnaryResult<TPartial2>> GetItem1RemoteFunc()
        {
            return ProcessItem1Remotely;
        }
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each new incoming item
    /// </summary>
    /// <typeparam name="TPartial1"><see cref="TPartial1"/></typeparam>
    /// <typeparam name="TPartial2"><see cref="TPartial2"/></typeparam>
    public class Item2RemotePartialResolver<TPartial1, TPartial2> : Item2RemoteFuncPartialResolver<TPartial1, TPartial2>
    {
        /// <summary>
        /// Resolve <see cref="Item2RemoteFuncPartialResolver{TPartial1,TPartial2}.ProcessItem2Remotely"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TPartial1, NodeMetrics, UnaryResult<TPartial2>> GetItem2RemoteFunc()
        {
            return ProcessItem2Remotely;
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class Item1RemoteFuncPartialResolver<TInput, TOutput> :
        ServiceBase<IOutputItem1RemoteContract<TInput, TOutput>>,
        IOutputItem1RemoteContract<TInput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, UnaryResult<TOutput>> GetItem1RemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        public virtual UnaryResult<TOutput> ProcessItem1Remotely(TInput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class Item2RemoteFuncPartialResolver<TInput, TOutput> :
        ServiceBase<IOutputItem2RemoteContract<TInput, TOutput>>,
        IOutputItem2RemoteContract<TInput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, UnaryResult<TOutput>> GetItem2RemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each incoming item remotely
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        public virtual UnaryResult<TOutput> ProcessItem2Remotely(TInput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Resolve the processing function which will be applied to each <see cref="TInput"/>
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public abstract class RemoteFuncResolver<TInput, TOutput> :
        ServiceBase<IOutputRemoteContract<TInput, TOutput>>,
        IOutputRemoteContract<TInput, TOutput>
    {
        /// <summary>
        /// Retrieve the processing function
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public abstract Func<TInput, NodeMetrics, UnaryResult<TOutput>> GetRemoteFunc();

        /// <summary>
        /// Override this method to apply a specific process to each item remotely
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="nodeMetrics"><see cref="NodeMetrics"/></param>
        public virtual UnaryResult<TOutput> ProcessRemotely(TInput item, NodeMetrics nodeMetrics)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each new incoming item
    /// </summary>
    /// <typeparam name="TInput"><see cref="TInput"/></typeparam>
    /// <typeparam name="TOutput"><see cref="TOutput"/></typeparam>
    public class RemoteResolver<TInput, TOutput> : RemoteFuncResolver<TInput, TOutput>
    {
        /// <summary>
        /// Resolve <see cref="RemoteFuncResolver{TInput,TOutput}.ProcessRemotely"/>
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TInput, NodeMetrics, UnaryResult<TOutput>> GetRemoteFunc()
        {
            return ProcessRemotely;
        }
    }

    /// <summary>
    /// Generic resolver which enable overriding the default behavior for each new incoming item
    /// </summary>
    /// <typeparam name="TOutput1"><see cref="TOutput1"/></typeparam>
    /// <typeparam name="TOutput2"><see cref="TOutput2"/></typeparam>
    public class DualFuncRemoteResolver<TOutput1, TOutput2> : FuncRemoteResolver<TOutput1, TOutput2>
    {
        /// <summary>
        /// Resolve
        /// </summary>
        /// <returns><see cref="Func{TResult}"/></returns>
        public override Func<TOutput1, TOutput2, NodeMetrics, UnaryResult<Nil>> GetItemRemoteFunc()
        {
            return ProcessRemotely;
        }
    }
}
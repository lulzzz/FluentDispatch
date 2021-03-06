﻿using MagicOnion;
using MessagePack;
using FluentDispatch.Models;
using FluentDispatch.Resolvers;

namespace FluentDispatch.Remote
{
    public interface IRemoteContract<TInput> : IService<IRemoteContract<TInput>>, IResolver
    {
        UnaryResult<Nil> ProcessRemotely(TInput item, NodeMetrics nodeMetrics);
    }

    public interface IRemoteContract<TInput1, TInput2> : IService<IRemoteContract<TInput1, TInput2>>, IResolver
    {
        UnaryResult<Nil> ProcessRemotely(TInput1 item1, TInput2 item2, NodeMetrics nodeMetrics);
    }

    public interface
        IOutputRemoteContract<TInput, TOutput> : IService<
            IOutputRemoteContract<TInput, TOutput>>, IResolver
    {
        UnaryResult<TOutput> ProcessRemotely(TInput item, NodeMetrics nodeMetrics);
    }

    public interface
        IOutputItem1RemoteContract<TInput, TOutput> : IService<
            IOutputItem1RemoteContract<TInput, TOutput>>, IResolver
    {
        UnaryResult<TOutput> ProcessItem1Remotely(TInput item, NodeMetrics nodeMetrics);
    }

    public interface
        IOutputItem2RemoteContract<TInput, TOutput> : IService<
            IOutputItem2RemoteContract<TInput, TOutput>>, IResolver
    {
        UnaryResult<TOutput> ProcessItem2Remotely(TInput item, NodeMetrics nodeMetrics);
    }
}
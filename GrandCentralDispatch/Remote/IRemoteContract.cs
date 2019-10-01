using MagicOnion;
using MessagePack;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Remote
{
    public interface IRemoteContract<TOutput> : IService<IRemoteContract<TOutput>>
    {
        UnaryResult<Nil> ProcessRemotely(TOutput item, NodeMetrics nodeMetrics);
    }

    public interface IRemoteContract<TOutput1, TOutput2> : IService<IRemoteContract<TOutput1, TOutput2>>
    {
        UnaryResult<Nil> ProcessRemotely(TOutput1 item1, TOutput2 item2, NodeMetrics nodeMetrics);
    }

    public interface
        IItem1RemotePartialContract<TPartialOutput, TOutput> : IService<
            IItem1RemotePartialContract<TPartialOutput, TOutput>>
    {
        UnaryResult<TOutput> ProcessItem1Remotely(TPartialOutput item, NodeMetrics nodeMetrics);
    }

    public interface
        IItem2RemotePartialContract<TPartialOutput, TOutput> : IService<
            IItem2RemotePartialContract<TPartialOutput, TOutput>>
    {
        UnaryResult<TOutput> ProcessItem2Remotely(TPartialOutput item, NodeMetrics nodeMetrics);
    }
}
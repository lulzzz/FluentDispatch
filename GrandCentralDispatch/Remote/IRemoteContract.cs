using MagicOnion;
using MessagePack;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Remote
{
    public interface IRemoteContract<TInput> : IService<IRemoteContract<TInput>>
    {
        UnaryResult<Nil> ProcessRemotely(TInput item, NodeMetrics nodeMetrics);
    }

    public interface IRemoteContract<TInput1, TInput2> : IService<IRemoteContract<TInput1, TInput2>>
    {
        UnaryResult<Nil> ProcessRemotely(TInput1 item1, TInput2 item2, NodeMetrics nodeMetrics);
    }

    public interface
        IOutputRemoteContract<TInput, TOutput> : IService<
            IOutputRemoteContract<TInput, TOutput>>
    {
        UnaryResult<TOutput> ProcessRemotely(TInput item, NodeMetrics nodeMetrics);
    }
}
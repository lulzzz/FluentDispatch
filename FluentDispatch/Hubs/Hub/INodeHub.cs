using System;
using System.Threading.Tasks;
using MagicOnion;
using FluentDispatch.Hubs.Receiver;

namespace FluentDispatch.Hubs.Hub
{
    public interface INodeHub : IStreamingHub<INodeHub, INodeReceiver>
    {
        Task HeartBeatAsync(Guid nodeGuid);
    }
}

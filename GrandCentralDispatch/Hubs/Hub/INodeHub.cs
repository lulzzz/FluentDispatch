using System;
using System.Threading.Tasks;
using MagicOnion;
using GrandCentralDispatch.Hubs.Receiver;

namespace GrandCentralDispatch.Hubs.Hub
{
    public interface INodeHub : IStreamingHub<INodeHub, INodeReceiver>
    {
        Task HeartBeatAsync(Guid nodeGuid);
    }
}

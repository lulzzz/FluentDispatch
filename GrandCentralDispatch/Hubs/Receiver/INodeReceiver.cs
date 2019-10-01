using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Hubs.Receiver
{
    public interface INodeReceiver
    {
        void OnHeartBeat(RemoteNodeHealth health);
    }
}

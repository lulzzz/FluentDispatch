using FluentDispatch.Models;

namespace FluentDispatch.Hubs.Receiver
{
    public interface INodeReceiver
    {
        void OnHeartBeat(RemoteNodeHealth health);
    }
}

using System.Reactive.Subjects;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Hubs.Receiver
{
    internal class NodeReceiver : INodeReceiver, IRemoteNodeSubject
    {
        private readonly ILogger _logger;

        public NodeReceiver(ILogger logger)
        {
            _logger = logger;
            RemoteNodeHealthSubject = new Subject<RemoteNodeHealth>();
        }

        public void OnHeartBeat(RemoteNodeHealth health)
        {
            _logger.LogTrace("OnHeartBeat");
            RemoteNodeHealthSubject.OnNext(health);
        }

        public ISubject<RemoteNodeHealth> RemoteNodeHealthSubject { get; }
    }
}
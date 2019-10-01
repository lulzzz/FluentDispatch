using System.Reactive.Subjects;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Hubs.Receiver
{
    internal interface IRemoteNodeSubject
    {
        ISubject<RemoteNodeHealth> RemoteNodeHealthSubject { get; }
    }
}

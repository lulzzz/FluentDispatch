using System.Reactive.Subjects;
using FluentDispatch.Models;

namespace FluentDispatch.Hubs.Receiver
{
    internal interface IRemoteNodeSubject
    {
        ISubject<RemoteNodeHealth> RemoteNodeHealthSubject { get; }
    }
}

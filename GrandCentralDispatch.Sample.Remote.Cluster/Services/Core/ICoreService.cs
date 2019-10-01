using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Sample.Remote.Cluster.Services.Core
{
    public interface ICoreService
    {
        CancellationTokenSource CancellationTokenSource { get; }
        Task Start();
        void Stop();
    }
}

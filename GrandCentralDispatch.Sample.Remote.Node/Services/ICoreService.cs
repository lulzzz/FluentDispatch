using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Sample.Remote.Node.Services
{
    public interface ICoreService
    {
        CancellationTokenSource CancellationTokenSource { get; }
        Task Start();
        void Stop();
    }
}

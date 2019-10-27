using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Models
{
    internal class RemoteItem<TInput, TOutput>
    {
        public TaskCompletionSource<TOutput> TaskCompletionSource { get; }

        public TInput Item { get; }

        public CancellationToken CancellationToken { get; }

        public RemoteItem(TaskCompletionSource<TOutput> taskCompletionSource, TInput item, CancellationToken cancellationToken)
        {
            TaskCompletionSource = taskCompletionSource;
            Item = item;
            CancellationToken = cancellationToken;
        }
    }
}
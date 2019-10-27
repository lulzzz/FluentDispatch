using System.Threading.Tasks;

namespace GrandCentralDispatch.Models
{
    internal class RemoteItem<TInput, TOutput>
    {
        public TaskCompletionSource<TOutput> TaskCompletionSource { get; }

        public TInput Item { get; }

        public RemoteItem(TaskCompletionSource<TOutput> taskCompletionSource, TInput item)
        {
            TaskCompletionSource = taskCompletionSource;
            Item = item;
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Models
{
    internal class AsyncItem<TInput, TOutput>
    {
        public TaskCompletionSource<TOutput> TaskCompletionSource { get; }

        public Func<TInput, Task<TOutput>> Selector { get; }

        public TInput Item { get; }

        public CancellationToken CancellationToken { get; }

        public AsyncItem(TaskCompletionSource<TOutput> taskCompletionSource, Func<TInput, Task<TOutput>> selector,
            TInput item, CancellationToken cancellationToken)
        {
            TaskCompletionSource = taskCompletionSource;
            Selector = selector;
            Item = item;
            CancellationToken = cancellationToken;
        }
    }
}
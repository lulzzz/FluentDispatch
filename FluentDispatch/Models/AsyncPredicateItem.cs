using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDispatch.Models
{
    internal class AsyncPredicateItem<TInput, TOutput> : AsyncItem<TInput, TOutput>
    {
        public Func<TInput, Task<TOutput>> Selector { get; }

        public AsyncPredicateItem(TaskCompletionSource<TOutput> taskCompletionSource, Func<TInput, Task<TOutput>> selector,
            TInput item, CancellationToken cancellationToken) : base(taskCompletionSource, item, cancellationToken)
        {
            Selector = selector;
        }
    }
}
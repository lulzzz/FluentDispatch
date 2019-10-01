using System;
using System.Threading;

namespace GrandCentralDispatch.Models
{
    internal class LinkedItem<T>
    {
        public Guid Key { get; }

        public T Entity { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public LinkedItem(Guid key, T entity, CancellationTokenSource cts)
        {
            Key = key;
            Entity = entity;
            CancellationTokenSource = cts;
        }
    }

    internal class LinkedFuncItem<T>
    {
        public Guid Key { get; }

        public Func<T> Entity { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public LinkedFuncItem(Guid key, Func<T> entity, CancellationTokenSource cts)
        {
            Key = key;
            Entity = entity;
            CancellationTokenSource = cts;
        }
    }
}
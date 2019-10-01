using System.Threading;

namespace GrandCentralDispatch.Models
{
    internal class PersistentItem<T>
    {
        public T Entity { get; }

        public CancellationTokenSource CancellationTokenSource { get; }

        public PersistentItem(T entity, CancellationTokenSource cts)
        {
            Entity = entity;
            CancellationTokenSource = cts;
        }
    }
}
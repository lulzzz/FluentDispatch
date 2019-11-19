using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDispatch.Cache
{
    public interface IAppCache
    {
        ICacheProvider CacheProvider { get; }

        Task AddItemAsync<TInput>(TInput item, CancellationToken ct);
        Task AddItem1Async<TInput1>(string key, TInput1 item, CancellationToken ct);
        Task AddItem2Async<TInput2>(string key, TInput2 item, CancellationToken ct);
    }
}
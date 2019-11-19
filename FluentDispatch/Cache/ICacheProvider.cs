using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FluentDispatch.Cache
{
    public interface ICacheProvider
    {
        Task AddItemAsync<TInput>(TInput item, CancellationToken ct);
        Task AddItem1Async<TInput1>(string key, TInput1 item, CancellationToken ct);
        Task AddItem2Async<TInput2>(string key, TInput2 item, CancellationToken ct);
        Task<IEnumerable<TOutput>> RetrieveItemsAsync<TOutput>();
        Task<IEnumerable<(string key, TOutput1 item1)>> RetrieveItems1Async<TOutput1>();
        Task<IEnumerable<(string key, TOutput2 item2)>> RetrieveItems2Async<TOutput2>();
        Task FlushDatabaseAsync();
    }
}
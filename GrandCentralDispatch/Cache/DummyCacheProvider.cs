using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Cache
{
    public class DummyCacheProvider : ICacheProvider
    {
        public Task AddItem1Async<TInput1>(string key, TInput1 item, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task AddItem2Async<TInput2>(string key, TInput2 item, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task AddItemAsync<TInput>(TInput item, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task FlushDatabaseAsync()
        {
            return Task.CompletedTask;
        }

        public Task<IEnumerable<(string key, TOutput1 item1)>> RetrieveItems1Async<TOutput1>()
        {
            return Task.FromResult(Enumerable.Empty<(string key, TOutput1 item1)>());
        }

        public Task<IEnumerable<(string key, TOutput2 item2)>> RetrieveItems2Async<TOutput2>()
        {
            return Task.FromResult(Enumerable.Empty<(string key, TOutput2 item2)>());
        }

        public Task<IEnumerable<TOutput>> RetrieveItemsAsync<TOutput>()
        {
            return Task.FromResult(Enumerable.Empty<TOutput>());
        }
    }
}
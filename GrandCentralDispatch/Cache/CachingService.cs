using System;
using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Cache
{
    internal class CachingService : IAppCache
    {
        private readonly Lazy<ICacheProvider> _cacheProvider;

        private CachingService(Func<ICacheProvider> cacheProviderFactory)
        {
            if (cacheProviderFactory == null) throw new ArgumentNullException(nameof(cacheProviderFactory));
            _cacheProvider = new Lazy<ICacheProvider>(cacheProviderFactory);
        }

        public CachingService(ICacheProvider cache) : this(() => cache)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
        }

        public virtual ICacheProvider CacheProvider => _cacheProvider.Value;

        public virtual Task AddItemAsync<TInput>(TInput item, CancellationToken ct)
        {
            return CacheProvider.AddItemAsync(item, ct);
        }

        public virtual Task AddItem1Async<TInput1>(string key, TInput1 item,
            CancellationToken ct)
        {
            return CacheProvider.AddItem1Async(key, item, ct);
        }

        public virtual Task AddItem2Async<TInput2>(string key, TInput2 item,
            CancellationToken ct)
        {
            return CacheProvider.AddItem2Async(key, item, ct);
        }
    }
}
using System;
using System.Threading.Tasks;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Processors.Async
{
    internal interface IAsyncProcessor<TInput, TOutput> : IDisposable
    {
        Task<TOutput> AddAsync(AsyncItem<TInput, TOutput> item);
    }
}

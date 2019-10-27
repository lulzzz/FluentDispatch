using System;
using System.Threading.Tasks;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Processors.Remote
{
    internal interface IRemoteProcessor<TInput, TOutput> : IDisposable
    {
        /// <summary>
        /// Process an incoming item
        /// </summary>
        /// <param name="item"><see cref="TInput"/></param>
        /// <param name="item"><see cref="RemoteItem{TInput,TOutput}"/></param>
        Task<TOutput> ProcessAsync(RemoteItem<TInput, TOutput> item);
    }
}
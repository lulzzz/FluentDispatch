using System;
using System.Threading.Tasks;
using Nest;

namespace FluentDispatch.Contract.Services.ElasticSearch
{
    public interface IElasticSearchService
    {
        Lazy<Task<IElasticClient>> Client { get; }
    }
}

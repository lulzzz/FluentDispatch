using System;
using System.Threading.Tasks;
using Nest;

namespace GrandCentralDispatch.Contract.Services.ElasticSearch
{
    public interface IElasticSearchService
    {
        Lazy<Task<IElasticClient>> Client { get; }
    }
}

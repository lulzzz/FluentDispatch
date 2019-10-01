using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GrandCentralDispatch.Sample.Web.Helpers
{
    public interface IRestClient : IDisposable
    {
        Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cToken);
    }
}

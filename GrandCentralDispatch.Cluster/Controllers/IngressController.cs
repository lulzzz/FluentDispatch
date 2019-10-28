using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Contract.Models;

namespace GrandCentralDispatch.Cluster.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IngressController : ControllerBase
    {
        private readonly ICluster<Payload, Uri> _cluster;
        private readonly IRemoteCluster<string, string> _remoteCluster;

        /// <summary>
        /// <see cref="ICluster{T}"/>
        /// </summary>
        /// <param name="cluster"></param>
        /// <param name="remoteCluster"></param>
        public IngressController(ICluster<Payload, Uri> cluster, IRemoteCluster<string, string> remoteCluster)
        {
            _cluster = cluster;
            _remoteCluster = remoteCluster;
        }

        /// <summary>
        /// Dispatch received messages to the cluster
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Payload payload)
        {
            var requestIdentifier = Guid.NewGuid();

            // We dispatch the func to be executed on a dedicated thread, the Linq expression will not be computed on this thread
            _cluster.Dispatch(requestIdentifier,
                () => Uri.IsWellFormedUriString(payload.Body, UriKind.Absolute)
                    ? new Uri(payload.Body)
                    : throw new Exception());

            // We post directly the value because there is nothing to compute here
            _cluster.Dispatch(requestIdentifier, payload);

            var headers = Request.Headers.ToList();
            var computedResult = await _remoteCluster.ExecuteAsync(headers.Select(header =>
            {
                header.Deconstruct(out var key, out var value);
                return $"{key}:{value}";
            }).Aggregate((header1, header2) => string.Join(';', header1, header2)), CancellationToken.None);
            return Ok(computedResult);
        }
    }
}
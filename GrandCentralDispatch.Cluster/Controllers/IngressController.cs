using System;
using Microsoft.AspNetCore.Mvc;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Contract.Models;

namespace GrandCentralDispatch.Cluster.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class IngressController : ControllerBase
    {
        private readonly ICluster<Payload, Uri> _requestCluster;

        /// <summary>
        /// <see cref="ICluster{T}"/>
        /// </summary>
        /// <param name="requestCluster"></param>
        public IngressController(ICluster<Payload, Uri> requestCluster)
        {
            _requestCluster = requestCluster;
        }

        /// <summary>
        /// Dispatch received messages to the cluster
        /// </summary>
        /// <param name="payload"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult Post([FromBody] Payload payload)
        {
            var requestIdentifier = Guid.NewGuid();

            // We dispatch the func to be executed on a dedicated thread, the Linq expression will not be computed on this thread
            _requestCluster.Dispatch(requestIdentifier,
                () => Uri.IsWellFormedUriString(payload.Body, UriKind.Absolute)
                    ? new Uri(payload.Body)
                    : throw new Exception());

            // We post directly the value because there is nothing to compute here
            _requestCluster.Dispatch(requestIdentifier, payload);
            return Ok();
        }
    }
}
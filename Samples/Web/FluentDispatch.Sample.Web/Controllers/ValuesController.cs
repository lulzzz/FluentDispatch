using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using FluentDispatch.Clusters;

namespace FluentDispatch.Sample.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly ICluster<IPAddress, string> _requestCluster;

        /// <summary>
        /// The cluster is resolved by the .NET Core DI
        /// </summary>
        /// <param name="requestCluster"></param>
        public ValuesController(ICluster<IPAddress, string> requestCluster)
        {
            _requestCluster = requestCluster;
        }

        // GET api/values
        [HttpGet]
        public IActionResult Get()
        {
            var requestIdentifier = Guid.NewGuid();

            // We dispatch the func to be executed on a dedicated thread, the Linq expression will not be computed on this thread
            var headers = Request.Headers.ToList();
            _requestCluster.Dispatch(requestIdentifier, () => headers.Select(header =>
            {
                header.Deconstruct(out var key, out var value);
                return $"{key}:{value}";
            }).Aggregate((header1, header2) => string.Join(';', header1, header2)));

            // We post directly the value because there is nothing to compute here
            _requestCluster.Dispatch(requestIdentifier, Request.HttpContext.Connection.RemoteIpAddress);

            return Ok();
        }
    }
}
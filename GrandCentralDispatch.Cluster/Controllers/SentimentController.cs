using System;
using Microsoft.AspNetCore.Mvc;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Contract.Models;

namespace GrandCentralDispatch.Cluster.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SentimentController : ControllerBase
    {
        private readonly ICluster<MovieDetails, MovieReview> _cluster;

        /// <summary>
        /// <see cref="ICluster{T}"/>
        /// </summary>
        /// <param name="cluster"></param>
        public SentimentController(ICluster<MovieDetails, MovieReview> cluster)
        {
            _cluster = cluster;
        }

        /// <summary>
        /// Dispatch received messages to the cluster
        /// </summary>
        /// <param name="input"><see cref="Input"/></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult Post([FromBody] Input input)
        {
            var requestIdentifier = Guid.NewGuid();

            // We dispatch the func to be executed on a dedicated thread, the Linq expression will not be computed on this thread
            _cluster.Dispatch(requestIdentifier,
                () => new MovieReview
                {
                    ReviewText = input.ReviewText
                });

            // We post directly the value because there is nothing to compute here
            _cluster.Dispatch(requestIdentifier, new MovieDetails
            {
                Title = input.Title
            });
            return Ok();
        }
    }
}
using System;
using Microsoft.AspNetCore.Mvc;
using FluentDispatch.Clusters;
using FluentDispatch.Contract.Models;

namespace FluentDispatch.Cluster.Controllers
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

            _cluster.Dispatch(requestIdentifier,
                () => new MovieReview
                {
                    ReviewText = input.ReviewText
                });

            _cluster.Dispatch(requestIdentifier, new MovieDetails
            {
                Title = input.Title
            });
            return Ok();
        }
    }
}
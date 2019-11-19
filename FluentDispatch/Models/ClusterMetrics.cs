using System;

namespace FluentDispatch.Models
{
    public class ClusterMetrics
    {
        /// <summary>
        /// <see cref="ClusterMetrics"/>
        /// </summary>
        /// <param name="id">Cluster identifier</param>
        public ClusterMetrics(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Node identifier
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Current number of items processed per second by the cluster.
        /// </summary>
        public double CurrentThroughput { get; internal set; }

        /// <summary>
        /// <see cref="ClusterHealth"/>
        /// </summary>
        public ClusterHealth Health { get; internal set; }
    }
}
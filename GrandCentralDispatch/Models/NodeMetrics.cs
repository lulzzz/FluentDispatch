using System;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using MessagePack;

namespace GrandCentralDispatch.Models
{
    /// <summary>
    /// Node informations
    /// </summary>
    [MessagePackObject(true)]
    public class NodeMetrics
    {
        /// <summary>
        /// <see cref="NodeMetrics"/>
        /// </summary>
        /// <param name="id">Node identifier</param>
        public NodeMetrics(Guid id)
        {
            Id = id;
            Alive = true;
            RefreshSubject = new Subject<Guid>();
            RemoteNodeHealth = new RemoteNodeHealth();
        }

        /// <summary>
        /// Node identifier
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// If node alive
        /// </summary>
        public bool Alive { get; internal set; }

        /// <summary>
        /// Indicates how many items have been submitted to the node
        /// </summary>
        public long TotalItemsProcessed { get; internal set; }

        /// <summary>
        /// Indicates how many items have been submitted to the node
        /// </summary>
        public long ItemsEvicted { get; internal set; }

        /// <summary>
        /// Current number of items processed per second by the node
        /// </summary>
        public double CurrentThroughput { get; internal set; }

        /// <summary>
        /// Indicates if current node is full.
        /// </summary>
        public bool Full { get; internal set; }

        /// <summary>
        /// Get current buffer size
        /// </summary>
        public int BufferSize { get; internal set; }

        /// <summary>
        /// <see cref="RemoteNodeHealth"/>
        /// </summary>
        public RemoteNodeHealth RemoteNodeHealth { get; internal set; }

        [IgnoreDataMember] public ISubject<Guid> RefreshSubject { get; internal set; }
    }
}
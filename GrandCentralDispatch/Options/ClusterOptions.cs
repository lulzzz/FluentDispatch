using System;
using System.Collections.Generic;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Options
{
    /// <summary>
    /// Set the options to apply to the cluster.
    /// <see cref="Cluster{T}"/>
    /// </summary>
    public class ClusterOptions
    {
        /// <summary>
        /// <see cref="ClusterOptions"/>
        /// </summary>
        public ClusterOptions()
        {

        }

        /// <summary>
        /// Initialize cluster options.
        /// </summary>
        /// <param name="clusterSize">The size of the cluster, i.e the amount of nodes available for the cluster to dispatch work.</param>
        /// <param name="persistenceEnabled">If enabled, items will fail-over from a cluster which has been non-gracefully shutdown.</param>
        /// <param name="maxItemsInPersistentCache">Maximum items which can stay in persistence cache.</param>
        /// <param name="limitCpuUsage">Limit the CPU usage.</param>
        /// <param name="nodeQueuingStrategy">How items are queued to nodes, <see cref="NodeQueuingStrategy"/></param>
        /// <param name="nodeThrottling">Set how many items per <see cref="Window"/> unit of time will be processed by a node.</param>
        /// <param name="evictItemsWhenNodesAreFull">Evicts dispatched items when nodes are full.</param>
        /// <param name="retryAttempt">Specify how many times node have to retry a failed item process.</param>
        /// <param name="windowInMilliseconds">Unit of time of the sliding windows, in which <see cref="nodeThrottling"/> items will be processed.</param>
        /// <param name="clusterProcessingType">Set the processing type of the cluster, i.e how items are dequeued from each node to be processed.</param>
        /// <param name="executeRemotely">Execute calls using RPC to remote clients</param>
        /// <param name="hosts">Remote hosts</param>
        /// <param name="enablePerformanceCounters">Enable Windows Performance Counters</param>
        public ClusterOptions(int clusterSize = 10,
            bool persistenceEnabled = false,
            int maxItemsInPersistentCache = 1_000_000,
            int limitCpuUsage = 100,
            NodeQueuingStrategy nodeQueuingStrategy = NodeQueuingStrategy.BestEffort,
            int nodeThrottling = 1000,
            bool evictItemsWhenNodesAreFull = true,
            int retryAttempt = 3,
            int windowInMilliseconds = 1000,
            ClusterProcessingType clusterProcessingType = ClusterProcessingType.Parallel,
            bool executeRemotely = false,
            ISet<Host> hosts = null,
            bool enablePerformanceCounters = false)
        {
            ClusterSize = clusterSize;
            PersistenceEnabled = persistenceEnabled;
            MaxItemsInPersistentCache = maxItemsInPersistentCache;
            LimitCpuUsage = limitCpuUsage < 0 || limitCpuUsage > 100 ? 100 : limitCpuUsage;
            NodeQueuingStrategy = nodeQueuingStrategy;
            EvictItemsWhenNodesAreFull = evictItemsWhenNodesAreFull;
            NodeThrottling = nodeThrottling;
            RetryAttempt = retryAttempt;
            Window = TimeSpan.FromMilliseconds(windowInMilliseconds);
            ClusterProcessingType = clusterProcessingType;
            ExecuteRemotely = executeRemotely;
            Hosts = hosts;
            EnablePerformanceCounters = enablePerformanceCounters;
        }

        /// <summary>
        /// The size of the cluster, i.e the amount of nodes available for the cluster to dispatch work. This is ignored if <see cref="ExecuteRemotely"/> is true.
        /// </summary>
        /// <remarks>
        /// The more the nodes, the more threads are created, which translates to better throughput.
        /// But beware of not exceeding a decent value. Too many threads will have a performance hit on the machine.
        /// </remarks>
        public int ClusterSize { get; set; } = 10;

        /// <summary>
        /// If enabled, items will fail-over from a cluster which has been non-gracefully shutdown.
        /// </summary>
        /// <remarks>
        /// This can have a negative impact on performances, especially if items are dispatched using <see cref="Func{TResult}"/>
        /// Warning: Your contract objects must be decorated with <see cref="MessagePack.MessagePackObjectAttribute"/>
        /// </remarks>
        public bool PersistenceEnabled { get; set; } = false;

        /// <summary>
        /// Maximum items which can stay in persistence cache.
        /// </summary>
        /// <remarks>
        /// The more items, the more cluster will be able to recover but it'll have an impact on memory and disk usage.
        /// </remarks>
        public int MaxItemsInPersistentCache { get; set; } = 1_000_000;

        /// <summary>
        /// Limit the CPU usage
        /// </summary>
        public int LimitCpuUsage { get; set; } = 100;

        /// <summary>
        /// How items are queued to nodes
        /// </summary>
        /// <remarks>
        /// Best effort will queued items to nodes which are the least populated, meaning that each node will share the same amount work
        /// Randomized will queued items to nodes randomly, meaning that some nodes will have to work more than others
        /// </remarks>
        public NodeQueuingStrategy NodeQueuingStrategy { get; set; } = NodeQueuingStrategy.BestEffort;

        /// <summary>
        /// The throttling applied to each node, i.e how many items per second are processed per node.
        /// </summary>
        /// <remarks>
        /// Increasing this value will allow more items to be processed per second.
        /// But beware of not exceeding a decent value. Too elements will likely overwhelm the node processor, especially if <see cref="ClusterProcessingType"/> is set to Parallel.
        /// </remarks>
        public int NodeThrottling { get; set; } = 1000;

        /// <summary>
        /// Evicts dispatched items when nodes are full.
        /// <remarks>If false, items will be postponed afterwards when possible.</remarks>
        /// </summary>
        public bool EvictItemsWhenNodesAreFull { get; set; } = true;

        /// <summary>
        /// Specify how many times node have to retry a failed item process.
        /// </summary>
        public int RetryAttempt { get; set; } = 3;

        /// <summary>
        /// Unit of time of the sliding windows, in which <see cref="NodeThrottling"/> items will be processed.
        /// </summary>
        public TimeSpan Window { get; set; } = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// Set the processing type of the cluster, i.e how items are dequeued from each node to be processed.
        /// </summary>
        /// <remarks>
        /// Parallel will generally be far more efficient than Sequential, especially on CPU with many cores.
        /// Usually, Parallel eats more CPU than memory and Sequential eats more memory than CPU.
        /// </remarks>
        public ClusterProcessingType ClusterProcessingType { get; set; } = ClusterProcessingType.Parallel;

        /// <summary>
        /// Execute resolvers on remote clients, using <see cref="Hosts"/>
        /// </summary>
        public bool ExecuteRemotely { get; set; } = false;

        /// <summary>
        /// Definition of remote hosts, key is machine name or IP and value is port.
        /// </summary>
        public ISet<Host> Hosts { get; set; } = new HashSet<Host>();

        /// <summary>
        /// Execute Windows Performance Counters
        /// </summary>
        public bool EnablePerformanceCounters { get; set; } = false;
    }

    /// <summary>
    /// Set the way how items are dequeued from node.
    /// </summary>
    public enum ClusterProcessingType
    {
        /// <summary>
        /// Items are dequeued sequentially from nodes.
        /// </summary>
        Sequential = 1,

        /// <summary>
        /// Items are dequeued from nodes in parallel.
        /// </summary>
        Parallel = 2
    }

    /// <summary>
    /// Set the way how items are queued to nodes.
    /// </summary>
    public enum NodeQueuingStrategy
    {
        /// <summary>
        /// Items are queued randomly to nodes.
        /// </summary>
        Randomized = 1,

        /// <summary>
        /// Items are queued to least populated nodes first.
        /// </summary>
        BestEffort = 2,

        /// <summary>
        /// Items are queued to healthiest nodes first
        /// </summary>
        Healthiest = 3
    }
}
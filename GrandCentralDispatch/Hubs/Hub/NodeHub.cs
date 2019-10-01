using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Helpers;
using GrandCentralDispatch.Hubs.Receiver;
using GrandCentralDispatch.Models;

namespace GrandCentralDispatch.Hubs.Hub
{
    [GroupConfiguration(typeof(ConcurrentDictionaryGroupRepositoryFactory))]
    public class NodeHub : StreamingHubBase<INodeHub, INodeReceiver>, INodeHub
    {
        private readonly ILogger _logger;
        private readonly IDictionary<Guid, IGroup> _cluster;
        private readonly ICollection<PerformanceCounter> _performanceCounters;

        public NodeHub(ILoggerFactory logger)
        {
            _logger = logger.CreateLogger<NodeHub>();
            _cluster = new ConcurrentDictionary<Guid, IGroup>();
            _performanceCounters = Helper.GetPerformanceCounters();
        }

        public async Task HeartBeatAsync(Guid nodeGuid)
        {
            _logger.LogTrace($"Requesting heartbeat, node {nodeGuid.ToString()}...");
            if (!_cluster.ContainsKey(nodeGuid))
            {
                var groupCreation = await Group.TryAddAsync(nodeGuid.ToString(), int.MaxValue, true);
                if (groupCreation.Item1)
                {
                    _cluster.Add(nodeGuid, groupCreation.Item2);
                }
            }

            var remoteNodeHealth = new RemoteNodeHealth
            {
                MachineName = Environment.MachineName
            };

            foreach (var performanceCounter in _performanceCounters)
            {
                remoteNodeHealth.PerformanceCounters.Add(performanceCounter.CounterName,
                    performanceCounter.NextValue());
            }

            foreach (var group in _cluster)
            {
                Broadcast(group.Value).OnHeartBeat(remoteNodeHealth);
            }
        }
    }
}
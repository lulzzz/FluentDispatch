using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Logging;
using GrandCentralDispatch.Hubs.Receiver;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.PerformanceCounters;
using System.Linq;

namespace GrandCentralDispatch.Hubs.Hub
{
    [GroupConfiguration(typeof(ConcurrentDictionaryGroupRepositoryFactory))]
    public class NodeHub : StreamingHubBase<INodeHub, INodeReceiver>, INodeHub
    {
        private readonly ILogger _logger;
        private readonly IDictionary<Guid, IGroup> _cluster;

        /// <summary>
        /// Monitor performance counters
        /// </summary>
        private readonly CounterMonitor _counterMonitor = new CounterMonitor();

        /// <summary>
        /// Performance counters
        /// </summary>
        private readonly IDictionary<string, double> _performanceCounters = new Dictionary<string, double>();

        public NodeHub(ILoggerFactory logger)
        {
            _logger = logger.CreateLogger<NodeHub>();
            _cluster = new ConcurrentDictionary<Guid, IGroup>();
            _counterMonitor.CounterUpdate += OnCounterUpdate;
            var monitorTask = new Task(() =>
            {
                try
                {
                    _counterMonitor.Start(_logger);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error while listening to counters: {ex.Message}");
                }
            });
            monitorTask.Start();
        }

        /// <summary>
        /// Update performance counters
        /// </summary>
        /// <param name="args"><see cref="CounterEventArgs"/></param>
        private void OnCounterUpdate(CounterEventArgs args)
        {
            if (!_performanceCounters.ContainsKey(args.DisplayName))
            {
                _performanceCounters.Add(args.DisplayName, args.Value);
            }
            else
            {
                _performanceCounters[args.DisplayName] = args.Value;
            }
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

            foreach (var performanceCounter in _performanceCounters.ToList())
            {
                remoteNodeHealth.PerformanceCounters.Add(performanceCounter.Key,
                    Convert.ToInt64(performanceCounter.Value));
            }

            foreach (var group in _cluster)
            {
                Broadcast(group.Value).OnHeartBeat(remoteNodeHealth);
            }
        }
    }
}
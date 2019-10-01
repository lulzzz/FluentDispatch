using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ShellProgressBar;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Models;
using GrandCentralDispatch.Resolvers;
using GrandCentralDispatch.Sample.Models;

namespace GrandCentralDispatch.Sample
{
    internal sealed class Resolver : Resolver<Message>
    {
        private static int _tracker;

        private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() =>
        {
            var seed = (int) (Environment.TickCount & 0xFFFFFF00 | (byte) (Interlocked.Increment(ref _tracker) % 255));
            var random = new Random(seed);
            return random;
        });

        private readonly Func<ICluster<Message>> _clusterFunc;

        private readonly ProgressBarOptions _nodeProgressBarOptions;

        private readonly ConcurrentDictionary<Guid, ChildProgressBar> _nodes;

        private readonly ProgressBar _clusterProgressBar;

        public Resolver(Func<ICluster<Message>> clusterFunc, int messageCount, int nodeCount)
        {
            _clusterFunc = clusterFunc;
            var clusterProgressBarOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Yellow,
                ForegroundColorDone = ConsoleColor.DarkGreen,
                BackgroundColor = ConsoleColor.DarkGray,
                BackgroundCharacter = '\u2593'
            };

            _nodeProgressBarOptions = new ProgressBarOptions
            {
                ForegroundColor = ConsoleColor.Green,
                BackgroundColor = ConsoleColor.DarkGreen,
                ProgressCharacter = '─'
            };

            _nodes = new ConcurrentDictionary<Guid, ChildProgressBar>();
            _clusterProgressBar =
                new ProgressBar(messageCount, $"Firing {messageCount} messages on {nodeCount} nodes...",
                    clusterProgressBarOptions);
        }

        // TODO: This is where you should write your code which will process every new message
        protected override async Task Process(Message message, NodeMetrics nodeMetrics,
            CancellationToken cancellationToken)
        {
            if (!_nodes.ContainsKey(nodeMetrics.Id) &&
                _nodes.TryAdd(nodeMetrics.Id,
                    _clusterProgressBar.Spawn(0, $"Node {nodeMetrics.Id} pending process...",
                        _nodeProgressBarOptions)))
            {
                // New child progress bar added
            }

            if (_nodes.TryGetValue(nodeMetrics.Id, out var nodeProgressBar))
            {
                // No need to take care of handling exception here, the circuit breaker and retry policies take care of it on a higher level
                if (Random.Value.Next(0, 100) == 50)
                {
                    // Uncomment to see how resiliency is ensured
                    // throw new Exception("I'm a bad exception and I'm trying to break your execution.");
                }

                // Simulate quite long processing time for each message, but could be stressful I/O, networking, ...
                await Task.Delay(125, cancellationToken);
                if (nodeProgressBar.CurrentTick == nodeProgressBar.MaxTicks)
                {
                    nodeProgressBar.MaxTicks = (int) nodeMetrics.TotalItemsProcessed;
                }

                nodeProgressBar.Tick(
                    $"Node {nodeMetrics.Id} ({nodeMetrics.CurrentThroughput} messages/s) processed: {message.Body}");
            }

            // Tick when a message has been processed
            _clusterProgressBar.Tick(
                $"New message processed by the cluster ({_clusterFunc().ClusterMetrics.CurrentThroughput} messages/s): {message.Body}");
        }
    }
}
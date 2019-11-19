using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Options;
using FluentDispatch.Clusters;
using FluentDispatch.Options;
using FluentDispatch.Sample.Models;

namespace FluentDispatch.Sample
{
    class Program
    {
        private static ICluster<Message> _cluster;
        private static readonly Progress<double> OverallProgress = new Progress<double>();

        static void Main(string[] args)
        {
            const int messageCount = 10000;
            const int nodeCount = 10;

            var fixture = new Fixture {RepeatCount = messageCount};
            var messages = fixture.Create<Message[]>();

            OverallProgress.ProgressChanged += OnOverallProgressChanged;
            using (_cluster = new Cluster<Message>(
                // TODO: Create your own resolver to apply a specific process to each received message
                new Resolver(() => _cluster, messageCount, nodeCount),
                // Feel free to update values here, decreasing cluster size will increase processing time, ...
                new OptionsWrapper<ClusterOptions>(new ClusterOptions(clusterSize: nodeCount,
                    persistenceEnabled: false,
                    maxItemsInPersistentCache: 1_000_000,
                    limitCpuUsage: 100,
                    nodeQueuingStrategy: NodeQueuingStrategy.Randomized,
                    nodeThrottling: 1000,
                    evictItemsWhenNodesAreFull: false,
                    retryAttempt: 3,
                    windowInMilliseconds: 1000,
                    clusterProcessingType: ClusterProcessingType.Parallel)),
                new OptionsWrapper<CircuitBreakerOptions>(new CircuitBreakerOptions(
                    circuitBreakerFailureThreshold: 0.5d,
                    circuitBreakerSamplingDurationInMilliseconds: 5000,
                    circuitBreakerDurationOfBreakInMilliseconds: 30000,
                    circuitBreakerMinimumThroughput: 20)),
                OverallProgress,
                new CancellationTokenSource()))
            {
                // TODO: This is where you send your messages to be processed afterward, asynchronously, in parallel, on dedicated background worker threads
                // Broadcast messages to the cluster
                Parallel.ForEach(messages, message =>
                {
                    // This call does not block the calling thread
                    _cluster.Dispatch(() =>
                    {
                        // This very slow method get "posted" to the cluster, so that it never executes on the main thread
                        long FindPrimeNumber(int n)
                        {
                            var count = 0;
                            long a = 2;
                            while (count < n)
                            {
                                long b = 2;
                                var prime = 1;
                                while (b * b <= a)
                                {
                                    if (a % b == 0)
                                    {
                                        prime = 0;
                                        break;
                                    }

                                    b++;
                                }

                                if (prime > 0)
                                {
                                    count++;
                                }

                                a++;
                            }

                            return (--a);
                        }

                        var primeNumber = FindPrimeNumber(1000);
                        return message;
                    });
                });

                Console.ReadLine();
            }
        }

        private static void OnOverallProgressChanged(object sender, double e)
        {
            // Console.WriteLine($"Overall progress of the current bulk: {Math.Round(e * 100)}%");
            // Show progress of the current bulk
            // This output is not compatible with ProgressBar (because both prints in the same console), either comment _progressBar.Tick or this code
        }
    }
}
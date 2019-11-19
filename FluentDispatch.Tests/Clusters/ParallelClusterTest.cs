using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FluentDispatch.Clusters;
using FluentDispatch.Options;
using FluentDispatch.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace FluentDispatch.Tests.Clusters
{
    public class ParallelClusterFixture : IDisposable
    {
        public readonly ICluster<Message> Cluster;
        private readonly ConcurrentBag<string> _bodies = new ConcurrentBag<string>();
        public readonly Progress<double> Progress = new Progress<double>();

        public ParallelClusterFixture()
        {
            Cluster = new Cluster<Message>(
                new Resolver(_bodies),
                new OptionsWrapper<ClusterOptions>(new ClusterOptions(clusterSize: 10,
                    limitCpuUsage: 100,
                    nodeQueuingStrategy: NodeQueuingStrategy.BestEffort,
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
                Progress,
                new CancellationTokenSource());
        }

        public IEnumerable<string> GetBodies()
        {
            return _bodies.ToList();
        }

        public void Dispose()
        {
            Cluster.Dispose();
        }
    }

    public class ParallelClusterTestData : TheoryData<Message[]>
    {
        public ParallelClusterTestData()
        {
            // This fixture can take up to 5 seconds to create
            var fixture = new Fixture {RepeatCount = 1000};
            var records = fixture.Create<Message[]>();
            Add(records);
        }
    }

    public class ParallelClusterTest : IClassFixture<ParallelClusterFixture>
    {
        private readonly ParallelClusterFixture _parallelClusterFixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public ParallelClusterTest(ParallelClusterFixture parallelClusterFixture, ITestOutputHelper testOutputHelper)
        {
            _parallelClusterFixture = parallelClusterFixture;
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [ClassData(typeof(ParallelClusterTestData))]
        public async Task Cluster_Should_Dispatch_Messages_In_Parallel(Message[] messages)
        {
            // Act
            var watcher = Stopwatch.StartNew();
            _parallelClusterFixture.Progress.ProgressChanged += OnProgressChanged;
            foreach (var message in messages)
            {
                _parallelClusterFixture.Cluster.Dispatch(message);
            }

            watcher.Stop();
            watcher.ElapsedMilliseconds.Should().BeLessThan(100,
                "dispatch process should not block the thread and be atomic operation.");

            // Assert
            // Necessary delay to wait for the underlying processors to handle messages to the dedicated background threads (sliding window is 1 second here, but adding 1 more to be sure not flickering the tests)
            await Task.Delay(TimeSpan.FromSeconds(2));
            var bodies = _parallelClusterFixture.GetBodies();
            bodies.Should().BeEquivalentTo(messages.Select(message => message.Body));
        }

        private void OnProgressChanged(object sender, double e)
        {
            _testOutputHelper.WriteLine($"Progress of the bulk changed: {Math.Round(e * 100)}%");
        }
    }
}
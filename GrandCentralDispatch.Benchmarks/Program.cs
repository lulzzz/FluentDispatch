using System;
using System.Runtime.CompilerServices;
using System.Threading;
using AutoFixture;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GrandCentralDispatch.Benchmarks.Models;
using GrandCentralDispatch.Clusters;
using GrandCentralDispatch.Options;

namespace GrandCentralDispatch.Benchmarks
{
    [ShortRunJob]
    [Config(typeof(Config))]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [MemoryDiagnoser]
    public class ClusterBench
    {
        private class Config : ManualConfig
        {
            public Config()
            {
                Add(CsvMeasurementsExporter.Default);
                Add(RPlotExporter.Default);
            }
        }

        private Message[] _messages;
        private ICluster<Message> _parallelCluster;
        private ICluster<Message> _sequentialCluster;
        private readonly Progress<double> _progress = new Progress<double>();

        [Params(1, 50, 100)] public int MaxCpuUsage;

        [GlobalSetup]
        public void Setup()
        {
            var fixture = new Fixture {RepeatCount = 1000};
            _messages = fixture.Create<Message[]>();
            _parallelCluster = new Cluster<Message>(
                new Resolver(),
                new OptionsWrapper<ClusterOptions>(new ClusterOptions(clusterSize: 10,
                    limitCpuUsage: MaxCpuUsage,
                    nodeQueuingStrategy: NodeQueuingStrategy.BestEffort,
                    nodeThrottling: 10,
                    evictItemsWhenNodesAreFull: false,
                    retryAttempt: 3,
                    windowInMilliseconds: 2500,
                    clusterProcessingType: ClusterProcessingType.Parallel)),
                new OptionsWrapper<CircuitBreakerOptions>(new CircuitBreakerOptions(
                    circuitBreakerFailureThreshold: 0.5d,
                    circuitBreakerSamplingDurationInMilliseconds: 5000,
                    circuitBreakerDurationOfBreakInMilliseconds: 30000,
                    circuitBreakerMinimumThroughput: 20)),
                _progress,
                new CancellationTokenSource());

            _sequentialCluster = new Cluster<Message>(
                new Resolver(),
                new OptionsWrapper<ClusterOptions>(new ClusterOptions(clusterSize: 10,
                    limitCpuUsage: MaxCpuUsage,
                    nodeQueuingStrategy: NodeQueuingStrategy.BestEffort,
                    nodeThrottling: 10,
                    evictItemsWhenNodesAreFull: false,
                    retryAttempt: 3,
                    windowInMilliseconds: 2500,
                    clusterProcessingType: ClusterProcessingType.Sequential)),
                new OptionsWrapper<CircuitBreakerOptions>(new CircuitBreakerOptions(
                    circuitBreakerFailureThreshold: 0.5d,
                    circuitBreakerSamplingDurationInMilliseconds: 5000,
                    circuitBreakerDurationOfBreakInMilliseconds: 30000,
                    circuitBreakerMinimumThroughput: 20)),
                _progress,
                new CancellationTokenSource());
        }

        [Benchmark(Description = "Dispatch messages in parallel")]
        public void Dispatch_And_Process_In_Parallel()
        {
            foreach (var message in _messages)
            {
                _parallelCluster.Dispatch(() =>
                {
                    var primeNumber = FindPrimeNumber(1000);
                    return message;
                });
            }
        }

        [Benchmark(Description = "Dispatch messages sequentially")]
        public void Dispatch_And_Process_Sequentially()
        {
            foreach (var message in _messages)
            {
                _sequentialCluster.Dispatch(() =>
                {
                    var primeNumber = FindPrimeNumber(1000);
                    return message;
                });
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long FindPrimeNumber(int n)
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
    }

    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<ClusterBench>();
        }
    }
}
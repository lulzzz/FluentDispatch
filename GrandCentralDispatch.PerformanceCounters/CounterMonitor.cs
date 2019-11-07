using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Diagnostics.Tools.RuntimeClient;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace GrandCentralDispatch.PerformanceCounters
{
    public class CounterMonitor
    {
        private const ulong EmptySession = 0xffffffff;

        private ulong _sessionId = EmptySession;

        public event Action<CounterEventArgs> CounterUpdate;

        public void Start(ILogger logger)
        {
            var configuration = new SessionConfiguration(
                circularBufferSizeMB: 1000,
                format: EventPipeSerializationFormat.NetTrace,
                providers: GetProviders()
            );

            var ports = EventPipeClient.ListAvailablePorts();
            logger.LogDebug($"IPC available ports for the performance monitoring session: {string.Join("-", ports)}.");
            using var binaryReader =
                EventPipeClient.CollectTracing(Process.GetCurrentProcess().Id, configuration, out _sessionId);
            using var source = new EventPipeEventSource(binaryReader);
            source.Dynamic.All += ProcessEvents;
            source.Process();
        }

        public void Stop()
        {
            if (_sessionId == EmptySession)
                throw new InvalidOperationException("Start() must be called to start the session");

            EventPipeClient.StopTracing(Process.GetCurrentProcess().Id, _sessionId);
        }

        private void ProcessEvents(TraceEvent data)
        {
            if (data.EventName.Equals("EventCounters"))
            {
                IDictionary<string, object> countersPayload = (IDictionary<string, object>) (data.PayloadValue(0));
                IDictionary<string, object> kvPairs = (IDictionary<string, object>) (countersPayload["Payload"]);
                var name = string.Intern(kvPairs["Name"].ToString());
                var displayName = string.Intern(kvPairs["DisplayName"].ToString());

                var counterType = kvPairs["CounterType"];
                if (counterType.Equals("Sum"))
                {
                    OnSumCounter(name, displayName, kvPairs);
                }
                else if (counterType.Equals("Mean"))
                {
                    OnMeanCounter(name, displayName, kvPairs);
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported counter type '{counterType}'");
                }
            }
        }

        private void OnSumCounter(string name, string displayName, IDictionary<string, object> kvPairs)
        {
            var value = double.Parse(kvPairs["Increment"].ToString());
            CounterUpdate?.Invoke(new CounterEventArgs(name, displayName, CounterType.Sum, value));
        }

        private void OnMeanCounter(string name, string displayName, IDictionary<string, object> kvPairs)
        {
            var value = double.Parse(kvPairs["Mean"].ToString());
            CounterUpdate?.Invoke(new CounterEventArgs(name, displayName, CounterType.Mean, value));
        }

        private IReadOnlyCollection<Provider> GetProviders()
        {
            var providers = new List<Provider>();
            var provider = CounterHelpers.MakeProvider("System.Runtime", 1);
            providers.Add(provider);
            return providers;
        }
    }
}
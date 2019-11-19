using System;
using System.Collections.Concurrent;
using System.Linq;

namespace FluentDispatch.Models
{
    public class ClusterHealth
    {
        /// <summary>
        /// Machine name
        /// </summary>
        public string MachineName { get; }

        public ConcurrentDictionary<string, float> PerformanceCounters { get; }

        public ClusterHealth()
        {
            MachineName = Environment.MachineName;
            PerformanceCounters = new ConcurrentDictionary<string, float>();
        }

        public override string ToString()
        {
            return string.Concat("Performance counters:" + Environment.NewLine, string.Join(Environment.NewLine,
                PerformanceCounters.Select(perfCounter => $"{perfCounter.Key} => {perfCounter.Value}")));
        }
    }
}

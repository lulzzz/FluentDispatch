using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace GrandCentralDispatch.Models
{
    /// <summary>
    /// Remote node informations
    /// </summary>
    [MessagePackObject(true)]
    public class RemoteNodeHealth
    {
        /// <summary>
        /// Performance counters
        /// </summary>
        public Dictionary<string, float> PerformanceCounters { get; set; }

        /// <summary>
        /// Machine name
        /// </summary>
        public string MachineName { get; set; }

        public RemoteNodeHealth()
        {
            PerformanceCounters = new Dictionary<string, float>();
            MachineName = Environment.MachineName;
        }

        public override string ToString()
        {
            return string.Concat("Performance counters:" + Environment.NewLine, string.Join(Environment.NewLine,
                PerformanceCounters.Select(perfCounter => $"{perfCounter.Key} => {perfCounter.Value}")));
        }
    }
}
﻿using System;
using System.Collections.Concurrent;
using System.Linq;

namespace GrandCentralDispatch.Models
{
    public class ClusterHealth
    {
        public ConcurrentDictionary<string, float> PerformanceCounters { get; }

        public ClusterHealth()
        {
            PerformanceCounters = new ConcurrentDictionary<string, float>();
        }

        public override string ToString()
        {
            return string.Concat("Performance counters:" + Environment.NewLine, string.Join(Environment.NewLine,
                PerformanceCounters.Select(perfCounter => $"{perfCounter.Key} => {perfCounter.Value}")));
        }
    }
}
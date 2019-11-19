using System;

namespace FluentDispatch.PerformanceCounters
{
    public class CounterEventArgs : EventArgs
    {
        internal CounterEventArgs(string name, string displayName, CounterType type, double value)
        {
            Counter = name;
            DisplayName = displayName;
            Type = type;
            Value = value;
        }

        public string Counter { get; set; }
        public string DisplayName { get; set; }
        public CounterType Type { get; set; }
        public double Value { get; set; }
    }

    public enum CounterType
    {
        Sum = 0,
        Mean = 1,
    }
}
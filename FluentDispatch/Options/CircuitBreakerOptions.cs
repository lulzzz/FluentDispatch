using System;

namespace FluentDispatch.Options
{
    /// <summary>
    /// Set the options for the circuit break applied to each node.
    /// </summary>
    public class CircuitBreakerOptions
    {
        /// <summary>
        /// <see cref="CircuitBreakerOptions"/>
        /// </summary>
        public CircuitBreakerOptions()
        {

        }

        /// <summary>
        /// Initialize circuit breaker options.
        /// </summary>
        /// <param name="circuitBreakerFailureThreshold">The proportion of failures at which to break. A double between 0 and 1. For example, 0.5 represents break on 50% or more of actions through the circuit resulting in a handled failure.</param>
        /// <param name="circuitBreakerSamplingDurationInMilliseconds">The failure rate is considered for actions over this period. Successes/failures older than the period are discarded from metrics.</param>
        /// <param name="circuitBreakerDurationOfBreakInMilliseconds">The duration of a break.</param>
        /// <param name="circuitBreakerMinimumThroughput">This many calls must have passed through the circuit within the active samplingDuration for the circuit to consider breaking.</param>
        public CircuitBreakerOptions(
            double circuitBreakerFailureThreshold = 0.5d,
            int circuitBreakerSamplingDurationInMilliseconds = 5000,
            int circuitBreakerDurationOfBreakInMilliseconds = 30000,
            int circuitBreakerMinimumThroughput = 20
        )
        {
            CircuitBreakerFailureThreshold = circuitBreakerFailureThreshold;
            CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(circuitBreakerSamplingDurationInMilliseconds);
            CircuitBreakerDurationOfBreak = TimeSpan.FromSeconds(circuitBreakerDurationOfBreakInMilliseconds);
            CircuitBreakerMinimumThroughput = circuitBreakerMinimumThroughput;
        }

        /// <summary>
        /// The proportion of failures at which to break. A double between 0 and 1. For example, 0.5 represents break on 50% or more of actions through the circuit resulting in a handled failure.
        /// </summary>
        public double CircuitBreakerFailureThreshold { get; set; } = 0.5d;

        /// <summary>
        /// The failure rate is considered for actions over this period. Successes/failures older than the period are discarded from metrics.
        /// </summary>
        public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromMilliseconds(5000);

        /// <summary>
        /// The duration of a break.
        /// </summary>
        public TimeSpan CircuitBreakerDurationOfBreak { get; set; } = TimeSpan.FromMilliseconds(30000);

        /// <summary>
        /// This many calls must have passed through the circuit within the active samplingDuration for the circuit to consider breaking.
        /// </summary>
        public int CircuitBreakerMinimumThroughput { get; set; } = 20;
    }
}
using System;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Timestamp struct for duck-typing
    /// Requires boxing, but necessary as we need to duck-type <see cref="Type"/> too
    /// </summary>
    public interface ITimestamp
    {
        /// <summary>
        /// Gets the timestamp type
        /// </summary>
        public TimestampType Type { get; }

        /// <summary>
        /// Gets the UTC DateTime for the timestamp
        /// </summary>
        public DateTime UtcDateTime { get; }
    }
}

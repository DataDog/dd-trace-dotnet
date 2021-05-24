using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Partition for duck-typing
    /// </summary>
    [DuckCopy]
    public struct Partition
    {
        /// <summary>
        /// Gets the int value corresponding to this partition
        /// </summary>
        public int Value;

        /// <summary>
        ///     Gets whether or not this is one of the special
        ///     partition values.
        /// </summary>
        public bool IsSpecial;

        /// <summary>
        /// Based on the original implementation
        /// https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/Partition.cs#L217-L224
        /// </summary>
        /// <returns>A string that represents the Partition object</returns>
        public override string ToString()
        {
            return IsSpecial ? "[Any]" : $"[{Value.ToString()}]";
        }
    }
}

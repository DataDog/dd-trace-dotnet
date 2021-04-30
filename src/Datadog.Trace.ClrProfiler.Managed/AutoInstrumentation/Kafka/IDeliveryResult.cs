namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// DeliveryResult interface for duck-typing
    /// </summary>
    public interface IDeliveryResult
    {
        /// <summary>
        ///     Gets the Kafka partition.
        /// </summary>
        public Partition Partition { get; }

        /// <summary>
        ///     Gets the Kafka offset
        /// </summary>
        public Offset Offset { get; }
    }
}

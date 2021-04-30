namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// ConsumeResult for duck-typing
    /// </summary>
    public interface IConsumeResult
    {
        /// <summary>
        /// Gets the topic
        /// </summary>
        public string Topic { get; }

        /// <summary>
        /// Gets the partition
        /// </summary>
        public Partition Partition { get; }

        /// <summary>
        /// Gets the offset
        /// </summary>
        public Offset Offset { get; }

        /// <summary>
        /// Gets the Kafka record
        /// </summary>
        public IMessage Message { get; }

        /// <summary>
        /// Gets a value indicating whether gets whether the message is a partition EOF
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public bool IsPartitionEOF { get; }
    }
}

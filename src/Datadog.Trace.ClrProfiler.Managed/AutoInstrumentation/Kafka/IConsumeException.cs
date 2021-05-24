namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// ConsumeException interface for duck-typing
    /// </summary>
    public interface IConsumeException
    {
        /// <summary>
        /// Gets the consume result associated with the consume request
        /// </summary>
        public IConsumeResult ConsumerRecord { get; }
    }
}

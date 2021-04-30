namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// TimestampType proxy for duck-typing
    /// https://github.com/confluentinc/confluent-kafka-dotnet/blob/1.5.x/src/Confluent.Kafka/TimestampType.cs
    /// </summary>
    public enum TimestampType
    {
        /// <summary>
        ///     Timestamp type is unknown.
        /// </summary>
        NotAvailable = 0,

        /// <summary>
        ///     Timestamp relates to message creation time as set by a Kafka client.
        /// </summary>
        CreateTime = 1,

        /// <summary>
        ///     Timestamp relates to the time a message was appended to a Kafka log.
        /// </summary>
        LogAppendTime = 2
    }
}

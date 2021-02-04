namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.RabbitMQ
{
    /// <summary>
    /// BasicGetResult interface for ducktyping
    /// </summary>
    public interface IBasicGetResult
    {
        /// <summary>
        /// Gets the message body of the result
        /// </summary>
        IBody Body { get; }

        /// <summary>
        /// Gets the message properties
        /// </summary>
        IBasicProperties BasicProperties { get; }
    }
}

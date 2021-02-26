namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// Message data interface for ducktyping
    /// </summary>
    public interface IMessageData
    {
        /// <summary>
        /// Gets message command and key
        /// </summary>
        public string CommandAndKey { get; }
    }
}

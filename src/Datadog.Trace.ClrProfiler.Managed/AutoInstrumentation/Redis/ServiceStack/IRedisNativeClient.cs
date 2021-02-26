namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.ServiceStack
{
    /// <summary>
    /// Redis native client for duck typing
    /// </summary>
    public interface IRedisNativeClient
    {
        /// <summary>
        /// Gets Client Hostname
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// Gets Client Port
        /// </summary>
        public int Port { get; }
    }
}

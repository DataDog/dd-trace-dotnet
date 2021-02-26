using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// Multiplexer data structure for duck typing
    /// </summary>
    [DuckCopy]
    public struct MultiplexerData
    {
        /// <summary>
        /// Multiplexer configuration
        /// </summary>
        public string Configuration;
    }
}

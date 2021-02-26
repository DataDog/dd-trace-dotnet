using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    /// <summary>
    /// RedisBase interface for ducktyping
    /// </summary>
    public interface IRedisBase
    {
        /// <summary>
        /// Gets multiplexer data structure
        /// </summary>
        [Duck(Name = "multiplexer", Kind = DuckKind.Field)]
        public MultiplexerData Multiplexer { get; }
    }
}

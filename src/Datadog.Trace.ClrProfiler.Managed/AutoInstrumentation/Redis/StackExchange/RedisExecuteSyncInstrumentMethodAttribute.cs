using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    internal class RedisExecuteSyncInstrumentMethodAttribute : InstrumentMethodAttribute
    {
        public RedisExecuteSyncInstrumentMethodAttribute()
        {
            AssemblyNames = new string[] { "StackExchange.Redis", "StackExchange.Redis.StrongName" };
            MethodName = "ExecuteSync";
            ReturnTypeName = "T";
            ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint" };
            MinimumVersion = "1.0.0";
            MaximumVersion = "2.*.*";
            IntegrationName = nameof(IntegrationIds.StackExchangeRedis);
        }
    }
}

// <copyright file="RedisExecuteAsyncInstrumentMethodAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Redis.StackExchange
{
    internal class RedisExecuteAsyncInstrumentMethodAttribute : InstrumentMethodAttribute
    {
        public RedisExecuteAsyncInstrumentMethodAttribute()
        {
            AssemblyNames = new string[] { "StackExchange.Redis", "StackExchange.Redis.StrongName" };
            MethodName = "ExecuteAsync";
            ReturnTypeName = "System.Threading.Tasks.Task`1<T>";
            ParameterTypeNames = new[] { "StackExchange.Redis.Message", "StackExchange.Redis.ResultProcessor`1[!!0]", "StackExchange.Redis.ServerEndPoint" };
            MinimumVersion = "1.0.0";
            MaximumVersion = "2.*.*";
            IntegrationName = nameof(IntegrationIds.StackExchangeRedis);
        }
    }
}

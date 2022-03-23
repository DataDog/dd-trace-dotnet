// <copyright file="DebuggerTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger
{
    internal static class DebuggerTransportStrategy
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebuggerTransportStrategy));

        public static IApiRequestFactory Get()
        {
#if NETCOREAPP
            Log.Information("Using {FactoryType} for debugger transport.", nameof(HttpClientRequestFactory));
            return new HttpClientRequestFactory(AgentHttpHeaderNames.DefaultHeaders);
#else
            Log.Information("Using {FactoryType} for debugger transport.", nameof(ApiWebRequestFactory));
            return new ApiWebRequestFactory(AgentHttpHeaderNames.DefaultHeaders);
#endif
        }
    }
}

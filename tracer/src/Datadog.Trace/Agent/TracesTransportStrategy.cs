// <copyright file="TracesTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent
{
    internal static class TracesTransportStrategy
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Tracer>();

        public static IApiRequestFactory Get(ImmutableExporterSettings settings)
        {
            var strategy = settings.TracesTransport;

            switch (strategy)
            {
                case TracesTransportType.CustomTcpProvider:
                    Log.Information("Using {FactoryType} for trace transport.", nameof(TcpStreamFactory));
                    return new HttpStreamRequestFactory(new TcpStreamFactory(settings.AgentUri.Host, settings.AgentUri.Port), DatadogHttpClient.CreateTraceAgentClient());
                case TracesTransportType.WindowsNamedPipe:
                    Log.Information<string, string, int>("Using {FactoryType} for trace transport, with pipe name {PipeName} and timeout {Timeout}ms.", nameof(NamedPipeClientStreamFactory), settings.TracesPipeName, settings.TracesPipeTimeoutMs);
                    return new HttpStreamRequestFactory(new NamedPipeClientStreamFactory(settings.TracesPipeName, settings.TracesPipeTimeoutMs), DatadogHttpClient.CreateTraceAgentClient());
                case TracesTransportType.UnixDomainSocket:
#if NETCOREAPP3_1_OR_GREATER
                    Log.Information<string, string, int>("Using {FactoryType} for trace transport, with Unix Domain Sockets path {Path} and timeout {Timeout}ms.", nameof(UnixDomainSocketStreamFactory), settings.TracesUnixDomainSocketPath, settings.TracesPipeTimeoutMs);
                    return new HttpStreamRequestFactory(new UnixDomainSocketStreamFactory(settings.TracesUnixDomainSocketPath), DatadogHttpClient.CreateTraceAgentClient());
#else
                    Log.Error("Using Unix Domain Sockets for trace transport is only supported on .NET Core 3.1 and greater. Falling back to default transport.");
                    goto case TracesTransportType.Default;
#endif
                case TracesTransportType.Default:
                default:
#if NETCOREAPP
                    Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
                    return new HttpClientRequestFactory(AgentHttpHeaderNames.DefaultHeaders);
#else
                    Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
                    return new ApiWebRequestFactory(AgentHttpHeaderNames.DefaultHeaders);
#endif
            }
        }
    }
}

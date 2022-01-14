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
                    return new HttpStreamRequestFactory(new TcpStreamFactory(settings.AgentUri.Host, settings.AgentUri.Port), new DatadogHttpClient());
                case TracesTransportType.WindowsNamedPipe:
                    Log.Information<string, string, int>("Using {FactoryType} for trace transport, with pipe name {PipeName} and timeout {Timeout}ms.", nameof(NamedPipeClientStreamFactory), settings.TracesPipeName, settings.TracesPipeTimeoutMs);
                    return new HttpStreamRequestFactory(new NamedPipeClientStreamFactory(settings.TracesPipeName, settings.TracesPipeTimeoutMs), new DatadogHttpClient());
#if NETCOREAPP3_1_OR_GREATER
                case TracesTransportType.UnixDomainSocket:
                    Log.Information<string, string, int>("Using {FactoryType} for trace transport, with UDS path {Path} and timeout {Timeout}ms.", nameof(UnixDomainSocketStreamFactory), settings.TracesUnixDomainSocketPath, settings.TracesPipeTimeoutMs);
                    return new HttpStreamRequestFactory(new UnixDomainSocketStreamFactory(settings.TracesUnixDomainSocketPath), new DatadogHttpClient());
#endif
                case TracesTransportType.Default:
                default:
#if NETCOREAPP
                    Log.Information("Using {FactoryType} for trace transport.", nameof(HttpClientRequestFactory));
                    return new HttpClientRequestFactory();
#else
                    Log.Information("Using {FactoryType} for trace transport.", nameof(ApiWebRequestFactory));
                    return new ApiWebRequestFactory();
#endif
            }
        }
    }
}

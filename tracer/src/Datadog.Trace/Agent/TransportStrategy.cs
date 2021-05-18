// <copyright file="TransportStrategy.cs" company="Datadog">
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
    internal static class TransportStrategy
    {
        public const string DatadogTcp = "DATADOG-TCP";
        public const string DatadogNamedPipes = "DATADOG-NAMED-PIPES";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Tracer>();

        public static IApiRequestFactory Get(TracerSettings settings)
        {
            var strategy = settings.TracesTransport?.ToUpperInvariant();

            switch (strategy)
            {
                case DatadogTcp:
                    Log.Information("Using {FactoryType} for trace transport.", nameof(TcpStreamFactory));
                    return new HttpStreamRequestFactory(new TcpStreamFactory(settings.AgentUri.Host, settings.AgentUri.Port), new DatadogHttpClient());
                case DatadogNamedPipes:
                    Log.Information<string, string, int>("Using {FactoryType} for trace transport, with pipe name {PipeName} and timeout {Timeout}ms.", nameof(NamedPipeClientStreamFactory), settings.TracesPipeName, settings.TracesPipeTimeoutMs);
                    return new HttpStreamRequestFactory(new NamedPipeClientStreamFactory(settings.TracesPipeName, settings.TracesPipeTimeoutMs), new DatadogHttpClient());
                default:
                    // Defer decision to Api logic
                    return null;
            }
        }
    }
}

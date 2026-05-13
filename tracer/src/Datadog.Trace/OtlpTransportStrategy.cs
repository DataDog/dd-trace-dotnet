// <copyright file="OtlpTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent;

internal static class OtlpTransportStrategy
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpTransportStrategy));
    private static readonly Uri Localhost = new Uri("http://localhost");

    public static IApiRequestFactory GetTraces(
        ExporterSettings settings)
    {
        return Get(settings.TracesTransport, settings.OtlpTracesEndpoint, settings.OtlpTracesHeaders ?? [], settings.OtlpTracesTimeoutMs, "traces");
    }

    private static IApiRequestFactory Get(
        AgentTransportType strategy,
        Uri signalEndpoint,
        KeyValuePair<string, string>[] signalHeaders,
        int timeoutMs,
        string productName)
    {
        var httpHeaderHelper = new OtlpHeaderHelper(signalHeaders);

        switch (strategy)
        {
            case AgentTransportType.WindowsNamedPipe:
                Log.Information<string, string?, int>("Using " + nameof(NamedPipeClientStreamFactory) + " for {ProductName} transport, with pipe name {TracesPipeName} and timeout {TracesPipeTimeoutMs}ms.", productName, signalEndpoint.ToString(), timeoutMs);
                return new HttpStreamRequestFactory(
                    new NamedPipeClientStreamFactory(signalEndpoint.ToString(), timeoutMs),
                    new DatadogHttpClient(httpHeaderHelper),
                    Localhost);

            case AgentTransportType.UnixDomainSocket:
                Log.Information("Using " + nameof(SocketHandlerRequestFactory) + " for {ProductName} transport, with UDS path {Path}.", productName, signalEndpoint.ToString());
                // use http://localhost as base endpoint
                return new SocketHandlerRequestFactory(
                    new UnixDomainSocketStreamFactory(signalEndpoint.ToString()),
                    httpHeaderHelper.DefaultHeaders,
                    Localhost);
            case AgentTransportType.Default:
            default:
                Log.Information("Using " + nameof(HttpClientRequestFactory) + " for {ProductName} transport.", productName);
                return new HttpClientRequestFactory(
                    signalEndpoint,
                    httpHeaderHelper.DefaultHeaders,
                    timeout: TimeSpan.FromMilliseconds(timeoutMs));
        }
    }
}
#endif

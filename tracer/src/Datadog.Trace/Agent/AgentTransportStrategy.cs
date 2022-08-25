// <copyright file="AgentTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent;

internal static class AgentTransportStrategy
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AgentTransportStrategy));

    /// <summary>
    /// A generic helper for creating an <see cref="IApiRequestFactory"/> for sending to the agent
    /// Allows customising the <see cref="DatadogHttpClient"/>
    /// </summary>
    /// <param name="settings">The exporter settings defining the agent</param>
    /// <param name="productName">The product this is transport for e.g. 'traces', 'telemetry'.
    /// Used in logging only </param>
    /// <param name="tcpTimeout">The timeout to use in TCP/IP requests</param>
    /// <param name="defaultAgentHeaders">The default headers to add to HttpClient requests</param>
    /// <param name="getHttpHeaderHelper">A func that returns an <see cref="HttpHeaderHelperBase"/> for use
    /// with <see cref="DatadogHttpClient"/></param>
    /// <param name="getBaseEndpoint">A func that returns the endpoint to send requests to </param>
    public static IApiRequestFactory Get(
        ImmutableExporterSettings settings,
        string productName,
        TimeSpan? tcpTimeout,
        KeyValuePair<string, string>[] defaultAgentHeaders,
        Func<HttpHeaderHelperBase> getHttpHeaderHelper,
        Func<Uri> getBaseEndpoint)
    {
        var strategy = settings.TracesTransport;

        switch (strategy)
        {
            case TracesTransportType.WindowsNamedPipe:
                // intentionally using string interpolation, as this is only called once, and avoids array allocation
                Log.Information($"Using {nameof(NamedPipeClientStreamFactory)} for {productName} transport, with pipe name {settings.TracesPipeName} and timeout {settings.TracesPipeTimeoutMs}ms.");
                return new HttpStreamRequestFactory(
                    new NamedPipeClientStreamFactory(settings.TracesPipeName, settings.TracesPipeTimeoutMs),
                    new DatadogHttpClient(getHttpHeaderHelper()),
                    getBaseEndpoint());

            case TracesTransportType.UnixDomainSocket:
#if NET5_0_OR_GREATER
                Log.Information("Using {FactoryType} for {ProductName} transport, with UDS path {Path}.", nameof(SocketHandlerRequestFactory), productName, settings.TracesUnixDomainSocketPath);
                // use http://localhost as base endpoint
                return new SocketHandlerRequestFactory(
                    new UnixDomainSocketStreamFactory(settings.TracesUnixDomainSocketPath),
                    defaultAgentHeaders,
                    getBaseEndpoint());
#elif NETCOREAPP3_1_OR_GREATER
                // intentionally using string interpolation, as this is only called once, and avoids array allocation
                Log.Information($"Using {nameof(UnixDomainSocketStreamFactory)} for {productName} transport, with Unix Domain Sockets path {settings.TracesUnixDomainSocketPath} and timeout {settings.TracesPipeTimeoutMs}ms.");
                return new HttpStreamRequestFactory(
                    new UnixDomainSocketStreamFactory(settings.TracesUnixDomainSocketPath),
                    new DatadogHttpClient(getHttpHeaderHelper()),
                    getBaseEndpoint());
#else
                Log.Error("Using Unix Domain Sockets for {ProductName} transport is only supported on .NET Core 3.1 and greater. Falling back to default transport.", productName);
                goto case TracesTransportType.Default;
#endif
            case TracesTransportType.Default:
            default:
#if NETCOREAPP
                Log.Information("Using {FactoryType} for {ProductName} transport.", nameof(HttpClientRequestFactory), productName);
                return new HttpClientRequestFactory(settings.AgentUri, defaultAgentHeaders, timeout: tcpTimeout);
#else
                Log.Information("Using {FactoryType} for {ProductName} transport.", nameof(ApiWebRequestFactory), productName);
                return new ApiWebRequestFactory(settings.AgentUri, defaultAgentHeaders, timeout: tcpTimeout);
#endif
        }
    }
}

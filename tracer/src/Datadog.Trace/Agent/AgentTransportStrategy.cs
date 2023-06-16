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
    private static readonly Uri Localhost = new Uri("http://localhost");

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
    /// <param name="getBaseEndpoint">A func that returns the endpoint to send requests to for a given "base" endpoint.
    /// The base endpoint will be <see cref="ImmutableExporterSettings.AgentUriInternal" /> for TCP requests and
    /// http://localhost/ for named pipes/UDS</param>
    public static IApiRequestFactory Get(
        ImmutableExporterSettings settings,
        string productName,
        TimeSpan? tcpTimeout,
        KeyValuePair<string, string>[] defaultAgentHeaders,
        Func<HttpHeaderHelperBase> getHttpHeaderHelper,
        Func<Uri, Uri> getBaseEndpoint)
    {
        var strategy = settings.TracesTransport;

        switch (strategy)
        {
            case TracesTransportType.WindowsNamedPipe:
                Log.Information<string, string?, int>("Using " + nameof(NamedPipeClientStreamFactory) + " for {ProductName} transport, with pipe name {TracesPipeName} and timeout {TracesPipeTimeoutMs}ms.", productName, settings.TracesPipeNameInternal, settings.TracesPipeTimeoutMsInternal);
                return new HttpStreamRequestFactory(
                    new NamedPipeClientStreamFactory(settings.TracesPipeNameInternal, settings.TracesPipeTimeoutMsInternal),
                    new DatadogHttpClient(getHttpHeaderHelper()),
                    getBaseEndpoint(Localhost));

            case TracesTransportType.UnixDomainSocket:
#if NET5_0_OR_GREATER
                Log.Information("Using " + nameof(SocketHandlerRequestFactory) + " for {ProductName} transport, with UDS path {Path}.", productName, settings.TracesUnixDomainSocketPathInternal);
                // use http://localhost as base endpoint
                return new SocketHandlerRequestFactory(
                    new UnixDomainSocketStreamFactory(settings.TracesUnixDomainSocketPathInternal),
                    defaultAgentHeaders,
                    getBaseEndpoint(Localhost));
#elif NETCOREAPP3_1_OR_GREATER
                Log.Information<string, string?, int>("Using " + nameof(UnixDomainSocketStreamFactory) + " for {ProductName} transport, with Unix Domain Sockets path {TracesUnixDomainSocketPath} and timeout {TracesPipeTimeoutMs}ms.", productName, settings.TracesUnixDomainSocketPathInternal, settings.TracesPipeTimeoutMsInternal);
                return new HttpStreamRequestFactory(
                    new UnixDomainSocketStreamFactory(settings.TracesUnixDomainSocketPathInternal),
                    new DatadogHttpClient(getHttpHeaderHelper()),
                    getBaseEndpoint(Localhost));
#else
                Log.Error("Using Unix Domain Sockets for {ProductName} transport is only supported on .NET Core 3.1 and greater. Falling back to default transport.", productName);
                goto case TracesTransportType.Default;
#endif
            case TracesTransportType.Default:
            default:
#if NETCOREAPP
                Log.Information("Using " + nameof(HttpClientRequestFactory) + " for {ProductName} transport.", productName);
                return new HttpClientRequestFactory(getBaseEndpoint(settings.AgentUriInternal), defaultAgentHeaders, timeout: tcpTimeout);
#else
                Log.Information("Using " + nameof(ApiWebRequestFactory) + " for {ProductName} transport.", productName);
                return new ApiWebRequestFactory(getBaseEndpoint(settings.AgentUriInternal), defaultAgentHeaders, timeout: tcpTimeout);
#endif
        }
    }
}

// <copyright file="OtlpTransportStrategy.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Agent.StreamFactories;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Agent;

internal static class OtlpTransportStrategy
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpTransportStrategy));
    internal const string UnixDomainSocketPrefix = "unix://";
#if NETCOREAPP3_1_OR_GREATER
    private static readonly Uri Localhost = new Uri("http://localhost");
#endif

    public static IApiRequestFactory GetTraces(
        ExporterSettings settings)
    {
        return Get(settings.OtlpTracesEndpoint, settings.OtlpTracesHeaders ?? [], settings.OtlpTracesTimeoutMs, "traces");
    }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Different implementation types are returned on for different TFMs")]
    private static IApiRequestFactory Get(
        Uri signalEndpoint,
        KeyValuePair<string, string>[] signalHeaders,
        int timeoutMs,
        string productName)
    {
        var httpHeaderHelper = new OtlpHeaderHelper(signalHeaders);

        if (signalEndpoint.OriginalString.StartsWith(UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
        {
#if NET5_0_OR_GREATER
            Log.Information("Using " + nameof(SocketHandlerRequestFactory) + " for {ProductName} transport, with UDS path {Path}.", productName, signalEndpoint.ToString());
            // use http://localhost as base endpoint
            return new SocketHandlerRequestFactory(
                new UnixDomainSocketStreamFactory(signalEndpoint.ToString()),
                httpHeaderHelper.DefaultHeaders,
                Localhost,
                timeout: TimeSpan.FromMilliseconds(timeoutMs));
#elif NETCOREAPP3_1_OR_GREATER
            Log.Information<string, string?, int>("Using " + nameof(UnixDomainSocketStreamFactory) + " for {ProductName} transport, with Unix Domain Sockets path {TracesUnixDomainSocketPath} and timeout {TracesPipeTimeoutMs}ms.", productName, signalEndpoint.ToString(), timeoutMs);
            return new HttpStreamRequestFactory(
                new UnixDomainSocketStreamFactory(signalEndpoint.ToString()),
                new DatadogHttpClient(httpHeaderHelper),
                Localhost);
#else
            Log.Error("Using Unix Domain Sockets for {ProductName} transport is only supported on .NET Core 3.1 and greater. Falling back to default transport.", productName);
#endif
        }

#if NETCOREAPP
        Log.Information("Using " + nameof(HttpClientRequestFactory) + " for {ProductName} transport.", productName);
        return new HttpClientRequestFactory(
            signalEndpoint,
            httpHeaderHelper.DefaultHeaders,
            timeout: TimeSpan.FromMilliseconds(timeoutMs));
#else
        Log.Information("Using " + nameof(ApiWebRequestFactory) + " for {ProductName} transport.", productName);
        return new ApiWebRequestFactory(
            signalEndpoint,
            httpHeaderHelper.DefaultHeaders,
            timeout: TimeSpan.FromMilliseconds(timeoutMs));
#endif
    }
}

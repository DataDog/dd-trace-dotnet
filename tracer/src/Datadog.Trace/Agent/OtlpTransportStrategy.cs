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
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent;

internal static class OtlpTransportStrategy
{
    internal const string TracesGrpcMethodPath = "/opentelemetry.proto.collector.trace.v1.TraceService/Export";
    internal const string MetricsGrpcMethodPath = "/opentelemetry.proto.collector.metrics.v1.MetricsService/Export";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(OtlpTransportStrategy));
    private static readonly Uri Localhost = new Uri("http://localhost");

    public static IApiRequestFactory GetTraces(ExporterSettings settings)
    {
        return Get(settings.OtlpTracesEndpoint, settings.OtlpTracesProtocol, settings.OtlpTracesHeaders ?? [], settings.OtlpTracesTimeoutMs, "/v1/traces", TracesGrpcMethodPath, "traces");
    }

    public static IApiRequestFactory GetMetrics(ExporterSettings settings)
    {
        return Get(settings.OtlpMetricsEndpoint, settings.OtlpMetricsProtocol, settings.OtlpMetricsHeaders ?? [], settings.OtlpMetricsTimeoutMs, "/v1/metrics", MetricsGrpcMethodPath, "metrics");
    }

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Different implementation types are returned on for different TFMs")]
    private static IApiRequestFactory Get(
        Uri signalEndpoint,
        OtlpProtocol protocol,
        KeyValuePair<string, string>[] signalHeaders,
        int timeoutMs,
        string signalPath,
        string grpcMethodPath,
        string productName)
    {
        var httpHeaderHelper = new OtlpHeaderHelper(signalHeaders);

#if NETCOREAPP3_1_OR_GREATER
        if (protocol == OtlpProtocol.Grpc)
        {
            return GetGrpc(signalEndpoint, httpHeaderHelper.DefaultHeaders, TimeSpan.FromMilliseconds(timeoutMs), grpcMethodPath, productName);
        }
#endif

        if (signalEndpoint.OriginalString.StartsWith(ExporterSettings.UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var socketPath = signalEndpoint.PathAndQuery;
            var endpointPath = protocol switch
            {
                OtlpProtocol.HttpProtobuf or OtlpProtocol.HttpJson => UriHelpers.Combine(Localhost, signalPath),
                _ => Localhost,
            };

#if NET5_0_OR_GREATER
            Log.Information<string, string>("Using " + nameof(SocketHandlerRequestFactory) + " for {ProductName} transport, with Unix Domain Sockets path {Path}.", productName, socketPath);
            // HttpClient only accepts http(s) URIs - use http://localhost and append the fixed relative path (e.g. /v1/traces)
            return new SocketHandlerRequestFactory(
                new UnixDomainSocketStreamFactory(socketPath),
                httpHeaderHelper.DefaultHeaders,
                endpointPath,
                timeout: TimeSpan.FromMilliseconds(timeoutMs));
#elif NETCOREAPP3_1_OR_GREATER
            Log.Information<string, string>("Using " + nameof(HttpStreamRequestFactory) + " for {ProductName} transport, with Unix Domain Sockets path {Path}.", productName, socketPath);
            // HttpClient only accepts http(s) URIs - use http://localhost and append the fixed relative path (e.g. /v1/traces)
            return new HttpStreamRequestFactory(
                new UnixDomainSocketStreamFactory(socketPath),
                new DatadogHttpClient(httpHeaderHelper),
                endpointPath);
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

#if NETCOREAPP3_1_OR_GREATER
    private static OtlpGrpcRequestFactory GetGrpc(
        Uri signalEndpoint,
        KeyValuePair<string, string>[] headers,
        TimeSpan timeout,
        string grpcMethodPath,
        string productName)
    {
#if NET5_0_OR_GREATER
        // ExporterSettings already falls back to TCP on .NET Core 3.1, which doesn't support gRPC over Unix Domain Sockets
        if (signalEndpoint.OriginalString.StartsWith(ExporterSettings.UnixDomainSocketPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var socketPath = signalEndpoint.PathAndQuery;
            Log.Information<string, string>("Using " + nameof(OtlpGrpcRequestFactory) + " for {ProductName} transport, with Unix Domain Sockets path {Path}.", productName, socketPath);
            // HttpClient only accepts http(s) URIs - use http://localhost and append the gRPC method path
            return new OtlpGrpcRequestFactory(
                new UnixDomainSocketStreamFactory(socketPath),
                UriHelpers.Combine(Localhost, grpcMethodPath),
                headers,
                timeout);
        }
#endif

        Log.Information("Using " + nameof(OtlpGrpcRequestFactory) + " for {ProductName} transport.", productName);
        // Like OpenTelemetry .NET, append the gRPC method path to the endpoint unless it's already there
        var endpoint = signalEndpoint.AbsolutePath.EndsWith(grpcMethodPath, StringComparison.Ordinal)
                           ? signalEndpoint
                           : UriHelpers.Combine(signalEndpoint, grpcMethodPath);
        return new OtlpGrpcRequestFactory(endpoint, headers, timeout);
    }
#endif
}

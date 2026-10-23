// <copyright file="OtlpGrpcRequestFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Datadog.Trace.Util;
#if !NET5_0_OR_GREATER
using System.Reflection;
using Datadog.Trace.Logging;
#endif

namespace Datadog.Trace.Agent.Transports;

/// <summary>
/// Creates <see cref="OtlpGrpcRequest"/>s, which send OTLP export requests to a collector as unary gRPC calls.
/// </summary>
internal sealed class OtlpGrpcRequestFactory : IApiRequestFactory
{
#if !NET5_0_OR_GREATER
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<OtlpGrpcRequestFactory>();
#endif

    private readonly HttpClient _client;
    private readonly HttpMessageHandler _handler;
    private readonly Uri _endpoint;
    private readonly IStreamFactory? _streamFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpGrpcRequestFactory"/> class.
    /// </summary>
    /// <param name="endpoint">The endpoint of the gRPC method to call, e.g. http://localhost:4317/opentelemetry.proto.collector.trace.v1.TraceService/Export</param>
    /// <param name="defaultHeaders">The headers, sent as gRPC metadata, to add to every request</param>
    /// <param name="timeout">The timeout of every request</param>
    /// <param name="handler">The handler to use, for testing purposes</param>
    public OtlpGrpcRequestFactory(Uri endpoint, KeyValuePair<string, string>[] defaultHeaders, TimeSpan timeout, HttpMessageHandler? handler = null)
    {
        _endpoint = endpoint;
        _handler = handler ?? CreateHandler();
        _client = new HttpClient(_handler) { Timeout = timeout };

        foreach (var pair in defaultHeaders)
        {
            _client.DefaultRequestHeaders.Add(pair.Key, pair.Value);
        }
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// Initializes a new instance of the <see cref="OtlpGrpcRequestFactory"/> class.
    /// </summary>
    /// <param name="streamFactory">The factory of the streams to send the requests over, e.g. to a Unix Domain Socket</param>
    /// <param name="endpoint">The endpoint of the gRPC method to call. Only its path is relevant, as requests are sent over the streams</param>
    /// <param name="defaultHeaders">The headers, sent as gRPC metadata, to add to every request</param>
    /// <param name="timeout">The timeout of every request</param>
    public OtlpGrpcRequestFactory(IStreamFactory streamFactory, Uri endpoint, KeyValuePair<string, string>[] defaultHeaders, TimeSpan timeout)
        : this(
            endpoint,
            defaultHeaders,
            timeout,
            new SocketsHttpHandler { ConnectCallback = async (_, token) => await streamFactory.GetBidirectionalStreamAsync(token).ConfigureAwait(false) })
    {
        _streamFactory = streamFactory;
    }
#endif

    public string Info(Uri endpoint) => _streamFactory is null ? endpoint.ToString() : $"{endpoint} to {_streamFactory.Info()}";

    public Uri GetEndpoint(string? relativePath) => relativePath is null ? _endpoint : UriHelpers.Combine(_endpoint, relativePath);

    public IApiRequest Create(Uri endpoint) => new OtlpGrpcRequest(_client, endpoint);

    public void SetProxy(WebProxy proxy, NetworkCredential? credential)
    {
        if (_handler is SocketsHttpHandler handler)
        {
            handler.Proxy = proxy;
            if (credential is not null)
            {
                handler.Credentials = credential;
            }
        }
    }

    private static SocketsHttpHandler CreateHandler()
    {
        var handler = new SocketsHttpHandler();
#if !NET5_0_OR_GREATER
        AllowHttp2WithoutTls(handler);
#endif
        return handler;
    }

#if !NET5_0_OR_GREATER
    /// <summary>
    /// Without TLS, .NET Core 3.1 only sends HTTP/2 requests, which gRPC requires, when the process-wide
    /// System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport switch is enabled. Each handler copies it to its
    /// settings when it's created, so set this setting on this handler only, to leave the application unaffected.
    /// This build also runs on .NET 5, which doesn't have this setting, and relies on the request's version policy instead, see OtlpGrpcRequest.
    /// </summary>
    private static void AllowHttp2WithoutTls(SocketsHttpHandler handler)
    {
        try
        {
            var settings = typeof(SocketsHttpHandler).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(handler);
            settings?.GetType().GetField("_allowUnencryptedHttp2", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(settings, true);
        }
        catch (Exception ex)
        {
            // Only gRPC with TLS works then
            Log.Debug(ex, "Unable to allow HTTP/2 without TLS for OTLP gRPC requests.");
        }
    }
#endif
}
#endif

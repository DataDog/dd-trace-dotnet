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
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.Agent.Transports;

/// <summary>
/// Creates <see cref="OtlpGrpcRequest"/>s, which send OTLP export requests to a collector as unary gRPC calls.
/// </summary>
internal sealed class OtlpGrpcRequestFactory : IApiRequestFactory
{
#if !NET5_0_OR_GREATER
    private static readonly object Http2UnencryptedSupportLock = new();
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
#if NET5_0_OR_GREATER
        return new SocketsHttpHandler();
#else
        // Without TLS, .NET Core 3.1 only sends HTTP/2 requests, which gRPC requires, when this switch is enabled.
        // Each handler reads the switch once, when it's created, so only enable it while creating this handler
        // to leave the application's own handlers unaffected.
        // This build also runs on .NET 5, which ignores the switch: there, gRPC without TLS requires a request
        // version policy that this build can't set, so only gRPC with TLS works.
        lock (Http2UnencryptedSupportLock)
        {
            var isSwitchSet = AppContext.TryGetSwitch(PlatformKeys.AppContextHttp2UnencryptedSupport, out var previousValue);
            AppContext.SetSwitch(PlatformKeys.AppContextHttp2UnencryptedSupport, true);

            try
            {
                return new SocketsHttpHandler();
            }
            finally
            {
                // A switch can't be removed, so when the application didn't set it, restore the value that the
                // runtime would otherwise read from the environment variable.
                AppContext.SetSwitch(
                    PlatformKeys.AppContextHttp2UnencryptedSupport,
                    isSwitchSet ? previousValue : IsHttp2UnencryptedSupportEnabledByEnvironmentVariable());
            }
        }
#endif
    }

#if !NET5_0_OR_GREATER
    private static bool IsHttp2UnencryptedSupportEnabledByEnvironmentVariable()
    {
        // Same parsing as SocketsHttpHandler
        var value = EnvironmentHelpers.GetEnvironmentVariable(PlatformKeys.DotNetHttp2UnencryptedSupport);
        return value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("1"));
    }
#endif
}
#endif

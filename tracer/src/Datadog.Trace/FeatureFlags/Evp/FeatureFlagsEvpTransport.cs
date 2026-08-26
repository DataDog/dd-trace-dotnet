// <copyright file="FeatureFlagsEvpTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
#if NETCOREAPP
using System.Net.Http;
#endif
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Evp;

/// <summary>
/// Selects and sends to a Feature Flags EVP route without changing the product payload.
/// </summary>
internal sealed class FeatureFlagsEvpTransport : IDisposable
{
    internal const string ExposureIntakePath = "api/v2/exposures";
    internal const string FlagEvaluationIntakePath = "api/v2/flagevaluation";
    internal const string EventPlatformProxyV4 = "evp_proxy/v4";
    internal const string EventPlatformProxyV2 = "evp_proxy/v2";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsEvpTransport));

    private readonly FeatureFlagsSource _source;
    private readonly IApiRequestFactory? _directRequestFactory;
    private readonly IDiscoveryService _discoveryService;
    private readonly Action<AgentConfiguration> _discoveryCallback;
    private readonly IDisposable? _settingsSubscription;
    private IApiRequestFactory _localRequestFactory;
    private string? _localProxyEndpoint;
    private int _directIsSticky;
    private int _unavailableWarningLogged;

    public FeatureFlagsEvpTransport(TracerSettings settings, IDiscoveryService discoveryService)
    {
        _source = settings.FeatureFlags.Source;
        _localRequestFactory = CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);
        _directRequestFactory = _source == FeatureFlagsSource.Agentless
                                    ? CreateDirectRequestFactory(settings.FeatureFlags)
                                    : null;
        _discoveryService = discoveryService;
        _discoveryCallback = UpdateAgentConfiguration;

        // Remote Configuration never falls back to direct intake. Keep its historical v2 route
        // available while discovery starts, and replace it with v4 when the Agent advertises it.
        _localProxyEndpoint = _source == FeatureFlagsSource.RemoteConfig ? EventPlatformProxyV2 : null;

        _settingsSubscription = settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                Interlocked.Exchange(ref _localRequestFactory!, CreateLocalRequestFactory(exporter));
            }
        });

        _discoveryService.SubscribeToChanges(_discoveryCallback);
    }

    [TestingOnly]
    internal FeatureFlagsEvpTransport(
        FeatureFlagsSource source,
        IApiRequestFactory localRequestFactory,
        IApiRequestFactory? directRequestFactory,
        IDiscoveryService discoveryService,
        string? initialLocalProxyEndpoint = null)
    {
        _source = source;
        _localRequestFactory = localRequestFactory;
        _directRequestFactory = source == FeatureFlagsSource.Agentless ? directRequestFactory : null;
        _discoveryService = discoveryService;
        _discoveryCallback = UpdateAgentConfiguration;
        _localProxyEndpoint = initialLocalProxyEndpoint ?? (source == FeatureFlagsSource.RemoteConfig ? EventPlatformProxyV2 : null);
        _discoveryService.SubscribeToChanges(_discoveryCallback);
    }

    private enum NetworkFailure
    {
        None,
        DefinitivePreSend,
        Ambiguous,
    }

    internal static KeyValuePair<string, string>[] GetDirectHeaders(string apiKey) =>
    [
        new(TelemetryConstants.ApiKeyHeader, apiKey),
        new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language),
        new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.ThreePartVersion),
    ];

    internal static IApiRequestFactory? CreateDirectRequestFactory(FeatureFlagsSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey)
         || !Uri.TryCreate($"https://event-platform-intake.{settings.Site}", UriKind.Absolute, out var endpoint)
         || endpoint.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var headers = GetDirectHeaders(settings.ApiKey!);
#if NETCOREAPP
        // With no custom handler/proxy, HttpClientHandler honours the standard HTTPS_PROXY and
        // NO_PROXY environment variables for the current runtime.
        return new HttpClientRequestFactory(endpoint, headers, timeout: TimeSpan.FromSeconds(5));
#else
        // HttpWebRequest uses the platform default proxy, including its bypass list.
        return new ApiWebRequestFactory(endpoint, headers, timeout: TimeSpan.FromSeconds(5));
#endif
    }

    private static IApiRequestFactory CreateLocalRequestFactory(ExporterSettings exporterSettings)
        => AgentTransportStrategy.Get(
            exporterSettings,
            productName: "Feature Flags EVP",
            tcpTimeout: TimeSpan.FromSeconds(5),
            httpHeaderHelper: EventPlatformHeaderHelper.Instance);

    private static NetworkFailure ClassifyNetworkFailure(Exception exception)
    {
        var isNetworkFailure = false;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case SocketException socketException:
                    return socketException.SocketErrorCode is SocketError.HostNotFound or SocketError.TryAgain or SocketError.ConnectionRefused
                        || socketException.ErrorCode == 2 // ENOENT for a missing Unix domain socket
                               ? NetworkFailure.DefinitivePreSend
                               : NetworkFailure.Ambiguous;
                case WebException webException:
                    switch (webException.Status)
                    {
                        case WebExceptionStatus.ConnectFailure:
                        case WebExceptionStatus.NameResolutionFailure:
                        case WebExceptionStatus.ProxyNameResolutionFailure:
                            return NetworkFailure.DefinitivePreSend;
                        case WebExceptionStatus.ConnectionClosed:
                        case WebExceptionStatus.KeepAliveFailure:
                        case WebExceptionStatus.PipelineFailure:
                        case WebExceptionStatus.ReceiveFailure:
                        case WebExceptionStatus.RequestCanceled:
                        case WebExceptionStatus.SendFailure:
                        case WebExceptionStatus.Timeout:
                            return NetworkFailure.Ambiguous;
                    }

                    isNetworkFailure = true;
                    break;
#if NETCOREAPP
                case HttpRequestException:
#endif
                case IOException:
                case TimeoutException:
                    isNetworkFailure = true;
                    break;
            }
        }

        return isNetworkFailure ? NetworkFailure.Ambiguous : NetworkFailure.None;
    }

    public void Dispose()
    {
        _settingsSubscription?.Dispose();
        _discoveryService.RemoveSubscription(_discoveryCallback);
    }

    internal Task SendAsync<T>(T payload, string intakePath, JsonSerializerSettings serializerSettings)
        => SendAsync(intakePath, request => request.PostAsJsonAsync(payload, MultipartCompression.GZip, serializerSettings));

    internal Task SendCompressedAsync(ArraySegment<byte> payload, string intakePath)
        => SendAsync(intakePath, request => request.PostAsync(payload, MimeTypes.Json, "gzip"));

    private async Task SendAsync(string intakePath, Func<IApiRequest, Task<IApiResponse>> sendAsync)
    {
        if (Volatile.Read(ref _directIsSticky) != 0)
        {
            await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
            return;
        }

        var localProxyEndpoint = Volatile.Read(ref _localProxyEndpoint);
        if (localProxyEndpoint is not null)
        {
            await SendLocalAsync(intakePath, localProxyEndpoint, sendAsync).ConfigureAwait(false);
            return;
        }

        if (_source == FeatureFlagsSource.Agentless && _directRequestFactory is not null)
        {
            Interlocked.Exchange(ref _directIsSticky, 1);
            await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
            return;
        }

        if (Interlocked.Exchange(ref _unavailableWarningLogged, 1) == 0)
        {
            Log.Warning("Feature Flags event delivery is disabled because no compatible local EVP route or direct intake credentials are available");
        }
    }

    private void UpdateAgentConfiguration(AgentConfiguration configuration)
    {
        var endpoint = configuration.EventPlatformProxyEndpoint switch
        {
            EventPlatformProxyV4 => EventPlatformProxyV4,
            EventPlatformProxyV2 => EventPlatformProxyV2,
            _ => null,
        };

        Volatile.Write(ref _localProxyEndpoint, endpoint);
    }

    private async Task SendLocalAsync(string intakePath, string localProxyEndpoint, Func<IApiRequest, Task<IApiResponse>> sendAsync)
    {
        var localFactory = Volatile.Read(ref _localRequestFactory);
        var endpoint = localFactory.GetEndpoint($"{localProxyEndpoint}/{intakePath}");

        try
        {
            var request = localFactory.Create(endpoint);
            using var response = await sendAsync(request).ConfigureAwait(false);
            if (response.StatusCode is 403 or 404 or 405 && _directRequestFactory is not null)
            {
                Interlocked.Exchange(ref _directIsSticky, 1);
                await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ClassifyNetworkFailure(ex) is NetworkFailure.DefinitivePreSend && _directRequestFactory is not null)
        {
            Interlocked.Exchange(ref _directIsSticky, 1);
            await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
        }
        catch (Exception ex) when (ClassifyNetworkFailure(ex) is NetworkFailure.Ambiguous)
        {
            // The local relay may have received this payload. Switch only future payloads so the
            // current one can never be duplicated across the local and direct routes.
            if (_directRequestFactory is not null)
            {
                Interlocked.Exchange(ref _directIsSticky, 1);
            }

            Log.ErrorSkipTelemetry(ex, "Feature Flags local EVP request failed ambiguously; the current event batch will not be replayed");
        }
    }

    private async Task SendDirectAsync(string intakePath, Func<IApiRequest, Task<IApiResponse>> sendAsync)
    {
        var directFactory = _directRequestFactory;
        if (directFactory is null)
        {
            return;
        }

        var endpoint = directFactory.GetEndpoint(intakePath);
        try
        {
            var request = directFactory.Create(endpoint);
            using var response = await sendAsync(request).ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 300)
            {
                Log.Warning<int>("Feature Flags direct EVP request failed with HTTP status code {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Direct intake is terminal: never loop a failed direct request back through the Agent.
            Log.ErrorSkipTelemetry(ex, "Feature Flags direct EVP request failed");
        }
    }
}

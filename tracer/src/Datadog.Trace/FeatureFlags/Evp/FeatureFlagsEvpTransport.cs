// <copyright file="FeatureFlagsEvpTransport.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
using Datadog.Trace.FeatureFlags.Agentless;
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
    internal const int PayloadSizeLimitBytes = 5 * 1024 * 1024;
    internal const string EventPlatformProxyV4 = "evp_proxy/v4";
    internal const string EventPlatformProxyV2 = "evp_proxy/v2";

    private static readonly TimeSpan InitialDiscoveryWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RouteRecoveryCooldown = TimeSpan.FromSeconds(30);
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FeatureFlagsEvpTransport));

    private readonly FeatureFlagsSource _source;
    private readonly IApiRequestFactory? _directRequestFactory;
    private readonly IDiscoveryService _discoveryService;
    private readonly Action<AgentConfiguration> _discoveryCallback;
    private readonly Action<string>? _warningSink;
    private readonly IDisposable? _settingsSubscription;
    private readonly TimeSpan _initialDiscoveryWait;
    private readonly TimeSpan _routeRecoveryCooldown;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly TaskCompletionSource<bool> _initialDiscovery = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly bool _discoverySubscribed;

    private IApiRequestFactory _localRequestFactory;
    private string? _localProxyEndpoint;
    private int _directIsSticky;
    private int _disposed;
    private int _discoveryKnown;
    private int _localRecoveryProbeInProgress;
    private int _unavailableWarningLogged;
    private long _localUnavailableUntilUtcTicks;

    internal FeatureFlagsEvpTransport(TracerSettings settings, IDiscoveryService discoveryService, Action<string>? warningSink = null)
    {
        _source = settings.FeatureFlags.Source;
        _localRequestFactory = CreateLocalRequestFactory(settings.Manager.InitialExporterSettings);
        _discoveryService = discoveryService;
        _discoveryCallback = UpdateAgentConfiguration;
        _warningSink = warningSink;
        _initialDiscoveryWait = InitialDiscoveryWait;
        _routeRecoveryCooldown = RouteRecoveryCooldown;
        _utcNow = static () => DateTimeOffset.UtcNow;

        if (_source == FeatureFlagsSource.Agentless)
        {
            _directRequestFactory = CreateDirectRequestFactory(settings.FeatureFlags, out var invalidSite);
            if (invalidSite)
            {
                WarnInvalidSite();
            }

            // The shared discovery service owns its own bounded retry/backoff loop. Event flushes
            // wait for its first result once and then consume later callbacks; they never start an
            // independent /info request on every flush.
            _discoveryService.SubscribeToChanges(_discoveryCallback);
            _discoverySubscribed = true;
        }
        else if (_source == FeatureFlagsSource.RemoteConfig)
        {
            // Preserve the historical Remote Config transport: fixed EVP v2, no /info discovery,
            // and no direct credentials.
            _localProxyEndpoint = EventPlatformProxyV2;
            Volatile.Write(ref _discoveryKnown, 1);
            _initialDiscovery.TrySetResult(true);
        }

        _settingsSubscription = settings.Manager.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                Interlocked.Exchange(ref _localRequestFactory!, CreateLocalRequestFactory(exporter));
            }
        });
    }

    [TestingOnly]
    internal FeatureFlagsEvpTransport(
        FeatureFlagsSource source,
        IApiRequestFactory localRequestFactory,
        IApiRequestFactory? directRequestFactory,
        IDiscoveryService discoveryService,
        string? initialLocalProxyEndpoint = null,
        bool initialDiscoveryKnown = true,
        TimeSpan? initialDiscoveryWait = null,
        TimeSpan? routeRecoveryCooldown = null,
        Func<DateTimeOffset>? utcNow = null,
        Action<string>? warningSink = null)
    {
        _source = source;
        _localRequestFactory = localRequestFactory;
        _directRequestFactory = source == FeatureFlagsSource.Agentless ? directRequestFactory : null;
        _discoveryService = discoveryService;
        _discoveryCallback = UpdateAgentConfiguration;
        _warningSink = warningSink;
        _initialDiscoveryWait = initialDiscoveryWait ?? InitialDiscoveryWait;
        _routeRecoveryCooldown = routeRecoveryCooldown ?? RouteRecoveryCooldown;
        _utcNow = utcNow ?? (static () => DateTimeOffset.UtcNow);

        if (source == FeatureFlagsSource.RemoteConfig)
        {
            _localProxyEndpoint = EventPlatformProxyV2;
            Volatile.Write(ref _discoveryKnown, 1);
            _initialDiscovery.TrySetResult(true);
        }
        else
        {
            _localProxyEndpoint = initialLocalProxyEndpoint;
            if (initialDiscoveryKnown)
            {
                Volatile.Write(ref _discoveryKnown, 1);
                _initialDiscovery.TrySetResult(true);
            }

            _discoveryService.SubscribeToChanges(_discoveryCallback);
            _discoverySubscribed = true;
        }
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
        new(FeatureFlagsEvpHeaderHelper.EvpOriginHeader, FeatureFlagsEvpHeaderHelper.EvpOrigin),
        new(FeatureFlagsEvpHeaderHelper.EvpOriginVersionHeader, TracerConstants.ThreePartVersion),
    ];

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Different implementation types are returned for different TFMs")]
    internal static IApiRequestFactory? CreateDirectRequestFactory(FeatureFlagsSettings settings)
        => CreateDirectRequestFactory(settings, out _);

    [SuppressMessage("Performance", "CA1859:Use concrete types when possible for improved performance", Justification = "Different implementation types are returned for different TFMs")]
    private static IApiRequestFactory? CreateDirectRequestFactory(FeatureFlagsSettings settings, out bool invalidSite)
    {
        invalidSite = false;
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            return null;
        }

        if (!AgentlessEndpoint.TryNormalizeSite(settings.Site, out var site, out _))
        {
            invalidSite = true;
            return null;
        }

        var expectedHost = $"event-platform-intake.{site}";
        if (!Uri.TryCreate($"https://{expectedHost}", UriKind.Absolute, out var endpoint)
         || endpoint.Scheme != Uri.UriSchemeHttps
         || !string.Equals(endpoint.IdnHost, expectedHost, StringComparison.Ordinal))
        {
            invalidSite = true;
            return null;
        }

        var headers = GetDirectHeaders(settings.ApiKey!);
#if NETCOREAPP
        // The default HttpClientHandler honours the runtime's HTTPS_PROXY and NO_PROXY settings.
        // Redirects stay disabled so the API key is sent only to the configured intake host.
        return new HttpClientRequestFactory(endpoint, headers, timeout: TimeSpan.FromSeconds(5), allowAutoRedirect: false);
#else
        // HttpWebRequest uses the platform default proxy and bypass list. Redirects stay disabled
        // so the API key is sent only to the configured intake host.
        return new ApiWebRequestFactory(endpoint, headers, timeout: TimeSpan.FromSeconds(5), allowAutoRedirect: false);
#endif
    }

    [TestingAndPrivateOnly]
    internal static IApiRequestFactory CreateLocalRequestFactory(ExporterSettings exporterSettings)
        => AgentTransportStrategy.Get(
            exporterSettings,
            productName: "Feature Flags EVP",
            tcpTimeout: TimeSpan.FromSeconds(5),
            httpHeaderHelper: FeatureFlagsEvpHeaderHelper.Instance);

    [TestingAndPrivateOnly]
    internal static bool IsDefinitivePreSendSocketFailure(SocketException exception)
        => exception.SocketErrorCode is SocketError.HostNotFound
                                             or SocketError.TryAgain
                                             or SocketError.ConnectionRefused
                                             or SocketError.NetworkUnreachable
                                             or SocketError.HostUnreachable
                                             or SocketError.AddressNotAvailable
        || exception.ErrorCode == 2 // ENOENT for a missing Unix domain socket
        || exception.ErrorCode == 10061; // WSAECONNREFUSED

    private static NetworkFailure ClassifyNetworkFailure(Exception exception)
    {
        var isNetworkFailure = false;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case SocketException socketException:
                    return IsDefinitivePreSendSocketFailure(socketException)
                               ? NetworkFailure.DefinitivePreSend
                               : NetworkFailure.Ambiguous;
                case FileNotFoundException:
                    return NetworkFailure.DefinitivePreSend;
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
                case OperationCanceledException:
                case TimeoutException:
                    isNetworkFailure = true;
                    break;
            }
        }

        return isNetworkFailure ? NetworkFailure.Ambiguous : NetworkFailure.None;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _initialDiscovery.TrySetResult(false);
        _settingsSubscription?.Dispose();
        if (_discoverySubscribed)
        {
            _discoveryService.RemoveSubscription(_discoveryCallback);
        }
    }

    internal Task SendAsync<T>(T payload, string intakePath, JsonSerializerSettings serializerSettings)
        => SendAsync(intakePath, request => request.PostAsJsonAsync(payload, MultipartCompression.GZip, serializerSettings));

    internal Task SendCompressedAsync(ArraySegment<byte> payload, string intakePath)
        => SendAsync(intakePath, request => request.PostAsync(payload, MimeTypes.Json, "gzip"));

    private async Task SendAsync(string intakePath, Func<IApiRequest, Task<IApiResponse>> sendAsync)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        if (Volatile.Read(ref _directIsSticky) != 0)
        {
            await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
            return;
        }

        var localProxyEndpoint = Volatile.Read(ref _localProxyEndpoint);
        if (localProxyEndpoint is not null)
        {
            await TrySendLocalAsync(intakePath, localProxyEndpoint, sendAsync).ConfigureAwait(false);
            return;
        }

        if (_source == FeatureFlagsSource.Agentless && Volatile.Read(ref _discoveryKnown) == 0)
        {
            var completed = await Task.WhenAny(_initialDiscovery.Task, Task.Delay(_initialDiscoveryWait)).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            if (completed != _initialDiscovery.Task && Volatile.Read(ref _discoveryKnown) == 0)
            {
                Volatile.Write(ref _discoveryKnown, 1);
                _initialDiscovery.TrySetResult(false);
            }
        }

        localProxyEndpoint = Volatile.Read(ref _localProxyEndpoint);
        if (localProxyEndpoint is not null)
        {
            await TrySendLocalAsync(intakePath, localProxyEndpoint, sendAsync).ConfigureAwait(false);
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
            WarnUnavailable();
        }
    }

    private void WarnInvalidSite()
    {
        const string Message = "Feature Flags direct event delivery is disabled because DD_SITE is not a valid DNS site suffix";
        if (_warningSink is { } warningSink)
        {
            warningSink(Message);
        }
        else
        {
            Log.Warning("Feature Flags direct event delivery is disabled because DD_SITE is not a valid DNS site suffix");
        }
    }

    private void WarnUnavailable()
    {
        const string Message = "Feature Flags event delivery is unavailable because no compatible local EVP route or direct intake credentials are available";
        if (_warningSink is { } warningSink)
        {
            warningSink(Message);
        }
        else
        {
            Log.Warning("Feature Flags event delivery is unavailable because no compatible local EVP route or direct intake credentials are available");
        }
    }

    private void UpdateAgentConfiguration(AgentConfiguration configuration)
    {
        // Agentless event delivery requires the Agent to preserve the logical producer identity.
        // Older Agents can advertise EVP while silently dropping these headers, so use direct
        // intake instead unless /info explicitly advertises both forwarding capabilities.
        var endpoint = configuration.EventPlatformProxySupportsEvpOriginHeaders
                           ? configuration.EventPlatformProxyEndpoint switch
                           {
                               EventPlatformProxyV4 => EventPlatformProxyV4,
                               EventPlatformProxyV2 => EventPlatformProxyV2,
                               _ => null,
                           }
                           : null;

        Volatile.Write(ref _localProxyEndpoint, endpoint);
        if (endpoint is not null)
        {
            Interlocked.Exchange(ref _localUnavailableUntilUtcTicks, 0);
        }

        Volatile.Write(ref _discoveryKnown, 1);
        _initialDiscovery.TrySetResult(true);
    }

    private async Task TrySendLocalAsync(string intakePath, string localProxyEndpoint, Func<IApiRequest, Task<IApiResponse>> sendAsync)
    {
        var unavailableUntilUtcTicks = Interlocked.Read(ref _localUnavailableUntilUtcTicks);
        var isRecoveryProbe = unavailableUntilUtcTicks != 0;
        if (isRecoveryProbe
         && (_utcNow().UtcTicks < unavailableUntilUtcTicks
          || Interlocked.CompareExchange(ref _localRecoveryProbeInProgress, 1, 0) != 0))
        {
            if (Interlocked.Exchange(ref _unavailableWarningLogged, 1) == 0)
            {
                WarnUnavailable();
            }

            return;
        }

        try
        {
            await SendLocalAsync(intakePath, localProxyEndpoint, sendAsync).ConfigureAwait(false);
        }
        finally
        {
            if (isRecoveryProbe)
            {
                Interlocked.Exchange(ref _localRecoveryProbeInProgress, 0);
            }
        }
    }

    private async Task SendLocalAsync(string intakePath, string localProxyEndpoint, Func<IApiRequest, Task<IApiResponse>> sendAsync)
    {
        var localFactory = Volatile.Read(ref _localRequestFactory);
        var endpoint = localFactory.GetEndpoint($"{localProxyEndpoint}/{intakePath}");

        try
        {
            var request = localFactory.Create(endpoint);
            using var response = await sendAsync(request).ConfigureAwait(false);
            if (response.StatusCode is >= 200 and < 300)
            {
                Interlocked.Exchange(ref _localUnavailableUntilUtcTicks, 0);
                return;
            }

            // The Agent contract proves these statuses mean the proxy route did not accept the
            // payload. An upstream 403 is not safe to replay because it may have been forwarded.
            if (response.StatusCode is 404 or 405)
            {
                if (LeaveLocalRoute())
                {
                    await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
                }
                else
                {
                    Log.Warning<int>("Feature Flags local EVP request failed with HTTP status code {StatusCode}", response.StatusCode);
                }

                return;
            }

            // Other responses may have come from upstream after the Agent accepted the payload.
            // Never replay this batch, but leave the failed route for future Agentless batches.
            LeaveLocalRoute();
            Log.Warning<int>("Feature Flags local EVP request failed with HTTP status code {StatusCode}", response.StatusCode);
        }
        catch (Exception ex) when (ClassifyNetworkFailure(ex) is NetworkFailure.DefinitivePreSend)
        {
            if (LeaveLocalRoute())
            {
                await SendDirectAsync(intakePath, sendAsync).ConfigureAwait(false);
            }
            else
            {
                Log.ErrorSkipTelemetry(ex, "Feature Flags local EVP request failed before the payload was sent");
            }
        }
        catch (Exception ex) when (ClassifyNetworkFailure(ex) is NetworkFailure.Ambiguous)
        {
            // The local relay may have received this payload. Switch only future payloads so the
            // current one can never be duplicated across the local and direct routes.
            LeaveLocalRoute();

            Log.ErrorSkipTelemetry(ex, "Feature Flags local EVP request failed ambiguously; the current event batch will not be replayed");
        }
    }

    private bool LeaveLocalRoute()
    {
        if (_source != FeatureFlagsSource.Agentless)
        {
            return false;
        }

        if (_directRequestFactory is not null)
        {
            Interlocked.Exchange(ref _directIsSticky, 1);
            return true;
        }

        var unavailableUntil = _utcNow().Add(_routeRecoveryCooldown).UtcTicks;
        Interlocked.Exchange(ref _localUnavailableUntilUtcTicks, unavailableUntil);
        return false;
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

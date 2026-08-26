// <copyright file="AgentlessConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// Polls the agentless endpoint for flag configuration. Polling is billable, so it is only
/// started once application code has activated the provider.
/// </summary>
internal sealed class AgentlessConfigurationSource : IDisposable
{
    private const int MaxAttempts = 3;
    private const double RetryJitter = 0.2;

    private static readonly TimeSpan FirstRetryMin = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FirstRetryMax = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SecondRetryMin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SecondRetryMax = TimeSpan.FromSeconds(30);

    // A jittered retry delay never drops below this, so a short poll interval cannot turn
    // retries into a burst against the endpoint.
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AgentlessConfigurationSource));

    private readonly IApiRequestFactory _requestFactory;
    private readonly AgentlessEndpoint _endpoint;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _requestTimeout;
    private readonly Func<ServerConfiguration, bool> _applyConfiguration;
    private readonly Func<TimeSpan, Task> _waitAsync;

    // Not a CancellationTokenSource: cancellation throws, and an exception on the shutdown path can
    // crash the runtime, so shutdown is signalled by completing a task instead.
    private readonly TaskCompletionSource<bool> _shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Only ever touched from the poll loop.
    private readonly HashSet<string> _loggedFailureCategories = new();

    private readonly IDisposable? _environmentSubscription;

    // Written by the settings-change callback, read by the poll loop. The URI is stored rather than
    // the environment it carries, so it is only built when the environment changes.
    private Uri _requestUri;

    // Only ever touched from the poll loop.
    private bool _malformedPayloadLogged;
    private bool _applyFailureLogged;
    private string? _etag;
    private Uri? _etagUri;

    private int _started;

    internal AgentlessConfigurationSource(
        AgentlessEndpoint endpoint,
        IApiRequestFactory requestFactory,
        TimeSpan pollInterval,
        TimeSpan requestTimeout,
        Func<ServerConfiguration, bool> applyConfiguration,
        string? environment = null,
        TracerSettings.SettingsManager? settingsManager = null,
        Func<TimeSpan, Task>? waitAsync = null)
    {
        _endpoint = endpoint;
        _requestFactory = requestFactory;
        _pollInterval = pollInterval;
        _requestTimeout = requestTimeout;
        _applyConfiguration = applyConfiguration;
        _requestUri = endpoint.BuildRequestUri(environment);
        _waitAsync = waitAsync ?? Task.Delay;

        // Subscribed last, so every field the callback touches is already set. The environment is
        // tracked rather than captured: customers can change it in code while the application runs,
        // and flags are targeted per environment.
        _environmentSubscription = settingsManager?.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateEnvironment(mutable.Environment);
            }
        });
    }

    /// <summary>
    /// Creates the source, or returns <c>null</c> when it cannot be operated: a base URL that is
    /// not a URL, or the managed endpoint without an API key. Polling anyway would only produce
    /// failures every interval.
    /// </summary>
    public static AgentlessConfigurationSource? Create(
        FeatureFlagsSettings settings,
        TracerSettings.SettingsManager manager,
        Func<ServerConfiguration, bool> applyConfiguration)
    {
        if (!AgentlessEndpoint.TryCreate(settings.Site, settings.AgentlessBaseUrl, out var endpoint, out var error))
        {
            Log.Error("Feature Flags agentless source is unavailable: {Error}", error);
            return null;
        }

        if (endpoint.IsManaged && StringUtil.IsNullOrEmpty(settings.ApiKey))
        {
            Log.Error("Feature Flags agentless source requires an API key. Set DD_API_KEY, or point DD_FEATURE_FLAGS_CONFIGURATION_SOURCE_AGENTLESS_BASE_URL at an endpoint of your own.");
            return null;
        }

        return new AgentlessConfigurationSource(
            endpoint,
            CreateRequestFactory(endpoint, settings),
            settings.PollInterval,
            settings.RequestTimeout,
            applyConfiguration,
            manager.InitialMutableSettings.Environment,
            manager);
    }

    /// <summary>
    /// Records the environment to request configuration for, as the URI that carries it. Picked up
    /// by the poll loop on its next request, so a change never disturbs a request already in flight.
    /// </summary>
    internal void UpdateEnvironment(string? environment)
        => Volatile.Write(ref _requestUri, _endpoint.BuildRequestUri(environment));

    /// <summary>
    /// Starts polling. Idempotent.
    /// </summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        // The loop runs on the thread pool, so nothing of it happens on the caller's thread. Nothing
        // ever awaits it either, so a fault is observed here or not at all.
        _ = Task.Run(RunAsync).ContinueWith(t => Log.Error(t.Exception, "Feature Flags agentless poll loop failed"), TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Runs a single poll, including its in-tick retries.
    /// </summary>
    internal async Task PollAsync()
    {
        var result = default(PollResult);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            result = await RequestAsync().ConfigureAwait(false);

            if (_shutdown.Task.IsCompleted)
            {
                // A shutdown mid-poll leaves the response unusable for state transitions: keep
                // last-known-good and the current ETag.
                return;
            }

            if (!IsRetryable(in result))
            {
                break;
            }

            if (attempt == MaxAttempts)
            {
                // Every attempt failed in a retryable way. Last-known-good stays in place.
                WarnFailure(in result, MaxAttempts);
                return;
            }

            await WaitAsync(RetryDelay(attempt)).ConfigureAwait(false);

            if (_shutdown.Task.IsCompleted)
            {
                return;
            }
        }

        if (_shutdown.Task.IsCompleted)
        {
            // A shutdown during the final attempt leaves the response unusable for state
            // transitions: keep last-known-good and the current ETag.
            return;
        }

        Apply(in result);

        // A failure with no status code never reached the endpoint, so it is worth another attempt.
        static bool IsRetryable(in PollResult result)
            => result.StatusCode is not { } status || status is 408 or 429 or (>= 500 and <= 599);
    }

    public void Dispose()
    {
        // The request in flight is bounded by the request timeout, and the loop is never joined,
        // so a shutdown does not wait for it. A poll that completes after disposal is prevented
        // from applying its result by the shutdown check in PollAsync.
        _shutdown.TrySetResult(true);

        try
        {
            _environmentSubscription?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error unsubscribing the Feature Flags agentless poll loop from settings changes");
        }
    }

    // The concrete type is returned rather than the interface because CA1859 asks for it on a
    // private member, which is also why the signature varies by target framework.
#if NETCOREAPP
    private static HttpClientRequestFactory CreateRequestFactory(AgentlessEndpoint endpoint, FeatureFlagsSettings settings)
#else
    private static ApiWebRequestFactory CreateRequestFactory(AgentlessEndpoint endpoint, FeatureFlagsSettings settings)
#endif
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            // The endpoint serves gzip, and neither transport decompresses for us.
            new("Accept-Encoding", "gzip"),
            new(TelemetryConstants.ClientLibraryLanguageHeader, TracerConstants.Language),
            new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.ThreePartVersion),

            // Without this the poll is itself instrumented, producing a span per poll and letting
            // auto-instrumentation recurse through the poller's own client.
            new(HttpHeaderNames.TracingEnabled, "false"),
        };

        if (endpoint.IsManaged)
        {
            // A custom endpoint is left to report its own authentication failure rather than
            // having the Datadog credential sent to it.
            headers.Add(new(TelemetryConstants.ApiKeyHeader, settings.ApiKey!));
        }

        // The endpoint is only the factory's default: both transports honour the URI passed to
        // Create, which is what carries the current environment.
#if NETCOREAPP
        return new HttpClientRequestFactory(endpoint.Uri, headers.ToArray(), timeout: settings.RequestTimeout);
#else
        return new ApiWebRequestFactory(endpoint.Uri, headers.ToArray(), timeout: settings.RequestTimeout);
#endif
    }

    private async Task RunAsync()
    {
        Log.Debug("AgentlessConfigurationSource::RunAsync -> Enter");

        while (!_shutdown.Task.IsCompleted)
        {
            try
            {
                await PollAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Feature Flags agentless poll failed unexpectedly");
            }

            // Fixed delay after completion, so polls never overlap.
            await WaitAsync(_pollInterval).ConfigureAwait(false);
        }

        Log.Debug("AgentlessConfigurationSource::RunAsync -> Exit");
    }

    // A shutdown ends the wait early. The delay itself is left to expire on its own: it holds no
    // thread, and the loop has already exited by the time it does.
    private async Task WaitAsync(TimeSpan delay)
        => await Task.WhenAny(_waitAsync(delay), _shutdown.Task).ConfigureAwait(false);

    private TimeSpan RetryDelay(int attempt)
    {
        var seconds = attempt == 1
                          ? Clamp(_pollInterval.TotalSeconds / 6, FirstRetryMin, FirstRetryMax)
                          : Clamp(_pollInterval.TotalSeconds / 3, SecondRetryMin, SecondRetryMax);

        var jitter = 1 - RetryJitter + (ThreadSafeRandom.Shared.NextDouble() * RetryJitter * 2);

        return TimeSpan.FromSeconds(Math.Max(MinRetryDelay.TotalSeconds, seconds * jitter));

        static double Clamp(double value, TimeSpan minimum, TimeSpan maximum)
            => Math.Max(minimum.TotalSeconds, Math.Min(maximum.TotalSeconds, value));
    }

    private async Task<PollResult> RequestAsync()
    {
        try
        {
            // Read per request rather than captured, because the environment it carries can be
            // changed in code after startup.
            var uri = Volatile.Read(ref _requestUri);

            // An ETag only identifies the configuration served for the URI it came from. Sending it
            // against a different environment would earn a 304 and pin the process to the previous
            // environment's flags, with no way back.
            if (_etagUri is not null && uri != _etagUri)
            {
                _etag = null;
            }

            _etagUri = uri;

            var request = _requestFactory.Create(uri);
            if (_etag is { } etag)
            {
                request.AddHeader("If-None-Match", etag);
            }

#if NETCOREAPP
            // HttpClient.Timeout applies to async calls, so no explicit race is needed.
            using var response = await request.GetAsync().ConfigureAwait(false);
#else
            // HttpWebRequest.Timeout does not apply to async calls (GetResponseAsync), so we race
            // the request against an explicit delay to bound the wait on net461/netstandard2.0.
            var getTask = request.GetAsync();
            var timeoutTask = Task.Delay(_requestTimeout);

            if (await Task.WhenAny(getTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
            {
                // The request is still in flight. Dispose the response when it eventually completes
                // (success or fault) so the underlying connection is released.
                _ = getTask.ContinueWith(
                    t => { try { using var r = t.Result; } catch { } },
                    TaskContinuationOptions.None);

                return new PollResult(statusCode: null, etag: null, configuration: null, parseError: null, error: new TimeoutException($"Feature Flags agentless request timed out after {_requestTimeout.TotalSeconds}s"));
            }

            using var response = await getTask.ConfigureAwait(false);
#endif

            // Only a 200 carries configuration; other bodies are never decoded as one. The payload
            // is parsed here, while the response is still open, so it never has to be held as a
            // string. A payload that does not parse is reported, not thrown: it is not retryable.
            ServerConfiguration? configuration = null;
            string? parseError = null;

            if (response.StatusCode == 200)
            {
                using var stream = await response.GetStreamAsync().ConfigureAwait(false);

                using var decompressed =
                    response.GetContentEncodingType() == ContentEncodingType.GZip
                        ? new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true)
                        : null;

                // Every parameter has to be given to reach leaveOpen. A byte order mark is not
                // expected, and letting one be detected would override the declared encoding.
                using var reader = new StreamReader(
                    decompressed ?? stream,
                    response.GetCharsetEncoding(),
                    detectEncodingFromByteOrderMarks: false,
                    bufferSize: 1024, // the default
                    leaveOpen: true);

                if (UfcConfigurationParser.TryParse(reader, out var parsed, out parseError))
                {
                    configuration = parsed;
                }
            }

            return new PollResult(response.StatusCode, response.GetHeader("ETag"), configuration, parseError, error: null);
        }
        catch (Exception ex)
        {
            return new PollResult(statusCode: null, etag: null, configuration: null, parseError: null, error: ex);
        }
    }

    private void Apply(in PollResult result)
    {
        switch (result.StatusCode)
        {
            case 304:
                // Nothing changed, and the ETag stays as it is.
                return;
            case 401 or 403:
                WarnFailure(in result, attempts: 1);
                return;
            case not 200:
                WarnFailure(in result, attempts: 1);
                return;
        }

        if (result.Configuration is not { } configuration)
        {
            if (!_malformedPayloadLogged)
            {
                _malformedPayloadLogged = true;
                Log.Error("Feature Flags agentless endpoint returned an unusable payload: {Error}", result.ParseError);
            }

            return;
        }

        if (!_applyConfiguration(configuration))
        {
            if (!_applyFailureLogged)
            {
                _applyFailureLogged = true;
                Log.Warning("Feature Flags agentless configuration could not be applied");
            }

            return;
        }

        // The ETag advances only once parsing and applying have both succeeded. Advancing on
        // receipt would acknowledge a payload that was never applied, and every later poll would
        // answer 304, pinning the process to stale configuration with no way back.
        var newEtag = result.ETag?.Trim();
        _etag = StringUtil.IsNullOrEmpty(newEtag) ? null : newEtag;
    }

    /// <summary>
    /// Warns once per failure category. A dead endpoint would otherwise produce a warning every
    /// poll interval, indefinitely.
    /// </summary>
    private void WarnFailure(in PollResult result, int attempts)
    {
        var category = result.StatusCode switch
        {
            401 or 403 => "authentication",
            not null => "http",
            _ => "request",
        };

        if (!_loggedFailureCategories.Add(category))
        {
            return;
        }

        switch (result.StatusCode)
        {
            case 401 or 403:
                Log.Warning<int>("Feature Flags agentless endpoint returned HTTP {StatusCode}; verify endpoint authentication", result.StatusCode!.Value);
                break;
            case not null:
                Log.Warning<int, int>("Feature Flags agentless endpoint returned HTTP {StatusCode} after {Attempts} attempts", result.StatusCode.Value, attempts);
                break;
            default:
                Log.Warning<int>(result.Error, "Feature Flags agentless request failed after {Attempts} attempts", attempts);
                break;
        }
    }

    internal readonly struct PollResult(int? statusCode, string? etag, ServerConfiguration? configuration, string? parseError, Exception? error)
    {
        public int? StatusCode { get; } = statusCode;

        public string? ETag { get; } = etag;

        public ServerConfiguration? Configuration { get; } = configuration;

        public string? ParseError { get; } = parseError;

        public Exception? Error { get; } = error;
    }
}

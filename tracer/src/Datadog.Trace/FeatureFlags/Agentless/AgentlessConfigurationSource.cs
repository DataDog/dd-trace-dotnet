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
    private readonly Uri _endpoint;
    private readonly TimeSpan _pollInterval;
    private readonly TimeSpan _requestTimeout;
    private readonly Func<ServerConfiguration, bool> _applyConfiguration;
    private readonly Func<TimeSpan, CancellationToken, Task> _waitAsync;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Random _random = new();

    // Only ever touched from the poll loop.
    private readonly HashSet<string> _loggedFailureCategories = new();
    private bool _malformedPayloadLogged;
    private bool _applyFailureLogged;
    private string? _etag;

    private int _started;

    internal AgentlessConfigurationSource(
        Uri endpoint,
        IApiRequestFactory requestFactory,
        TimeSpan pollInterval,
        TimeSpan requestTimeout,
        Func<ServerConfiguration, bool> applyConfiguration,
        Func<TimeSpan, CancellationToken, Task>? waitAsync = null)
    {
        _endpoint = endpoint;
        _requestFactory = requestFactory;
        _pollInterval = pollInterval;
        _requestTimeout = requestTimeout;
        _applyConfiguration = applyConfiguration;
        _waitAsync = waitAsync ?? Task.Delay;
    }

    /// <summary>
    /// Creates the source, or returns <c>null</c> when it cannot be operated: a base URL that is
    /// not a URL, or the managed endpoint without an API key. Polling anyway would only produce
    /// failures every interval.
    /// </summary>
    public static AgentlessConfigurationSource? Create(FeatureFlagsSettings settings, Func<ServerConfiguration, bool> applyConfiguration)
    {
        if (!AgentlessEndpoint.TryCreate(settings.Site, settings.Env, settings.AgentlessBaseUrl, out var endpoint, out var error))
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
            endpoint.Uri,
            CreateRequestFactory(endpoint, settings),
            settings.PollInterval,
            settings.RequestTimeout,
            applyConfiguration);
    }

    /// <summary>
    /// Starts polling. Idempotent.
    /// </summary>
    public void Start()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
        {
            return;
        }

        // Deliberately not wrapped in Task.Run: this is called from provider initialization, which
        // is waiting for the first configuration, so the first request should go out on the calling
        // thread rather than queue behind whatever else is on the thread pool.
        _ = RunAsync().ContinueWith(t => Log.Error(t.Exception, "Feature Flags agentless poll loop failed"), TaskContinuationOptions.OnlyOnFaulted);
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

            if (_shutdown.IsCancellationRequested)
            {
                // A shutdown mid-poll leaves the response unusable for state transitions: keep
                // last-known-good and the current ETag.
                return;
            }

            if (!IsRetryable(result))
            {
                break;
            }

            if (attempt == MaxAttempts)
            {
                // Every attempt failed in a retryable way. Last-known-good stays in place.
                WarnFailure(result, MaxAttempts);
                return;
            }

            await WaitAsync(RetryDelay(attempt)).ConfigureAwait(false);

            if (_shutdown.IsCancellationRequested)
            {
                return;
            }
        }

        if (_shutdown.IsCancellationRequested)
        {
            // A shutdown during the final attempt leaves the response unusable for state
            // transitions: keep last-known-good and the current ETag.
            return;
        }

        await ApplyAsync(result).ConfigureAwait(false);
    }

    public void Dispose()
    {
        // The request in flight is bounded by the request timeout, and the loop is never joined,
        // so a shutdown does not wait for it. A poll that completes after disposal is prevented
        // from applying its result by the shutdown check in PollAsync.
        try
        {
            _shutdown.Cancel();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error cancelling the Feature Flags agentless poll loop");
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

#if NETCOREAPP
        return new HttpClientRequestFactory(endpoint.Uri, headers.ToArray(), timeout: settings.RequestTimeout);
#else
        return new ApiWebRequestFactory(endpoint.Uri, headers.ToArray(), timeout: settings.RequestTimeout);
#endif
    }

    private static bool IsRetryable(in PollResult result)
        => result.StatusCode is not { } status || status is 408 or 429 or (>= 500 and <= 599);

    private async Task RunAsync()
    {
        Log.Debug("AgentlessConfigurationSource::RunAsync -> Enter");

        while (!_shutdown.IsCancellationRequested)
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

    private async Task WaitAsync(TimeSpan delay)
    {
        try
        {
            await _waitAsync(delay, _shutdown.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
    }

    private TimeSpan RetryDelay(int attempt)
    {
        var seconds = attempt == 1
                          ? Clamp(_pollInterval.TotalSeconds / 6, FirstRetryMin, FirstRetryMax)
                          : Clamp(_pollInterval.TotalSeconds / 3, SecondRetryMin, SecondRetryMax);

        double jitter;
        lock (_random)
        {
            jitter = 1 - RetryJitter + (_random.NextDouble() * RetryJitter * 2);
        }

        return TimeSpan.FromSeconds(Math.Max(MinRetryDelay.TotalSeconds, seconds * jitter));

        static double Clamp(double value, TimeSpan minimum, TimeSpan maximum)
            => Math.Max(minimum.TotalSeconds, Math.Min(maximum.TotalSeconds, value));
    }

    private async Task<PollResult> RequestAsync()
    {
        try
        {
            var request = _requestFactory.Create(_endpoint);
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

                return new PollResult(statusCode: null, etag: null, body: null, error: new TimeoutException($"Feature Flags agentless request timed out after {_requestTimeout.TotalSeconds}s"));
            }

            using var response = await getTask.ConfigureAwait(false);
#endif

            // Only a 200 carries configuration; other bodies are never decoded as one.
            var body = response.StatusCode == 200 ? await ReadBodyAsync(response).ConfigureAwait(false) : null;
            return new PollResult(response.StatusCode, response.GetHeader("ETag"), body, error: null);
        }
        catch (Exception ex)
        {
            return new PollResult(statusCode: null, etag: null, body: null, error: ex);
        }
    }

    private async Task<string> ReadBodyAsync(IApiResponse response)
    {
        var stream = await response.GetStreamAsync().ConfigureAwait(false);
        GZipStream? decompressed = null;

        try
        {
            if (response.GetContentEncodingType() == ContentEncodingType.GZip)
            {
                decompressed = new GZipStream(stream, CompressionMode.Decompress);
            }

            using var reader = new StreamReader(decompressed ?? stream, response.GetCharsetEncoding());
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        finally
        {
            decompressed?.Dispose();
        }
    }

    private Task ApplyAsync(PollResult result)
    {
        switch (result.StatusCode)
        {
            case 304:
                // Nothing changed, and the ETag stays as it is.
                return Task.CompletedTask;
            case 401 or 403:
                WarnFailure(result, attempts: 1);
                return Task.CompletedTask;
            case not 200:
                WarnFailure(result, attempts: 1);
                return Task.CompletedTask;
        }

        if (!UfcConfigurationParser.TryParse(result.Body, out var configuration, out var error))
        {
            if (!_malformedPayloadLogged)
            {
                _malformedPayloadLogged = true;
                Log.Error("Feature Flags agentless endpoint returned an unusable payload: {Error}", error);
            }

            return Task.CompletedTask;
        }

        if (!_applyConfiguration(configuration))
        {
            if (!_applyFailureLogged)
            {
                _applyFailureLogged = true;
                Log.Warning("Feature Flags agentless configuration could not be applied");
            }

            return Task.CompletedTask;
        }

        // The ETag advances only once parsing and applying have both succeeded. Advancing on
        // receipt would acknowledge a payload that was never applied, and every later poll would
        // answer 304, pinning the process to stale configuration with no way back.
        var newEtag = result.ETag?.Trim();
        _etag = StringUtil.IsNullOrEmpty(newEtag) ? null : newEtag;

        return Task.CompletedTask;
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
                Log.Error<int>("Feature Flags agentless endpoint returned HTTP {StatusCode}; verify endpoint authentication", result.StatusCode!.Value);
                break;
            case not null:
                Log.Error<int, int>("Feature Flags agentless endpoint returned HTTP {StatusCode} after {Attempts} attempts", result.StatusCode.Value, attempts);
                break;
            default:
                Log.Error<int>(result.Error, "Feature Flags agentless request failed after {Attempts} attempts", attempts);
                break;
        }
    }

    internal readonly struct PollResult(int? statusCode, string? etag, string? body, Exception? error)
    {
        public int? StatusCode { get; } = statusCode;

        public string? ETag { get; } = etag;

        public string? Body { get; } = body;

        public Exception? Error { get; } = error;
    }
}

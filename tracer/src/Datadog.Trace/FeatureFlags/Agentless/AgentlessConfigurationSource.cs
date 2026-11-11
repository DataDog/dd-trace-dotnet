// <copyright file="AgentlessConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// Polls the agentless endpoint for flag configuration. Polling is billable, so it is only
/// started once application code has activated the provider.
/// </summary>
internal sealed class AgentlessConfigurationSource : IFeatureFlagsDeliverySource
{
    private const int MaxAttempts = 3;
    private const double RetryJitter = 0.2;

    // The CDN attributes usage only to a service of at most 100 characters. The Agent cuts a service
    // to 100 bytes of UTF-8.
    private const int MaxServiceLength = 100;

    private static readonly TimeSpan FirstRetryMin = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FirstRetryMax = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SecondRetryMin = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SecondRetryMax = TimeSpan.FromSeconds(30);

    // A jittered retry delay never drops below this, so a short poll interval cannot turn
    // retries into a burst against the endpoint.
    private static readonly TimeSpan MinRetryDelay = TimeSpan.FromSeconds(1);

    private readonly IDatadogLogger _log;
    private readonly IApiRequestFactory _requestFactory;
    private readonly AgentlessEndpoint _endpoint;
    private readonly TimeSpan _pollInterval;
    private readonly Func<ServerConfiguration, bool> _applyConfiguration;
    private readonly Func<TimeSpan, Task> _waitAsync;

    // Not a CancellationTokenSource: cancellation throws, and an exception on the shutdown path can
    // crash the runtime, so shutdown is signalled by completing a task instead.
    private readonly TaskCompletionSource<bool> _shutdown = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Only ever touched from the poll loop.
    private readonly HashSet<string> _loggedFailureCategories = new();

    private readonly IDisposable? _settingsSubscription;

    // Written by the settings-change callback, read by the poll loop. The URI is stored rather than
    // the environment it carries, so it is only built when the environment changes. The environment
    // the URI sends travels with it, so a response is reported against the environment its own
    // request used, even when the setting changes while that request is in flight.
    private RequestTarget _requestTarget;

    // The settings-change callback writes this value, and the poll loop reads it. It is null when
    // requests send no DD-Service header.
    private string? _service;

    // The setting that gives _service. Only the constructor and the settings-change callback use it.
    private string? _serviceSetting;

    // Only ever touched from the poll loop.
    private bool _hasApplied;
    private bool _malformedPayloadLogged;
    private bool _applyFailureLogged;
    private string? _etag;
    private Uri? _etagUri;

    private int _started;

    internal AgentlessConfigurationSource(
        AgentlessEndpoint endpoint,
        IApiRequestFactory requestFactory,
        TimeSpan pollInterval,
        Func<ServerConfiguration, bool> applyConfiguration,
        string? environment = null,
        string? service = null,
        TracerSettings.SettingsManager? settingsManager = null,
        Func<TimeSpan, Task>? waitAsync = null,
        IDatadogLogger? logger = null)
    {
        _log = logger ?? DatadogLogging.GetLoggerFor(typeof(AgentlessConfigurationSource));
        _endpoint = endpoint;
        _requestFactory = requestFactory;
        _pollInterval = pollInterval;
        _applyConfiguration = applyConfiguration;
        _requestTarget = new RequestTarget(endpoint, environment);
        _waitAsync = waitAsync ?? Task.Delay;
        UpdateService(service);

        // The source subscribes last, so the callback finds every field set. Code can change the
        // environment and the service while the application runs, so the source tracks both. The CDN
        // targets flags per environment and attributes usage per service.
        _settingsSubscription = settingsManager?.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedMutable is { } mutable)
            {
                UpdateEnvironment(mutable.Environment);
                UpdateService(mutable.DefaultServiceName);
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
        Func<ServerConfiguration, bool> applyConfiguration,
        IDatadogLogger? logger = null,
        IApiRequestFactory? requestFactory = null)
    {
        var log = logger ?? DatadogLogging.GetLoggerFor(typeof(AgentlessConfigurationSource));

        if (!AgentlessEndpoint.TryCreate(settings.Site, settings.AgentlessBaseUrl, out var endpoint, out var error))
        {
            log.Error("Feature Flags agentless source is unavailable: {Error}", error);
            return null;
        }

        if (endpoint.IsManaged && StringUtil.IsNullOrEmpty(settings.ApiKey))
        {
            log.Error("Feature Flags agentless source requires an API key. Set DD_API_KEY, or point DD_FEATURE_FLAGS_CONFIGURATION_SOURCE_AGENTLESS_BASE_URL at an endpoint of your own.");
            return null;
        }

        log.Debug<string, double, double>(
            "Feature Flags: starting agentless configuration source at {Origin} (poll interval {PollIntervalSeconds}s, request timeout={RequestTimeoutSeconds}s)",
            RedactedOrigin(endpoint.Uri),
            settings.PollInterval.TotalSeconds,
            settings.RequestTimeout.TotalSeconds);

        return new AgentlessConfigurationSource(
            endpoint,
            requestFactory ?? CreateRequestFactory(endpoint, settings),
            settings.PollInterval,
            applyConfiguration,
            manager.InitialMutableSettings.Environment,
            manager.InitialMutableSettings.DefaultServiceName,
            manager,
            logger: log);
    }

    /// <summary>
    /// Records the environment to request configuration for, as the URI that carries it. Picked up
    /// by the poll loop on its next request, so a change never disturbs a request already in flight.
    /// </summary>
    internal void UpdateEnvironment(string? environment)
    {
        // Every settings change lands here, so the target is only rebuilt, and the change reported,
        // when what is actually sent changes. The URI is derived from the normalized environment
        // alone, so comparing that is enough. A configured base URL never sends an environment, so
        // it never gets past this check.
        var requested = _endpoint.RequestEnvironment(environment);
        if (string.Equals(Volatile.Read(ref _requestTarget).Environment, requested, StringComparison.Ordinal))
        {
            return;
        }

        Volatile.Write(ref _requestTarget, new RequestTarget(_endpoint, environment));
        _log.Debug<string>("Feature Flags: agentless configuration source now requests env={RequestedEnvironment}", requested ?? "none");
    }

    /// <summary>
    /// Returns true when the CDN attributes usage to this DD-Service value. The CDN accepts only values
    /// that match <c>^[A-Za-z0-9][A-Za-z0-9_.:/-]{0,99}$</c>, and drops all other values.
    /// </summary>
    private static bool IsAttributableService(string service)
    {
        if (service.Length is 0 or > MaxServiceLength || !char.IsAsciiLetterOrDigit(service[0]))
        {
            return false;
        }

        for (var i = 1; i < service.Length; i++)
        {
            var c = service[i];
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '.' or ':' or '/' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the name APM shows for a service. <c>My Service</c> becomes <c>my_service</c>.
    /// The result can be empty, or a name the CDN drops, such as <c>_svc</c> or <c>café-api</c>.
    /// </summary>
    /// <remarks>
    /// This is a port of <c>NormalizeService</c> in the current Agent (pkg/trace/traceutil/normalize).
    /// <c>NormalizerTraceProcessor.NormalizeService</c> follows an older Agent. That version removes
    /// the characters before the first letter, so it changes <c>_svc</c> to <c>svc</c> and
    /// <c>2fa-service</c> to <c>fa-service</c>. APM does not show these names.
    /// </remarks>
    internal static string CleanUpService(string service)
    {
        // Most names do not change. The method copies the name only when the result stops being a
        // prefix of it.
        char[]? cleaned = null;
        var length = 0;
        var bytes = 0;

        // A run of replaced characters becomes one _ before the next kept character. At the end of
        // the name, the run is removed.
        var pending = false;

        var i = 0;
        while (i < service.Length)
        {
            var c = service[i];
            var width = char.IsSurrogatePair(service, i) ? 2 : 1;

            // The Agent first cuts the name to 100 bytes of UTF-8, and never splits a character.
            // A lone surrogate is sent as U+FFFD, which has 3 bytes.
            bytes += c switch
            {
                < '\u0080' => 1,
                < '\u0800' => 2,
                _ => width == 2 ? 4 : 3,
            };

            if (bytes > MaxServiceLength)
            {
                break;
            }

            bool kept;
            if (c < 0x80)
            {
                // Only ASCII letters are lowercased. The Agent also lowercases most other letters.
                // That has no effect on the header, because the CDN drops every non-ASCII name.
                if (c is >= 'A' and <= 'Z')
                {
                    c = (char)(c + ('a' - 'A'));
                }

                kept = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or ':' or '.' or '/' or '-';
            }
            else
            {
                kept = char.IsLetter(service, i) || char.IsDigit(service, i);
            }

            if (kept)
            {
                if (pending)
                {
                    Append(service, ref cleaned, ref length, '_');
                    pending = false;
                }

                Append(service, ref cleaned, ref length, c);
                if (width == 2)
                {
                    Append(service, ref cleaned, ref length, service[i + 1]);
                }
            }
            else
            {
                pending = true;
            }

            i += width;
        }

        if (cleaned is not null)
        {
            return new string(cleaned, 0, length);
        }

        return length == service.Length ? service : service.Substring(0, length);

        // Adds a character to the result. While the result is a prefix of the name, it only counts
        // the character. Each character of the result comes from at least one character of the name
        // before the cut, so the result has at most MaxServiceLength characters.
        static void Append(string service, ref char[]? cleaned, ref int length, char value)
        {
            if (cleaned is null)
            {
                if (length < service.Length && service[length] == value)
                {
                    length++;
                    return;
                }

                cleaned = new char[Math.Min(service.Length, MaxServiceLength)];
                service.CopyTo(0, cleaned, 0, length);
            }

            cleaned[length++] = value;
        }
    }

    /// <summary>
    /// Sets the service to send as DD-Service. The poll loop uses it on its next request.
    /// </summary>
    private void UpdateService(string? service)
    {
        // Every settings change calls this method. It does work only when the service setting
        // changes, so it logs a rejected name only once.
        if (string.Equals(_serviceSetting, service, StringComparison.Ordinal))
        {
            return;
        }

        _serviceSetting = service;

        string? sent = null;
        if (!StringUtil.IsNullOrEmpty(service))
        {
            // The header uses the name APM shows, so usage and APM name the same service. For
            // example, My Service is sent as my_service. The tracer's own service does not change.
            var shown = CleanUpService(service);

            // The CDN drops some names that APM shows, such as _svc and café-api. The source does
            // not send these names. It never sends a different name, because APM does not show it.
            if (IsAttributableService(shown))
            {
                sent = shown;
            }
            else
            {
                _log.Debug<string>("Feature Flags: the CDN does not accept {Service} as APM shows it; DD-Service header not sent", service);
            }
        }

        Volatile.Write(ref _service, sent);
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

        // The loop runs on the thread pool, so nothing of it happens on the caller's thread. Nothing
        // ever awaits it either, so a fault is observed here or not at all.
        _ = Task.Run(RunAsync).ContinueWith(t => _log.Error(t.Exception, "Feature Flags agentless poll loop failed"), TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Runs a single poll, including its in-tick retries.
    /// </summary>
    internal async Task PollAsync()
    {
        var result = default(PollResult);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            _log.Debug<int, int>("Feature Flags: agentless configuration poll attempt {Attempt}/{MaxAttempts}", attempt, MaxAttempts);
            result = await RequestAsync().ConfigureAwait(false);
            LogAttemptOutcome(in result, attempt);

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
                LogRetainedConfiguration("every attempt failed");
                return;
            }

            var retryDelay = RetryDelay(attempt);
            _log.Debug<double>("Feature Flags: retrying agentless configuration poll in {DelayMs}ms", retryDelay.TotalMilliseconds);
            await WaitAsync(retryDelay).ConfigureAwait(false);

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
        if (_shutdown.TrySetResult(true))
        {
            _log.Debug("Feature Flags: configuration polling stop requested; in-flight responses will not be applied");
        }

        try
        {
            _settingsSubscription?.Dispose();
        }
        catch (Exception ex)
        {
            _log.Debug(ex, "Error unsubscribing the Feature Flags agentless poll loop from settings changes");
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
            new(TelemetryConstants.ClientLibraryVersionHeader, TracerConstants.SemanticVersion),

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
        _log.Debug("AgentlessConfigurationSource::RunAsync -> Enter");

        while (!_shutdown.Task.IsCompleted)
        {
            try
            {
                await PollAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Feature Flags agentless poll failed unexpectedly");
            }

            // Fixed delay after completion, so polls never overlap.
            await WaitAsync(_pollInterval).ConfigureAwait(false);
        }

        _log.Debug("AgentlessConfigurationSource::RunAsync -> Exit");
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
        // Read per request rather than captured, because the environment it carries can be
        // changed in code after startup.
        var target = Volatile.Read(ref _requestTarget);

        try
        {
            var uri = target.Uri;

            // An ETag only identifies the configuration served for the URI it came from. Sending it
            // against a different environment would earn a 304 and pin the process to the previous
            // environment's flags, with no way back.
            if (_etagUri is not null && uri != _etagUri)
            {
                _etag = null;
            }

            _etagUri = uri;

            var request = _requestFactory.Create(uri);

            // The header lets the CDN attribute the usage of this request to the service. The source
            // reads the service for each request, because code can change it after startup.
            if (Volatile.Read(ref _service) is { } service)
            {
                request.AddHeader("DD-Service", service);
            }

            if (_etag is { } etag)
            {
                request.AddHeader("If-None-Match", etag);
            }

            using var response = await request.GetAsync().ConfigureAwait(false);

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

            return new PollResult(response.StatusCode, response.GetHeader("ETag"), configuration, parseError, error: null, requestedEnvironment: target.Environment);
        }
        catch (Exception ex)
        {
            return new PollResult(statusCode: null, etag: null, configuration: null, parseError: null, error: ex, requestedEnvironment: target.Environment);
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
                LogRetainedConfiguration("the endpoint rejected the credentials");
                return;
            case not 200:
                WarnFailure(in result, attempts: 1);
                LogRetainedConfiguration("the response status is not retryable");
                return;
        }

        if (result.Configuration is not { } configuration)
        {
            if (!_malformedPayloadLogged)
            {
                _malformedPayloadLogged = true;
                _log.Error("Feature Flags agentless endpoint returned an unusable payload: {Error}", result.ParseError);
            }

            // The error above is only written once, so every later rejection is reported here.
            LogRetainedConfiguration(result.ParseError ?? "the payload is unusable");
            return;
        }

        if (!_applyConfiguration(configuration))
        {
            if (!_applyFailureLogged)
            {
                _applyFailureLogged = true;
                _log.Warning("Feature Flags agentless configuration could not be applied");
            }

            LogRetainedConfiguration("the configuration could not be applied");
            return;
        }

        _hasApplied = true;

        // The ETag advances only once parsing and applying have both succeeded. Advancing on
        // receipt would acknowledge a payload that was never applied, and every later poll would
        // answer 304, pinning the process to stale configuration with no way back.
        var newEtag = result.ETag?.Trim();
        _etag = StringUtil.IsNullOrEmpty(newEtag) ? null : newEtag;

        // The payload's environment is reported next to the requested one, because a mismatch
        // between DD_ENV and where the flags are configured is a common reason nothing resolves.
        if (_log.IsEnabled(LogEventLevel.Debug))
        {
            _log.Debug(
                "Feature Flags: agentless configuration applied successfully ({UsableFlagCount} usable flag(s), {RejectedFlagCount} rejected, requested env={RequestedEnvironment}, configuration env={ConfigurationEnvironment}, createdAt={CreatedAt}, etag={ETag})",
                [
                    configuration.Flags?.ValidFlags.Count() ?? 0,
                    configuration.Flags?.InvalidFlagKeys.Count() ?? 0,
                    result.RequestedEnvironment ?? "none",
                    configuration.Environment?.Name ?? "unset",
                    configuration.CreatedAt ?? "unset",
                    _etag ?? "none",
                ]);
            FeatureFlagsDiagnostics.LogRejectedFlags(_log, "agentless", configuration);
        }
    }

    /// <summary>
    /// Reports, at debug level, what a poll that applied nothing leaves in place, so a failed refresh
    /// is never mistaken for losing the configuration, nor for having one.
    /// </summary>
    private void LogRetainedConfiguration(string reason)
    {
        var state = _hasApplied
                        ? "the previously applied configuration remains active"
                        : "no configuration has been applied yet, so evaluations return their default values";
        _log.Debug<string, string>("Feature Flags: agentless configuration was not updated ({Reason}); {State}", reason, state);
    }

    /// <summary>
    /// Keeps only the scheme, host and port: a custom base URL may carry credentials or a signed
    /// token in its user info, path or query.
    /// </summary>
    internal static string RedactedOrigin(Uri uri) => $"{uri.Scheme}://{uri.Authority}";

    /// <summary>
    /// Logs the outcome of a single attempt, at debug level, so a customer can tell a DNS failure from
    /// a refused connection, a rejected credential, or a configuration that has not changed. The
    /// warnings below fire once per category, so this is the only place every attempt is visible.
    /// </summary>
    private void LogAttemptOutcome(in PollResult result, int attempt)
    {
        // Describing a failure allocates, so it is skipped unless the line is actually written.
        if (!_log.IsEnabled(LogEventLevel.Debug))
        {
            return;
        }

        switch (result.StatusCode)
        {
            case 304:
                _log.Debug<int, int>("Feature Flags: agentless configuration poll attempt {Attempt}/{MaxAttempts} returned HTTP 304 (unchanged)", attempt, MaxAttempts);
                break;
            case null:
                _log.Debug<int, int, string>("Feature Flags: agentless configuration poll attempt {Attempt}/{MaxAttempts} failed: {Error}", attempt, MaxAttempts, DescribeError(result.Error));
                break;
            default:
                // The response body is not logged: an error page can echo the request back.
                _log.Debug<int, int, int>("Feature Flags: agentless configuration poll attempt {Attempt}/{MaxAttempts} returned HTTP {StatusCode}", attempt, MaxAttempts, result.StatusCode.Value);
                break;
        }

        // The innermost exception is what names the transport failure, such as a SocketException for
        // a DNS failure or a refused connection, rather than the generic wrapper around it.
        static string DescribeError(Exception? error)
        {
            if (error is null)
            {
                return "request was not sent";
            }

            var inner = error;
            while (inner.InnerException is not null)
            {
                inner = inner.InnerException;
            }

            return ReferenceEquals(inner, error)
                       ? $"{error.GetType().Name}: {error.Message}"
                       : $"{error.GetType().Name}: {error.Message} ({inner.GetType().Name}: {inner.Message})";
        }
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

        // An HTTP status when the endpoint answered. When it did not, the exception is logged with the
        // line, so only its type is named here.
        var failure = result.StatusCode switch
        {
            401 or 403 => $"HTTP {result.StatusCode}; verify endpoint authentication",
            not null => $"HTTP {result.StatusCode}",
            _ => result.Error?.GetType().Name ?? "request was not sent",
        };

        _log.Warning<string, int, string>(
            result.Error,
            "Feature Flags agentless request to {Origin} failed after {Attempts} attempt(s): {Failure}. See https://docs.datadoghq.com/feature_flags/",
            RedactedOrigin(_endpoint.Uri),
            attempts,
            failure);
    }

    internal readonly struct PollResult(int? statusCode, string? etag, ServerConfiguration? configuration, string? parseError, Exception? error, string? requestedEnvironment)
    {
        public int? StatusCode { get; } = statusCode;

        public string? ETag { get; } = etag;

        public ServerConfiguration? Configuration { get; } = configuration;

        public string? ParseError { get; } = parseError;

        public Exception? Error { get; } = error;

        /// <summary>
        /// Gets the environment the request sent as <c>dd_env</c>, normalized, or <c>null</c> when
        /// it sent none.
        /// </summary>
        public string? RequestedEnvironment { get; } = requestedEnvironment;
    }

    /// <summary>
    /// The URI requested for an environment, and the environment it sends. Immutable, so the poll
    /// loop reads both from a single reference.
    /// </summary>
    private sealed class RequestTarget(AgentlessEndpoint endpoint, string? environment)
    {
        public Uri Uri { get; } = endpoint.BuildRequestUri(environment);

        public string? Environment { get; } = endpoint.RequestEnvironment(environment);
    }
}

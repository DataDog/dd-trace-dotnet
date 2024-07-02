// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains Tracer settings that can be set in code.
/// </summary>
public sealed class TracerSettings
{
    private readonly bool _diagnosticSourceEnabled;
    private readonly bool _isFromDefaultSources;

    private OverrideValue<string?> _environment;
    private OverrideValue<string?> _serviceName;
    private OverrideValue<string?> _serviceVersion;
    private OverrideValue<bool> _analyticsEnabled;
    private OverrideValue<double?> _globalSamplingRate;
    private OverrideValue<IDictionary<string, string>> _globalTags;
    private OverrideValue<IDictionary<string, string>> _grpcTags;
    private OverrideValue<IDictionary<string, string>> _headerTags;
    private OverrideValue<bool> _kafkaCreateConsumerScopeEnabled;
    private OverrideValue<bool> _logsInjectionEnabled;
    private OverrideValue<int> _maxTracesSubmittedPerSecond;
    private OverrideValue<string?> _customSamplingRules;
    private OverrideValue<bool> _startupDiagnosticLogEnabled;
    private OverrideValue<bool> _traceEnabled;
    private OverrideValue<HashSet<string>> _disabledIntegrationNames;
    private OverrideValue<bool> _tracerMetricsEnabled;
    private OverrideValue<bool> _statsComputationEnabled;
    private OverrideValue<Uri> _agentUri;
    private List<int>? _httpClientErrorCodes;
    private List<int>? _httpServerErrorCodes;
    private Dictionary<string, string>? _serviceNameMappings;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
    /// </summary>
    [Instrumented]
    public TracerSettings()
        : this(PopulateDictionary(new(), useDefaultSources: false), isFromDefaultSources: false)
    {
        // TODO: _Currently_ this doesn't load _any_ configuration, so it feels like an error for customers to use it?
        // I'm wondering if we should _always_ populate from the default sources instead, as otherwise seems
        // like an obvious point of confusion?
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values,
    /// or initializes the configuration from environment variables and configuration files.
    /// Calling <c>new TracerSettings(true)</c> is equivalent to calling <c>TracerSettings.FromDefaultSources()</c>
    /// </summary>
    /// <param name="useDefaultSources">If <c>true</c>, creates a <see cref="TracerSettings"/> populated from
    /// the default sources such as environment variables etc. If <c>false</c>, uses the default values.</param>
    [Instrumented]
    public TracerSettings(bool useDefaultSources)
        : this(PopulateDictionary(new(), useDefaultSources), useDefaultSources)
    {
    }

    // Internal for testing
    internal TracerSettings(Dictionary<string, object?> initialValues, bool isFromDefaultSources)
    {
        // The values set here represent the defaults when there's no auto-instrumentation
        // We don't care too much if they get out of sync because that's not supported anyway
        _agentUri = TryGetValue<Uri>(initialValues, TracerSettingKeyConstants.AgentUriKey) ?? new OverrideValue<Uri>(new Uri("http://127.0.0.1:8126"));
        _analyticsEnabled = GetValue(initialValues, TracerSettingKeyConstants.AnalyticsEnabledKey, false);
        _customSamplingRules = GetValue<string?>(initialValues, TracerSettingKeyConstants.CustomSamplingRules, null);
        _disabledIntegrationNames = GetAsHashSet(initialValues, TracerSettingKeyConstants.DisabledIntegrationNamesKey);
        _diagnosticSourceEnabled = GetValue(initialValues, TracerSettingKeyConstants.DiagnosticSourceEnabledKey, false).Value;
        _environment = GetValue<string?>(initialValues, TracerSettingKeyConstants.EnvironmentKey, null);
        _globalSamplingRate = GetValue<double?>(initialValues, TracerSettingKeyConstants.GlobalSamplingRateKey, null);
        _globalTags = GetAsDictionary(initialValues, TracerSettingKeyConstants.GlobalTagsKey);
        _grpcTags = GetAsDictionary(initialValues, TracerSettingKeyConstants.GrpcTags);
        _headerTags = GetAsDictionary(initialValues, TracerSettingKeyConstants.HeaderTags);
        _kafkaCreateConsumerScopeEnabled = GetValue(initialValues, TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey, true);
        _logsInjectionEnabled = GetValue(initialValues, TracerSettingKeyConstants.LogsInjectionEnabledKey, false);
        _maxTracesSubmittedPerSecond = GetValue(initialValues, TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey, 100);
        _serviceName = GetValue<string?>(initialValues, TracerSettingKeyConstants.ServiceNameKey, null);
        _serviceVersion = GetValue<string?>(initialValues, TracerSettingKeyConstants.ServiceVersionKey, null);
        _startupDiagnosticLogEnabled = GetValue(initialValues, TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey, true);
        _statsComputationEnabled = GetValue(initialValues, TracerSettingKeyConstants.StatsComputationEnabledKey, true);
        _traceEnabled = GetValue(initialValues, TracerSettingKeyConstants.TraceEnabledKey, true);
        _tracerMetricsEnabled = GetValue(initialValues, TracerSettingKeyConstants.TracerMetricsEnabledKey, false);
        _isFromDefaultSources = isFromDefaultSources;

        // This is just a bunch of indirection to not change the public API for now
#pragma warning disable CS0618 // Type or member is obsolete
        Exporter = new ExporterSettings(this);
#pragma warning restore CS0618 // Type or member is obsolete

        Integrations = IntegrationSettingsHelper.ParseFromAutomatic(initialValues);

        static OverrideValue<HashSet<string>> GetAsHashSet(Dictionary<string, object?> results, string key)
        {
            var initial = TryGetValue<HashSet<string>?>(results, key);
            // we copy these so we can detect changes later (including replacement)
            return initial?.Value is { } value
                       ? new OverrideValue<HashSet<string>>(value, @override: new HashSet<string>(value))
                       : new OverrideValue<HashSet<string>>(initial: null!, new HashSet<string>());
        }

        static OverrideValue<IDictionary<string, string>> GetAsDictionary(Dictionary<string, object?> results, string key)
        {
            var initial = TryGetValue<IDictionary<string, string>?>(results, key);
            // we copy these so we can detect changes later (including replacement)
            return initial?.Value is { } value
                       ? new OverrideValue<IDictionary<string, string>>(value, @override: new ConcurrentDictionary<string, string>(value))
                       : new OverrideValue<IDictionary<string, string>>(initial: null!, new ConcurrentDictionary<string, string>());
        }

        static OverrideValue<T> GetValue<T>(Dictionary<string, object?> results, string key, T defaultValue)
            => TryGetValue<T>(results, key) ?? new OverrideValue<T>(defaultValue);

        static OverrideValue<T>? TryGetValue<T>(Dictionary<string, object?> results, string key)
        {
            if (results.TryGetValue(key, out var value)
                && value is T t)
            {
                return new OverrideValue<T>(t);
            }

            return null;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the use
    /// of System.Diagnostics.DiagnosticSource is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    /// <remark>
    /// This value cannot be set in code. Instead,
    /// set it using the <c>DD_DIAGNOSTIC_SOURCE_ENABLED</c>
    /// environment variable or in configuration files.
    /// </remark>
    [Instrumented]
    public bool DiagnosticSourceEnabled
    {
        get => _diagnosticSourceEnabled;

        [Obsolete("This value cannot be set in code. Instead, set it using the DD_DIAGNOSTIC_SOURCE_ENABLED environment variable, or in configuration files")]
        set
        {
            // As this was previously obsolete, we could just remove it?
            // Alternatively, mark it as an error instead?
        }
    }

    /// <summary>
    /// Gets or sets the default environment name applied to all spans.
    /// Can also be set via DD_ENV.
    /// </summary>
    public string? Environment
    {
        [Instrumented]
        get => _environment.Value;
        set => _environment = _environment.Override(value);
    }

    /// <summary>
    /// Gets or sets the service name applied to top-level spans and used to build derived service names.
    /// Can also be set via DD_SERVICE.
    /// </summary>
    public string? ServiceName
    {
        [Instrumented]
        get => _serviceName.Value;
        set => _serviceName = _serviceName.Override(value);
    }

    /// <summary>
    /// Gets or sets the version tag applied to all spans.
    /// </summary>
    public string? ServiceVersion
    {
        [Instrumented]
        get => _serviceVersion.Value;
        set => _serviceVersion = _serviceVersion.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether default Analytics are enabled.
    /// Settings this value is a shortcut for setting
    /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
    /// See the documentation for more details.
    /// </summary>
    [Obsolete(DeprecationMessages.AppAnalytics)]
    public bool AnalyticsEnabled
    {
        [Instrumented]
        get => _analyticsEnabled.Value;
        set => _analyticsEnabled = _analyticsEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating a global rate for sampling.
    /// </summary>
    public double? GlobalSamplingRate
    {
        [Instrumented]
        get => _globalSamplingRate.Value;
        set => _globalSamplingRate = _globalSamplingRate.Override(value);
    }

    /// <summary>
    /// Gets or sets the global tags, which are applied to all <see cref="ISpan"/>s.
    /// </summary>
    public IDictionary<string, string> GlobalTags
    {
        [Instrumented]
        get => _globalTags.Value;
        set => _globalTags = _globalTags.Override(value);
    }

    /// <summary>
    /// Gets or sets the map of metadata keys to tag names, which are applied to the root <see cref="ISpan"/>
    /// of incoming and outgoing GRPC requests.
    /// </summary>
    public IDictionary<string, string> GrpcTags
    {
        [Instrumented]
        get => _grpcTags.Value;
        set => _grpcTags = _grpcTags.Override(value);
    }

    /// <summary>
    /// Gets or sets the map of header keys to tag names, which are applied to the root <see cref="ISpan"/>
    /// of incoming and outgoing HTTP requests.
    /// </summary>
    public IDictionary<string, string> HeaderTags
    {
        [Instrumented]
        get => _headerTags.Value;
        set => _headerTags = _headerTags.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a span context should be created on exiting a successful Kafka
    /// Consumer.Consume() call, and closed on entering Consumer.Consume().
    /// </summary>
    public bool KafkaCreateConsumerScopeEnabled
    {
        [Instrumented]
        get => _kafkaCreateConsumerScopeEnabled.Value;
        set => _kafkaCreateConsumerScopeEnabled = _kafkaCreateConsumerScopeEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether correlation identifiers are
    /// automatically injected into the logging context.
    /// Default is <c>false</c>, unless Direct Log Submission is enabled.
    /// </summary>
    public bool LogsInjectionEnabled
    {
        [Instrumented]
        get => _logsInjectionEnabled.Value;
        set => _logsInjectionEnabled = _logsInjectionEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
    /// Default is <c>100</c>.
    /// </summary>
    public int MaxTracesSubmittedPerSecond
    {
        [Instrumented]
        get => _maxTracesSubmittedPerSecond.Value;
        set => _maxTracesSubmittedPerSecond = _maxTracesSubmittedPerSecond.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating custom sampling rules.
    /// </summary>
    public string? CustomSamplingRules
    {
        [Instrumented]
        get => _customSamplingRules.Value;
        set => _customSamplingRules = _customSamplingRules.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the diagnostic log at startup is enabled
    /// </summary>
    public bool StartupDiagnosticLogEnabled
    {
        [Instrumented]
        get => _startupDiagnosticLogEnabled.Value;
        set => _startupDiagnosticLogEnabled = _startupDiagnosticLogEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    public bool TraceEnabled
    {
        [Instrumented]
        get => _traceEnabled.Value;
        set => _traceEnabled = _traceEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets the names of disabled integrations.
    /// </summary>
    public HashSet<string> DisabledIntegrationNames
    {
        [Instrumented]
        get => _disabledIntegrationNames.Value;
        set => _disabledIntegrationNames = _disabledIntegrationNames.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether internal metrics
    /// are enabled and sent to DogStatsd.
    /// </summary>
    public bool TracerMetricsEnabled
    {
        [Instrumented]
        get => _tracerMetricsEnabled.Value;
        set => _tracerMetricsEnabled = _tracerMetricsEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether stats are computed on the tracer side
    /// </summary>
    public bool StatsComputationEnabled
    {
        [Instrumented]
        get => _statsComputationEnabled.Value;
        set => _statsComputationEnabled = _statsComputationEnabled.Override(value);
    }

    /// <summary>
    /// Gets or sets the Uri where the Tracer can connect to the Agent.
    /// Default is <c>"http://localhost:8126"</c>.
    /// </summary>
    public Uri AgentUri
    {
        [Instrumented]
        get => _agentUri.Value;
        set => _agentUri = _agentUri.Override(value);
    }

    /// <summary>
    /// Gets a collection of <see cref="IntegrationSettings"/> keyed by integration name.
    /// </summary>
    [Instrumented]
    public IntegrationSettingsCollection Integrations { get; }

    /// <summary>
    /// Gets the transport settings that dictate how the tracer connects to the agent.
    /// </summary>
    [Obsolete("This property is obsolete and will be removed in a future version. To set the AgentUri, use the TracerSettings.AgentUri property")]
    [Instrumented]
    public ExporterSettings Exporter { get; }

    /// <summary>
    /// Create a <see cref="TracerSettings"/> populated from the default sources.
    /// </summary>
    /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
    [Instrumented]
    public static TracerSettings FromDefaultSources() => new(PopulateDictionary(new(), useDefaultSources: true), isFromDefaultSources: true);

    /// <summary>
    /// Sets the HTTP status code that should be marked as errors for client integrations.
    /// </summary>
    /// <param name="statusCodes">Status codes that should be marked as errors</param>
    public void SetHttpClientErrorStatusCodes(IEnumerable<int> statusCodes)
    {
        // Check for null to be safe as it's a public API.
        // We throw in Datadog.Trace so it's the same behaviour, just a better error message
        if (statusCodes is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(statusCodes));
        }

        _httpClientErrorCodes = statusCodes.ToList();
    }

    /// <summary>
    /// Sets the HTTP status code that should be marked as errors for server integrations.
    /// </summary>
    /// <param name="statusCodes">Status codes that should be marked as errors</param>
    public void SetHttpServerErrorStatusCodes(IEnumerable<int> statusCodes)
    {
        // Check for null to be safe as it's a public API.
        // We throw in Datadog.Trace so it's the same behaviour, just a better error message
        if (statusCodes is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(statusCodes));
        }

        _httpServerErrorCodes = statusCodes.ToList();
    }

    /// <summary>
    /// Sets the mappings to use for service names within a <see cref="ISpan"/>
    /// </summary>
    /// <param name="mappings">Mappings to use from original service name (e.g. <code>sql-server</code> or <code>graphql</code>)
    /// as the <see cref="KeyValuePair{TKey, TValue}.Key"/>) to replacement service names as <see cref="KeyValuePair{TKey, TValue}.Value"/>).</param>
    [PublicApi]
    public void SetServiceNameMappings(IEnumerable<KeyValuePair<string, string>> mappings)
    {
        // Check for null to be safe as it's a public API.
        // We throw in Datadog.Trace so it's the same behaviour, just a better error message
        if (mappings is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(mappings));
        }

        _serviceNameMappings = mappings.ToDictionary(x => x.Key, x => x.Value);
    }

    [Instrumented]
    private static Dictionary<string, object?> PopulateDictionary(Dictionary<string, object?> values, bool useDefaultSources)
    {
        // The automatic tracer populates the dictionary with values which are then used to create the tracer
        _ = useDefaultSources;
        return values;
    }

    internal Dictionary<string, object?> ToDictionary()
    {
        // Could probably source gen this if we can be bothered
        var results = new Dictionary<string, object?>();

        AddIfChanged(results, TracerSettingKeyConstants.AgentUriKey, _agentUri);
        AddIfChanged(results, TracerSettingKeyConstants.AnalyticsEnabledKey, _analyticsEnabled);
        AddIfChanged(results, TracerSettingKeyConstants.EnvironmentKey, _environment);
        AddIfChanged(results, TracerSettingKeyConstants.CustomSamplingRules, _customSamplingRules);
        AddIfChanged(results, TracerSettingKeyConstants.GlobalSamplingRateKey, _globalSamplingRate);
        AddIfChanged(results, TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey, _kafkaCreateConsumerScopeEnabled);
        AddIfChanged(results, TracerSettingKeyConstants.LogsInjectionEnabledKey, _logsInjectionEnabled);
        AddIfChanged(results, TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey, _maxTracesSubmittedPerSecond);
        AddIfChanged(results, TracerSettingKeyConstants.ServiceNameKey, _serviceName);
        AddIfChanged(results, TracerSettingKeyConstants.ServiceVersionKey, _serviceVersion);
        AddIfChanged(results, TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey, _startupDiagnosticLogEnabled);
        AddIfChanged(results, TracerSettingKeyConstants.StatsComputationEnabledKey, _statsComputationEnabled);
        AddIfChanged(results, TracerSettingKeyConstants.TraceEnabledKey, _traceEnabled);
        AddIfChanged(results, TracerSettingKeyConstants.TracerMetricsEnabledKey, _tracerMetricsEnabled);

        // We have to check if any of the tags have changed
        AddIfChangedDictionary(results, TracerSettingKeyConstants.GlobalTagsKey, _globalTags);
        AddIfChangedDictionary(results, TracerSettingKeyConstants.GrpcTags, _grpcTags);
        AddIfChangedDictionary(results, TracerSettingKeyConstants.HeaderTags, _headerTags);
        AddIfChangedHashSet(results, TracerSettingKeyConstants.DisabledIntegrationNamesKey, _disabledIntegrationNames);

        // These are write-only, so only send them if we have them
        if (_serviceNameMappings is not null)
        {
            results.Add(TracerSettingKeyConstants.ServiceNameMappingsKey, _serviceNameMappings);
        }

        if (_httpClientErrorCodes is not null)
        {
            results.Add(TracerSettingKeyConstants.HttpClientErrorCodesKey, _httpClientErrorCodes);
        }

        if (_httpServerErrorCodes is not null)
        {
            results.Add(TracerSettingKeyConstants.HttpServerErrorCodesKey, _httpServerErrorCodes);
        }

        // Always set
        results[TracerSettingKeyConstants.IsFromDefaultSourcesKey] = _isFromDefaultSources;
        if (BuildIntegrationSettings(Integrations) is { } integrations)
        {
            results[TracerSettingKeyConstants.IntegrationSettingsKey] = integrations;
        }

        return results;

        static void AddIfChanged<T>(Dictionary<string, object?> results, string key, in OverrideValue<T> updated)
        {
            if (updated.IsOverridden)
            {
                results.Add(key, updated.Value);
            }
        }

        static void AddIfChangedHashSet(Dictionary<string, object?> results, string key, in OverrideValue<HashSet<string>> updated)
        {
            // we always have an override, but are they the same?
            var initial = updated.Initial;
            var value = updated.Value;

            if (initial is null)
            {
                if (value.Count != 0)
                {
                    results.Add(key, value);
                }
            }
            else
            {
                if ((initial.Count != value.Count) || !initial.SetEquals(value))
                {
                    results.Add(key, value);
                }
            }
        }

        static void AddIfChangedDictionary(Dictionary<string, object?> results, string key, in OverrideValue<IDictionary<string, string>> updated)
        {
            if (HasChanges(in updated))
            {
                results[key] = updated.Value;
            }

            return;

            static bool HasChanges(in OverrideValue<IDictionary<string, string>> updated)
            {
                // initial could be null, but value is never null
                var initial = updated.Initial;
                var value = updated.Value;

                // Currently need to account for customers _replacing_ the Global Tags as well as changing it
                // we create the updated one as a concurrent dictionary, so if it's not any more, then we know they've replaced it
                if (value is not ConcurrentDictionary<string, string> || (initial?.Count ?? 0) != value.Count)
                {
                    return true;
                }

                if (initial is null)
                {
                    return value.Count != 0;
                }

                var comparer = StringComparer.Ordinal;
                foreach (var kvp in initial)
                {
                    if (!value.TryGetValue(kvp.Key, out var value2)
                     || !comparer.Equals(kvp.Value, value2))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        static Dictionary<string, object?[]>? BuildIntegrationSettings(IntegrationSettingsCollection settings)
        {
            if (settings.Settings.Count == 0)
            {
                return null;
            }

            var results = new Dictionary<string, object?[]>(settings.Settings.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in settings.Settings)
            {
                var setting = pair.Value;
                if (setting.GetChangeDetails() is { } changes)
                {
                    results[setting.IntegrationName] = changes;
                }
            }

            return results;
        }
    }
}

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
    private readonly bool _diagnosticSourceEnabledInitial;
    private readonly string? _environmentInitial;
    private readonly string? _serviceNameInitial;
    private readonly string? _serviceVersionInitial;
    private readonly bool _analyticsEnabledInitial;
    private readonly double? _globalSamplingRateInitial;
    private readonly IDictionary<string, string> _globalTagsInitial;
    private readonly IDictionary<string, string> _grpcTagsInitial;
    private readonly IDictionary<string, string> _headerTagsInitial;
    private readonly bool _kafkaCreateConsumerScopeEnabledInitial;
    private readonly bool _logsInjectionEnabledInitial;
    private readonly int _maxTracesSubmittedPerSecondInitial;
    private readonly string? _customSamplingRulesInitial;
    private readonly bool _startupDiagnosticLogEnabledInitial;
    private readonly bool _traceEnabledInitial;
    private readonly HashSet<string> _disabledIntegrationNamesInitial;
    private readonly bool _tracerMetricsEnabledInitial;
    private readonly bool _statsComputationEnabledInitial;
    private readonly Uri _agentUriInitial;
    private readonly bool _isFromDefaultSources;

    private OverrideValue<string?> _environmentOverride = new();
    private OverrideValue<string?> _serviceNameOverride = new();
    private OverrideValue<string?> _serviceVersionOverride = new();
    private OverrideValue<bool> _analyticsEnabledOverride = new();
    private OverrideValue<double?> _globalSamplingRateOverride = new();
    private IDictionary<string, string> _globalTagsOverride;
    private IDictionary<string, string> _grpcTagsOverride;
    private IDictionary<string, string> _headerTagsOverride;
    private OverrideValue<bool> _kafkaCreateConsumerScopeEnabledOverride = new();
    private OverrideValue<bool> _logsInjectionEnabledOverride = new();
    private OverrideValue<int> _maxTracesSubmittedPerSecondOverride = new();
    private OverrideValue<string?> _customSamplingRulesOverride = new();
    private OverrideValue<bool> _startupDiagnosticLogEnabledOverride = new();
    private OverrideValue<bool> _traceEnabledOverride = new();
    private HashSet<string> _disabledIntegrationNamesOverride;
    private OverrideValue<bool> _tracerMetricsEnabledOverride = new();
    private OverrideValue<bool> _statsComputationEnabledOverride = new();
    private OverrideValue<Uri> _agentUriOverride = new();
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
        _agentUriInitial = GetValue<Uri?>(initialValues, TracerSettingKeyConstants.AgentUriKey, null) ?? new Uri("http://127.0.0.1:8126");
        _analyticsEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.AnalyticsEnabledKey, false);
        _customSamplingRulesInitial = GetValue<string?>(initialValues, TracerSettingKeyConstants.CustomSamplingRules, null);
        _disabledIntegrationNamesInitial = GetValue<HashSet<string>?>(initialValues, TracerSettingKeyConstants.DisabledIntegrationNamesKey, null) ?? [];
        _diagnosticSourceEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.DiagnosticSourceEnabledKey, false);
        _environmentInitial = GetValue<string?>(initialValues, TracerSettingKeyConstants.EnvironmentKey, null);
        _globalSamplingRateInitial = GetValue<double?>(initialValues, TracerSettingKeyConstants.GlobalSamplingRateKey, null);
        _globalTagsInitial = GetValue<IDictionary<string, string>?>(initialValues, TracerSettingKeyConstants.GlobalTagsKey, null) ?? new ConcurrentDictionary<string, string>();
        _grpcTagsInitial = GetValue<IDictionary<string, string>?>(initialValues, TracerSettingKeyConstants.GrpcTags, null) ?? new ConcurrentDictionary<string, string>();
        _headerTagsInitial = GetValue<IDictionary<string, string>?>(initialValues, TracerSettingKeyConstants.HeaderTags, null) ?? new ConcurrentDictionary<string, string>();
        _kafkaCreateConsumerScopeEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey, true);
        _logsInjectionEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.LogsInjectionEnabledKey, false);
        _maxTracesSubmittedPerSecondInitial = GetValue(initialValues, TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey, 100);
        _serviceNameInitial = GetValue<string?>(initialValues, TracerSettingKeyConstants.ServiceNameKey, null);
        _serviceVersionInitial = GetValue<string?>(initialValues, TracerSettingKeyConstants.ServiceVersionKey, null);
        _startupDiagnosticLogEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey, true);
        _statsComputationEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.StatsComputationEnabledKey, true);
        _traceEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.TraceEnabledKey, true);
        _tracerMetricsEnabledInitial = GetValue(initialValues, TracerSettingKeyConstants.TracerMetricsEnabledKey, false);
        _isFromDefaultSources = isFromDefaultSources;

        // we copy these so we can detect changes later (including replacement)
        _globalTagsOverride = new ConcurrentDictionary<string, string>(_globalTagsInitial);
        _disabledIntegrationNamesOverride = new HashSet<string>(_disabledIntegrationNamesInitial);
        _grpcTagsOverride = new ConcurrentDictionary<string, string>(_grpcTagsInitial);
        _headerTagsOverride = new ConcurrentDictionary<string, string>(_headerTagsInitial);

        // This is just a bunch of indirection to not change the public API for now
#pragma warning disable CS0618 // Type or member is obsolete
        Exporter = new ExporterSettings(this);
#pragma warning restore CS0618 // Type or member is obsolete

        Integrations = IntegrationSettingsHelper.ParseFromAutomatic(initialValues);

        static T GetValue<T>(Dictionary<string, object?> results, string key, T defaultValue)
        {
            if (results.TryGetValue(key, out var value)
                && value is T t)
            {
                return t;
            }

            return defaultValue;
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
        get => _diagnosticSourceEnabledInitial;

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
        get => _environmentOverride.IsOverridden ? _environmentOverride.Value : _environmentInitial;
        set => _environmentOverride = new(value);
    }

    /// <summary>
    /// Gets or sets the service name applied to top-level spans and used to build derived service names.
    /// Can also be set via DD_SERVICE.
    /// </summary>
    public string? ServiceName
    {
        [Instrumented]
        get => _serviceNameOverride.IsOverridden ? _serviceNameOverride.Value : _serviceNameInitial;
        set => _serviceNameOverride = new(value);
    }

    /// <summary>
    /// Gets or sets the version tag applied to all spans.
    /// </summary>
    public string? ServiceVersion
    {
        [Instrumented]
        get => _serviceVersionOverride.IsOverridden ? _serviceVersionOverride.Value : _serviceVersionInitial;
        set => _serviceVersionOverride = new(value);
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
        get => _analyticsEnabledOverride.IsOverridden ? _analyticsEnabledOverride.Value : _analyticsEnabledInitial;
        set => _analyticsEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating a global rate for sampling.
    /// </summary>
    public double? GlobalSamplingRate
    {
        [Instrumented]
        get => _globalSamplingRateOverride.IsOverridden ? _globalSamplingRateOverride.Value : _globalSamplingRateInitial;
        set => _globalSamplingRateOverride = new(value);
    }

    /// <summary>
    /// Gets or sets the global tags, which are applied to all <see cref="ISpan"/>s.
    /// </summary>
    public IDictionary<string, string> GlobalTags
    {
        [Instrumented]
        get => _globalTagsOverride;
        set => _globalTagsOverride = value;
    }

    /// <summary>
    /// Gets or sets the map of metadata keys to tag names, which are applied to the root <see cref="ISpan"/>
    /// of incoming and outgoing GRPC requests.
    /// </summary>
    public IDictionary<string, string> GrpcTags
    {
        [Instrumented]
        get => _grpcTagsOverride;
        set => _grpcTagsOverride = value;
    }

    /// <summary>
    /// Gets or sets the map of header keys to tag names, which are applied to the root <see cref="ISpan"/>
    /// of incoming and outgoing HTTP requests.
    /// </summary>
    public IDictionary<string, string> HeaderTags
    {
        [Instrumented]
        get => _headerTagsOverride;
        set => _headerTagsOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether a span context should be created on exiting a successful Kafka
    /// Consumer.Consume() call, and closed on entering Consumer.Consume().
    /// </summary>
    public bool KafkaCreateConsumerScopeEnabled
    {
        [Instrumented]
        get => _kafkaCreateConsumerScopeEnabledOverride.IsOverridden ? _kafkaCreateConsumerScopeEnabledOverride.Value : _kafkaCreateConsumerScopeEnabledInitial;
        set => _kafkaCreateConsumerScopeEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether correlation identifiers are
    /// automatically injected into the logging context.
    /// Default is <c>false</c>, unless Direct Log Submission is enabled.
    /// </summary>
    public bool LogsInjectionEnabled
    {
        [Instrumented]
        get => _logsInjectionEnabledOverride.IsOverridden ? _logsInjectionEnabledOverride.Value : _logsInjectionEnabledInitial;
        set => _logsInjectionEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
    /// Default is <c>100</c>.
    /// </summary>
    public int MaxTracesSubmittedPerSecond
    {
        [Instrumented]
        get => _maxTracesSubmittedPerSecondOverride.IsOverridden ? _maxTracesSubmittedPerSecondOverride.Value : _maxTracesSubmittedPerSecondInitial;
        set => _maxTracesSubmittedPerSecondOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating custom sampling rules.
    /// </summary>
    public string? CustomSamplingRules
    {
        [Instrumented]
        get => _customSamplingRulesOverride.IsOverridden ? _customSamplingRulesOverride.Value : _customSamplingRulesInitial;
        set => _customSamplingRulesOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the diagnostic log at startup is enabled
    /// </summary>
    public bool StartupDiagnosticLogEnabled
    {
        [Instrumented]
        get => _startupDiagnosticLogEnabledOverride.IsOverridden ? _startupDiagnosticLogEnabledOverride.Value : _startupDiagnosticLogEnabledInitial;
        set => _startupDiagnosticLogEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    public bool TraceEnabled
    {
        [Instrumented]
        get => _traceEnabledOverride.IsOverridden ? _traceEnabledOverride.Value : _traceEnabledInitial;
        set => _traceEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets the names of disabled integrations.
    /// </summary>
    public HashSet<string> DisabledIntegrationNames
    {
        [Instrumented]
        get => _disabledIntegrationNamesOverride;
        set => _disabledIntegrationNamesOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether internal metrics
    /// are enabled and sent to DogStatsd.
    /// </summary>
    public bool TracerMetricsEnabled
    {
        [Instrumented]
        get => _tracerMetricsEnabledOverride.IsOverridden ? _tracerMetricsEnabledOverride.Value : _tracerMetricsEnabledInitial;
        set => _tracerMetricsEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether stats are computed on the tracer side
    /// </summary>
    public bool StatsComputationEnabled
    {
        [Instrumented]
        get => _statsComputationEnabledOverride.IsOverridden ? _statsComputationEnabledOverride.Value : _statsComputationEnabledInitial;
        set => _statsComputationEnabledOverride = new(value);
    }

    /// <summary>
    /// Gets or sets the Uri where the Tracer can connect to the Agent.
    /// Default is <c>"http://localhost:8126"</c>.
    /// </summary>
    public Uri AgentUri
    {
        [Instrumented]
        get => _agentUriOverride.IsOverridden ? _agentUriOverride.Value : _agentUriInitial;
        set => _agentUriOverride = new(value);
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

        AddIfChanged(results, TracerSettingKeyConstants.AgentUriKey, _agentUriOverride);
        AddIfChanged(results, TracerSettingKeyConstants.AnalyticsEnabledKey, _analyticsEnabledOverride);
        AddIfChanged(results, TracerSettingKeyConstants.EnvironmentKey, _environmentOverride);
        AddIfChanged(results, TracerSettingKeyConstants.CustomSamplingRules, _customSamplingRulesOverride);
        AddIfChanged(results, TracerSettingKeyConstants.GlobalSamplingRateKey, _globalSamplingRateOverride);
        AddIfChanged(results, TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey, _kafkaCreateConsumerScopeEnabledOverride);
        AddIfChanged(results, TracerSettingKeyConstants.LogsInjectionEnabledKey, _logsInjectionEnabledOverride);
        AddIfChanged(results, TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey, _maxTracesSubmittedPerSecondOverride);
        AddIfChanged(results, TracerSettingKeyConstants.ServiceNameKey, _serviceNameOverride);
        AddIfChanged(results, TracerSettingKeyConstants.ServiceVersionKey, _serviceVersionOverride);
        AddIfChanged(results, TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey, _startupDiagnosticLogEnabledOverride);
        AddIfChanged(results, TracerSettingKeyConstants.StatsComputationEnabledKey, _statsComputationEnabledOverride);
        AddIfChanged(results, TracerSettingKeyConstants.TraceEnabledKey, _traceEnabledOverride);
        AddIfChanged(results, TracerSettingKeyConstants.TracerMetricsEnabledKey, _tracerMetricsEnabledOverride);

        // We have to check if any of the tags have changed
        AddChangedDictionary(results, TracerSettingKeyConstants.GlobalTagsKey, _globalTagsOverride, _globalTagsInitial);
        AddChangedDictionary(results, TracerSettingKeyConstants.GrpcTags, _grpcTagsOverride, _grpcTagsInitial);
        AddChangedDictionary(results, TracerSettingKeyConstants.HeaderTags, _headerTagsOverride, _headerTagsInitial);

        if (_disabledIntegrationNamesInitial.Count != _disabledIntegrationNamesOverride.Count
         || !_disabledIntegrationNamesOverride.SetEquals(_disabledIntegrationNamesInitial))
        {
            results.Add(TracerSettingKeyConstants.DisabledIntegrationNamesKey, _disabledIntegrationNamesOverride);
        }

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
        results[TracerSettingKeyConstants.IntegrationSettingsKey] = BuildIntegrationSettings(Integrations);

        return results;

        static void AddIfChanged<T>(Dictionary<string, object?> results, string key, OverrideValue<T> updated)
        {
            if (updated.IsOverridden)
            {
                results.Add(key, updated.Value);
            }
        }

        static void AddChangedDictionary(Dictionary<string, object?> results, string key, IDictionary<string, string> updated, IDictionary<string, string> initial)
        {
            if (HasChanges(initial, updated))
            {
                results[key] = updated;
            }

            return;

            static bool HasChanges(IDictionary<string, string> initial, IDictionary<string, string> updated)
            {
                // Currently need to account for customers _replacing_ the Global Tags as well as changing it
                // we create the updated one as a concurrent dictionary, so if they change it
                if (initial.Count != updated.Count || updated is not ConcurrentDictionary<string, string>)
                {
                    return true;
                }

                var comparer = StringComparer.Ordinal;
                foreach (var kvp in initial)
                {
                    if (!updated.TryGetValue(kvp.Key, out var value2)
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

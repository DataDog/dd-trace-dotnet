// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Concurrent;

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

    private string? _environmentOverride;
    private string? _serviceNameOverride;
    private string? _serviceVersionOverride;
    private bool? _analyticsEnabledOverride;
    private double? _globalSamplingRateOverride;
    private IDictionary<string, string>? _globalTagsOverride;
    private bool? _kafkaCreateConsumerScopeEnabledOverride;
    private bool? _logsInjectionEnabledOverride;
    private bool? _startupDiagnosticLogEnabledOverride;
    private bool? _traceEnabledOverride;
    private bool? _tracerMetricsEnabledOverride;
    private Uri? _agentUriOverride;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
    /// </summary>
    [Instrumented]
    public TracerSettings()
    {
        // TODO: _Currently_ this doesn't load _any_ configuration, which feels like an error for customers to use?
        // Instead, I'm thinking that we should populate from the default sources instead, as otherwise seems
        // like an obvious point of confusion?

        // TODO: We need to set the values here based on the current auto-config values
        // to ensure the setters get the correct values
        // The values set here represent the defaults when there's no auto-instrumentation
        _diagnosticSourceEnabledInitial = false;
        _analyticsEnabledInitial = false;
        _environmentInitial = null;
        _serviceNameInitial = null;
        _serviceVersionInitial = null;
        _globalSamplingRateInitial = null;
        _globalTagsInitial = new ConcurrentDictionary<string, string>();
        _kafkaCreateConsumerScopeEnabledInitial = true;
        _logsInjectionEnabledInitial = false;
        _startupDiagnosticLogEnabledInitial = true;
        _traceEnabledInitial = true;
        _tracerMetricsEnabledInitial = false;
        _agentUriInitial = new Uri("http://localhost:8126");

#pragma warning disable CS0618 // Type or member is obsolete
        Exporter = new ExporterSettings(this);
#pragma warning restore CS0618 // Type or member is obsolete
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
    internal string? Environment
    {
        get => _environmentOverride ?? _environmentInitial;
        set => _environmentOverride = value;
    }

    /// <summary>
    /// Gets or sets the service name applied to top-level spans and used to build derived service names.
    /// Can also be set via DD_SERVICE.
    /// </summary>
    internal string? ServiceName
    {
        get => _serviceNameOverride ?? _serviceNameInitial;
        set => _serviceNameOverride = value;
    }

    /// <summary>
    /// Gets or sets the version tag applied to all spans.
    /// </summary>
    internal string? ServiceVersion
    {
        get => _serviceVersionOverride ?? _serviceVersionInitial;
        set => _serviceVersionOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether default Analytics are enabled.
    /// Settings this value is a shortcut for setting
    /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
    /// See the documentation for more details.
    /// </summary>
    [Obsolete(DeprecationMessages.AppAnalytics)]
    internal bool AnalyticsEnabled
    {
        get => _analyticsEnabledOverride ?? _analyticsEnabledInitial;
        set => _analyticsEnabledOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating a global rate for sampling.
    /// </summary>
    internal double? GlobalSamplingRate
    {
        get => _globalSamplingRateOverride ?? _globalSamplingRateInitial;
        set => _globalSamplingRateOverride = value;
    }

    /// <summary>
    /// Gets or sets the global tags, which are applied to all <see cref="ISpan"/>s.
    /// </summary>
    internal IDictionary<string, string> GlobalTags
    {
        get => _globalTagsOverride ?? _globalTagsInitial;
        set => _globalTagsOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether a span context should be created on exiting a successful Kafka
    /// Consumer.Consume() call, and closed on entering Consumer.Consume().
    /// </summary>
    internal bool KafkaCreateConsumerScopeEnabled
    {
        get => _kafkaCreateConsumerScopeEnabledOverride ?? _kafkaCreateConsumerScopeEnabledInitial;
        set => _kafkaCreateConsumerScopeEnabledOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether correlation identifiers are
    /// automatically injected into the logging context.
    /// Default is <c>false</c>, unless Direct Log Submission is enabled.
    /// </summary>
    public bool LogsInjectionEnabled
    {
        get => _logsInjectionEnabledOverride ?? _logsInjectionEnabledInitial;
        set => _logsInjectionEnabledOverride = value;
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
    // TODO: redirect to ImmutableTracerSettings.StartupDiagnosticLogEnabled?
    public bool StartupDiagnosticLogEnabled
    {
        get => _startupDiagnosticLogEnabledOverride ?? _startupDiagnosticLogEnabledInitial;
        [Obsolete("This value cannot be set in code. Instead, set it using the DD_TRACE_STARTUP_LOGS environment variable, or in configuration files")]
        set => _startupDiagnosticLogEnabledOverride = value;
    }

    /// <summary>
    /// Gets or sets a value indicating whether tracing is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    public bool TraceEnabled
    {
        get => _traceEnabledOverride ?? _traceEnabledInitial;
        set => _traceEnabledOverride = value;
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
        get => _tracerMetricsEnabledOverride ?? _tracerMetricsEnabledInitial;
        set => _tracerMetricsEnabledOverride = value;
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
        get => _agentUriOverride ?? _agentUriInitial;
        set => _agentUriOverride = value;
    }

    /// <summary>
    /// Gets a collection of <see cref="IntegrationSettings"/> keyed by integration name.
    /// </summary>
    internal IntegrationSettingsCollection IntegrationsInternal { get; } = new();

    /// <summary>
    /// Gets the transport settings that dictate how the tracer connects to the agent.
    /// </summary>
    [Obsolete("This property is obsolete and will be removed in a future version. To set the AgentUri, use the TracerSettings.AgentUri property")]
    internal ExporterSettings Exporter { get; }

    /// <summary>
    /// Create a <see cref="TracerSettings"/> populated from the default sources.
    /// </summary>
    /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
    public static TracerSettings FromDefaultSources()
    {
        // TODO: this assumes that we change the behaviour of new TracerSettings() to populate with default sources
        return new TracerSettings();
    }

    /// TODO: Can we delete this? There's no API for creating a tracer like this
    /// <summary>
    /// Create an instance of <see cref="ImmutableTracerSettings"/> that can be used to build a <see cref="Tracer"/>
    /// </summary>
    /// <returns>The <see cref="ImmutableTracerSettings"/> that can be passed to a <see cref="Tracer"/> instance</returns>
    public ImmutableTracerSettings Build() => new();

    internal Dictionary<string, object> ToDictionary()
    {
        // only record the overrides
        var results = new Dictionary<string, object>();
        AddIfNotNull(results, "DD_ENV", _environmentOverride);
        AddIfNotNull(results, "DD_SERVICE", _serviceNameOverride);
        AddIfNotNull(results, "DD_VERSION", _serviceVersionOverride);
        AddIfNotNull(results, "DD_TRACE_ANALYTICS_ENABLED", _analyticsEnabledOverride);
        AddIfNotNull(results, "DD_TRACE_SAMPLE_RATE", _globalSamplingRateOverride);
        AddIfNotNull(results, "DD_TAGS", _globalTagsOverride);
        AddIfNotNull(results, "DD_TRACE_KAFKA_CREATE_CONSUMER_SCOPE_ENABLED", _kafkaCreateConsumerScopeEnabledOverride);
        AddIfNotNull(results, "DD_LOGS_INJECTION", _logsInjectionEnabledOverride);
        AddIfNotNull(results, "DD_TRACE_STARTUP_LOGS", _startupDiagnosticLogEnabledOverride);
        AddIfNotNull(results, "DD_TRACE_ENABLED", _traceEnabledOverride);
        AddIfNotNull(results, "DD_TRACE_METRICS_ENABLED", _tracerMetricsEnabledOverride);
        AddIfNotNull(results, "DD_TRACE_AGENT_URL", _agentUriOverride);

        return results;

        static void AddIfNotNull<T>(Dictionary<string, object> results, string key, T value)
        {
            if (value is not null)
            {
                results.Add(key, value);
            }
        }
    }
}

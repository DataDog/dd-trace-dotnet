// <copyright file="ImmutableTracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Concurrent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Configuration;

/// <summary>
/// Contains Tracer settings.
/// </summary>
public sealed class ImmutableTracerSettings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ImmutableTracerSettings"/> class.
    /// </summary>
    internal ImmutableTracerSettings(Dictionary<string, object?> initialValues)
    {
        AgentUri = GetValue<Uri?>(initialValues, TracerSettingKeyConstants.AgentUriKey, null) ?? new Uri("http://127.0.0.1:8126");
        CustomSamplingRules = GetValue<string?>(initialValues, TracerSettingKeyConstants.CustomSamplingRules, null);
        Environment = GetValue<string?>(initialValues, TracerSettingKeyConstants.EnvironmentKey, null);
        GlobalSamplingRate = GetValue<double?>(initialValues, TracerSettingKeyConstants.GlobalSamplingRateKey, null);
        GlobalTags = GetValue<IReadOnlyDictionary<string, string>?>(initialValues, TracerSettingKeyConstants.GlobalTagsKey, null) ?? new ConcurrentDictionary<string, string>();
        GrpcTags = GetValue<IReadOnlyDictionary<string, string>?>(initialValues, TracerSettingKeyConstants.GrpcTags, null) ?? new ConcurrentDictionary<string, string>();
        HeaderTags = GetValue<IReadOnlyDictionary<string, string>?>(initialValues, TracerSettingKeyConstants.HeaderTags, null) ?? new ConcurrentDictionary<string, string>();
        KafkaCreateConsumerScopeEnabled = GetValue(initialValues, TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey, true);
        LogsInjectionEnabled = GetValue(initialValues, TracerSettingKeyConstants.LogsInjectionEnabledKey, false);
        MaxTracesSubmittedPerSecond = GetValue(initialValues, TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey, 100);
        ServiceName = GetValue<string?>(initialValues, TracerSettingKeyConstants.ServiceNameKey, null);
        ServiceVersion = GetValue<string?>(initialValues, TracerSettingKeyConstants.ServiceVersionKey, null);
        StartupDiagnosticLogEnabled = GetValue(initialValues, TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey, true);
        StatsComputationEnabled = GetValue(initialValues, TracerSettingKeyConstants.StatsComputationEnabledKey, false);
        TraceEnabled = GetValue(initialValues, TracerSettingKeyConstants.TraceEnabledKey, true);
        TracerMetricsEnabled = GetValue(initialValues, TracerSettingKeyConstants.TracerMetricsEnabledKey, false);

#pragma warning disable CS0618 // Type or member is obsolete
        Exporter = new ImmutableExporterSettings(this);
        AnalyticsEnabled = GetValue(initialValues, TracerSettingKeyConstants.AnalyticsEnabledKey, false);
#pragma warning restore CS0618 // Type or member is obsolete

        Integrations = IntegrationSettingsHelper.ParseImmutableFromAutomatic(initialValues);

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
    /// Gets the default environment name applied to all spans.
    /// </summary>
    [Instrumented]
    public string? Environment { get; }

    /// <summary>
    /// Gets the Uri where the Tracer can connect to the Agent.
    /// </summary>
    [Instrumented]
    public Uri AgentUri { get; }

    /// <summary>
    /// Gets the exporter settings that dictate how the tracer exports data.
    /// </summary>
    [Obsolete("This property is obsolete and will be removed in a future version. To get the AgentUri, use the AgentUri property")]
    [Instrumented]
    public ImmutableExporterSettings Exporter { get; }

    /// <summary>
    /// Gets the service name applied to top-level spans and used to build derived service names.
    /// </summary>
    /// <seealso cref="TracerSettings.ServiceName"/>
    [Instrumented]
    public string? ServiceName { get; }

    /// <summary>
    /// Gets the version tag applied to all spans.
    /// </summary>
    /// <seealso cref="TracerSettings.ServiceVersion"/>
    [Instrumented]
    public string? ServiceVersion { get; }

    /// <summary>
    /// Gets a value indicating whether tracing is enabled.
    /// Default is <c>true</c>.
    /// </summary>
    [Instrumented]
    public bool TraceEnabled { get; }

#pragma warning disable CS1574 // AnalyticsEnabled is obsolete
    /// <summary>
    /// Gets a value indicating whether default Analytics are enabled.
    /// Settings this value is a shortcut for setting
    /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
    /// See the documentation for more details.
    /// </summary>
    /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
#pragma warning restore CS1574
    [Obsolete(DeprecationMessages.AppAnalytics)]
    [Instrumented]
    public bool AnalyticsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether correlation identifiers are
    /// automatically injected into the logging context.
    /// Default is <c>false</c>.
    /// </summary>
    [Instrumented]
    public bool LogsInjectionEnabled { get; }

    /// <summary>
    /// Gets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
    /// Default is <c>100</c>.
    /// </summary>
    [Instrumented]
    public int MaxTracesSubmittedPerSecond { get; }

    /// <summary>
    /// Gets a value indicating custom sampling rules.
    /// </summary>
    [Instrumented]
    public string? CustomSamplingRules { get; }

    /// <summary>
    /// Gets a value indicating a global rate for sampling.
    /// </summary>
    [Instrumented]
    public double? GlobalSamplingRate { get; }

    /// <summary>
    /// Gets a collection of <see cref="ImmutableIntegrationSettings"/> keyed by integration name.
    /// </summary>
    [Instrumented]
    public ImmutableIntegrationSettingsCollection Integrations { get; }

    /// <summary>
    /// Gets the global tags, which are applied to all <see cref="ISpan"/>s.
    /// </summary>
    [Instrumented]
    public IReadOnlyDictionary<string, string> GlobalTags { get; }

    /// <summary>
    /// Gets the map of metadata keys to tag names, which are applied to the root <see cref="ISpan"/>
    /// of incoming and outgoing GRPC requests.
    /// </summary>
    [Instrumented]
    public IReadOnlyDictionary<string, string> GrpcTags { get; }

    /// <summary>
    /// Gets the map of header keys to tag names, which are applied to the root <see cref="ISpan"/>
    /// of incoming and outgoing requests.
    /// </summary>
    [Instrumented]
    public IReadOnlyDictionary<string, string> HeaderTags { get; }

    /// <summary>
    /// Gets a value indicating whether internal metrics
    /// are enabled and sent to DogStatsd.
    /// </summary>
    [Instrumented]
    public bool TracerMetricsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether stats are computed on the tracer side
    /// </summary>
    [Instrumented]
    public bool StatsComputationEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether a span context should be created on exiting a successful Kafka
    /// Consumer.Consume() call, and closed on entering Consumer.Consume().
    /// </summary>
    [Instrumented]
    public bool KafkaCreateConsumerScopeEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether the diagnostic log at startup is enabled
    /// </summary>
    [Instrumented]
    public bool StartupDiagnosticLogEnabled { get; }
}

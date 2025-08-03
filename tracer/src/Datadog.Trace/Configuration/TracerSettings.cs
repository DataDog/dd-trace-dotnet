// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Processors;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Tagging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public record TracerSettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerSettings>();
        private static readonly HashSet<string> DefaultExperimentalFeatures = new HashSet<string>()
        {
            "DD_TAGS"
        };

        private readonly IConfigurationTelemetry _telemetry;
        // we cached the static instance here, because is being used in the hotpath
        // by IsIntegrationEnabled method (called from all integrations)
        private readonly DomainMetadata _domainMetadata = DomainMetadata.Instance;
        // These values can all be overwritten by dynamic config
        private readonly bool _traceEnabled;
        private readonly bool _apmTracingEnabled;
        private readonly bool _isDataStreamsMonitoringEnabled;
        private readonly bool _isDataStreamsMonitoringInDefaultState;
        private readonly ReadOnlyDictionary<string, string> _headerTags;
        private readonly ReadOnlyDictionary<string, string> _serviceNameMappings;
        private readonly ReadOnlyDictionary<string, string> _globalTags;
        private readonly double? _globalSamplingRate;
        private readonly bool _runtimeMetricsEnabled;
        private readonly string? _customSamplingRules;

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
        /// </summary>
        [PublicApi]
        public TracerSettings()
            : this(null, new ConfigurationTelemetry(), new OverrideErrorLog())
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Ctor);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <remarks>
        /// We deliberately don't use the static <see cref="TelemetryFactory.Config"/> collector here
        /// as we don't want to automatically record these values, only once they're "activated",
        /// in <see cref="Tracer.Configure(TracerSettings)"/>
        /// </remarks>
        [PublicApi]
        public TracerSettings(IConfigurationSource? source)
        : this(source, new ConfigurationTelemetry(), new OverrideErrorLog())
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Ctor_Source);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class.
        /// The "main" constructor for <see cref="TracerSettings"/> that should be used internally in the library.
        /// </summary>
        /// <param name="source">The configuration source. If <c>null</c> is provided, uses <see cref="NullConfigurationSource"/> </param>
        /// <param name="telemetry">The telemetry collection instance. Typically you should create a new <see cref="ConfigurationTelemetry"/> </param>
        /// <param name="errorLog">Used to record cases where telemetry is overridden </param>
        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, OverrideErrorLog errorLog)
            : this(source, telemetry, errorLog, LibDatadog.NativeInterop.IsLibDatadogAvailable)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class.
        /// The "main" constructor for <see cref="TracerSettings"/> that should be used internally in the library.
        /// </summary>
        /// <param name="source">The configuration source. If <c>null</c> is provided, uses <see cref="NullConfigurationSource"/> </param>
        /// <param name="telemetry">The telemetry collection instance. Typically you should create a new <see cref="ConfigurationTelemetry"/> </param>
        /// <param name="errorLog">Used to record cases where telemetry is overridden </param>
        /// <param name="isLibDatadogAvailable">Used to check whether the libdatadog library is available. Useful for integration tests</param>
        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, OverrideErrorLog errorLog, bool isLibDatadogAvailable)
        {
            var commaSeparator = new[] { ',' };
            source ??= NullConfigurationSource.Instance;
            _telemetry = telemetry;
            ErrorLog = errorLog;
            var config = new ConfigurationBuilder(source, _telemetry);

            ExperimentalFeaturesEnabled = config
                    .WithKeys(ConfigurationKeys.ExperimentalFeaturesEnabled)
                    .AsString()?.Trim() switch
                    {
                        null or "none" => new HashSet<string>(),
                        "all" => DefaultExperimentalFeatures,
                        string s => new HashSet<string>(s.Split([','], StringSplitOptions.RemoveEmptyEntries)),
                    };

            GCPFunctionSettings = new ImmutableGCPFunctionSettings(source, _telemetry);
            IsRunningInGCPFunctions = GCPFunctionSettings.IsGCPFunction;

            // We don't want/need to record this value, so explicitly use null telemetry
            var isRunningInCiVisibility = new ConfigurationBuilder(source, NullConfigurationTelemetry.Instance)
                                         .WithKeys(ConfigurationKeys.CIVisibility.IsRunningInCiVisMode)
                                         .AsBool(false);

            LambdaMetadata = LambdaMetadata.Create();

            if (ImmutableAzureAppServiceSettings.IsRunningInAzureAppServices(source, telemetry))
            {
                AzureAppServiceMetadata = new ImmutableAzureAppServiceSettings(source, _telemetry);
            }

            // With SSI, beyond ContinuousProfiler.ConfigurationKeys.ProfilingEnabled (true or auto vs false),
            // the profiler could be enabled via ContinuousProfiler.ConfigurationKeys.SsiDeployed:
            //  - if it contains "profiler", the profiler is enabled after 30 seconds + at least 1 span
            //  - if not, the profiler needed to be loaded by the CLR but no profiling will be done, only telemetry metrics will be sent
            // So, for the Tracer, the profiler should be seen as enabled if ContinuousProfiler.ConfigurationKeys.SsiDeployed has a value
            // (even without "profiler") so that spans will be sent to the profiler.
            ProfilingEnabledInternal = config
                         .WithKeys(ContinuousProfiler.ConfigurationKeys.ProfilingEnabled)
                         .GetAs(
                            converter: x => x switch
                            {
                                "auto" => true,
                                _ when x.ToBoolean() is { } boolean => boolean,
                                _ => ParsingResult<bool>.Failure(),
                            },
                            getDefaultValue: () =>
                            {
                                var profilingSsiDeployed = config.WithKeys(ContinuousProfiler.ConfigurationKeys.SsiDeployed).AsString();
                                return (profilingSsiDeployed != null);
                            },
                            validator: null);

            var otelTags = config
                          .WithKeys(ConfigurationKeys.OpenTelemetry.ResourceAttributes)
                          .AsDictionaryResult(separator: '=');

            Dictionary<string, string>? globalTags = default;
            if (ExperimentalFeaturesEnabled.Contains("DD_TAGS"))
            {
                // New behavior: If ExperimentalFeaturesEnabled configures DD_TAGS, we want to change DD_TAGS parsing to do the following:
                // 1. If a comma is in the value, split on comma as before. Otherwise, split on space
                // 2. Key-value pairs with empty values are allowed, instead of discarded
                // 3. Key-value pairs without values (i.e. no `:` separator) are allowed and treated as key-value pairs with empty values, instead of discarded
                Func<string, IDictionary<string, string>> updatedTagsParser = (data) =>
                {
                    var dictionary = new ConcurrentDictionary<string, string>();
                    if (string.IsNullOrWhiteSpace(data))
                    {
                        // return empty collection
                        return dictionary;
                    }

                    char[] separatorChars = data.Contains(',') ? [','] : [' '];
                    var entries = data.Split(separatorChars, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var entry in entries)
                    {
                        // we need Trim() before looking forthe separator so we can skip entries with no key
                        // (that is, entries with a leading separator, like "<empty or whitespace>:value")
                        var trimmedEntry = entry.Trim();
                        if (trimmedEntry.Length == 0 || trimmedEntry[0] == ':')
                        {
                            continue;
                        }

                        var separatorIndex = trimmedEntry.IndexOf(':');
                        if (separatorIndex < 0)
                        {
                            // entries with no separator are allowed (e.g. key1 and key3 in "key1, key2:value2, key3"),
                            // it's a key with no value.
                            var key = trimmedEntry;
                            dictionary[key] = string.Empty;
                        }
                        else if (separatorIndex > 0)
                        {
                            // if a separator is present with no value, we take the value to be empty (e.g. "key1:, key2: ").
                            // note we already did Trim() on the entire entry, so the key portion only needs TrimEnd().
                            var key = trimmedEntry.Substring(0, separatorIndex).TrimEnd();
                            var value = trimmedEntry.Substring(separatorIndex + 1).Trim();
                            dictionary[key] = value;
                        }
                    }

                    return dictionary;
                };

                globalTags = config
                                .WithKeys(ConfigurationKeys.GlobalTags, "DD_TRACE_GLOBAL_TAGS")
                                .AsDictionaryResult(parser: updatedTagsParser)
                                .OverrideWith(
                                     RemapOtelTags(in otelTags),
                                     ErrorLog,
                                     () => new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), string.Empty))

                                // Filter out tags with empty keys, and trim whitespace
                                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                                .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value?.Trim() ?? string.Empty);
            }
            else
            {
                globalTags = config
                                .WithKeys(ConfigurationKeys.GlobalTags, "DD_TRACE_GLOBAL_TAGS")
                                .AsDictionaryResult()
                                .OverrideWith(
                                     RemapOtelTags(in otelTags),
                                     ErrorLog,
                                     () => new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), string.Empty))

                                // Filter out tags with empty keys or empty values, and trim whitespace
                                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());
            }

            Environment = config
                         .WithKeys(ConfigurationKeys.Environment)
                         .AsString();

            // DD_ENV has precedence over DD_TAGS
            Environment = GetExplicitSettingOrTag(Environment, globalTags!, Tags.Env, ConfigurationKeys.Environment);

            var otelServiceName = config.WithKeys(ConfigurationKeys.OpenTelemetry.ServiceName).AsStringResult();
            var serviceName = config
                                 .WithKeys(ConfigurationKeys.ServiceName, "DD_SERVICE_NAME")
                                 .AsStringResult()
                                 .OverrideWith(in otelServiceName, ErrorLog);

            // DD_SERVICE has precedence over DD_TAGS
            serviceName = GetExplicitSettingOrTag(serviceName, globalTags, Tags.Service, ConfigurationKeys.ServiceName);

            if (isRunningInCiVisibility)
            {
                // Set the service name if not set
                var isUserProvidedTestServiceTag = true;
                var ciVisServiceName = serviceName;
                if (string.IsNullOrEmpty(serviceName))
                {
                    // Extract repository name from the git url and use it as a default service name.
                    ciVisServiceName = TestOptimization.Instance.TracerManagement?.GetServiceNameFromRepository(CIEnvironmentValues.Instance.Repository);
                    isUserProvidedTestServiceTag = false;
                }

                globalTags[Ci.Tags.CommonTags.UserProvidedTestServiceTag] = isUserProvidedTestServiceTag ? "true" : "false";

                // Normalize the service name
                ciVisServiceName = NormalizerTraceProcessor.NormalizeService(ciVisServiceName);
                if (ciVisServiceName != serviceName)
                {
                    serviceName = ciVisServiceName;
                    telemetry.Record(ConfigurationKeys.ServiceName, serviceName, recordValue: true, ConfigurationOrigins.Calculated);
                }
            }

            ServiceName = serviceName;

            ServiceVersion = config
                            .WithKeys(ConfigurationKeys.ServiceVersion)
                            .AsString();

            // DD_VERSION has precedence over DD_TAGS
            ServiceVersion = GetExplicitSettingOrTag(ServiceVersion, globalTags, Tags.Version, ConfigurationKeys.ServiceVersion);

            GitCommitSha = config
                          .WithKeys(ConfigurationKeys.GitCommitSha)
                          .AsString();

            // DD_GIT_COMMIT_SHA has precedence over DD_TAGS
            GitCommitSha = GetExplicitSettingOrTag(GitCommitSha, globalTags, Ci.Tags.CommonTags.GitCommit, ConfigurationKeys.GitCommitSha);

            GitRepositoryUrl = config
                              .WithKeys(ConfigurationKeys.GitRepositoryUrl)
                              .AsString();

            // DD_GIT_REPOSITORY_URL has precedence over DD_TAGS
            GitRepositoryUrl = GetExplicitSettingOrTag(GitRepositoryUrl, globalTags, Ci.Tags.CommonTags.GitRepository, ConfigurationKeys.GitRepositoryUrl);

            GitMetadataEnabled = config
                                .WithKeys(ConfigurationKeys.GitMetadataEnabled)
                                .AsBool(defaultValue: true);

            var otelTraceEnabled = config
                                  .WithKeys(ConfigurationKeys.OpenTelemetry.TracesExporter)
                                  .AsBoolResult(
                                       value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                                                    ? ParsingResult<bool>.Success(result: false)
                                                    : ParsingResult<bool>.Failure());
            _traceEnabled = config
                                  .WithKeys(ConfigurationKeys.TraceEnabled)
                                  .AsBoolResult()
                                  .OverrideWith(in otelTraceEnabled, ErrorLog, defaultValue: true);

            _apmTracingEnabled = config
                                      .WithKeys(ConfigurationKeys.ApmTracingEnabled)
                                      .AsBool(defaultValue: true);

            var ciVisibilityEnabled = config
                                     .WithKeys(ConfigurationKeys.CIVisibility.Enabled)
                                     .AsBool(defaultValue: false);

            // If CI Visibility is enabled then we should enable tracing.
            _traceEnabled = _traceEnabled || ciVisibilityEnabled;

            if (AzureAppServiceMetadata?.IsUnsafeToTrace == true)
            {
                telemetry.Record(ConfigurationKeys.TraceEnabled, false, ConfigurationOrigins.Calculated);
                _traceEnabled = false;
            }

            var otelActivityListenerEnabled = config
                                             .WithKeys(ConfigurationKeys.OpenTelemetry.SdkDisabled)
                                             .AsBoolResult(
                                                  value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                                                               ? ParsingResult<bool>.Success(result: false)
                                                               : ParsingResult<bool>.Failure());
            IsActivityListenerEnabled = config
                                       .WithKeys(ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, "DD_TRACE_ACTIVITY_LISTENER_ENABLED")
                                       .AsBoolResult()
                                       .OverrideWith(in otelActivityListenerEnabled, ErrorLog, defaultValue: false);

            var disabledIntegrationNames = config.WithKeys(ConfigurationKeys.DisabledIntegrations)
                                                 .AsString()
                                                ?.Split([';'], StringSplitOptions.RemoveEmptyEntries) ?? [];

            // If Activity support is enabled, we shouldn't enable the OTel listener
            DisabledIntegrationNames = IsActivityListenerEnabled
                                           ? new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase)
                                           : new HashSet<string>([..disabledIntegrationNames, nameof(IntegrationId.OpenTelemetry)], StringComparer.OrdinalIgnoreCase);

            Integrations = new IntegrationSettingsCollection(source, DisabledIntegrationNames);
            RecordDisabledIntegrationsTelemetry(Integrations, Telemetry);

            Exporter = new ExporterSettings(source, _telemetry);

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = config.WithKeys(ConfigurationKeys.GlobalAnalyticsEnabled)
                                                   .AsBool(defaultValue: false);
#pragma warning restore 618

#pragma warning disable 618 // this parameter has been replaced but may still be used
            MaxTracesSubmittedPerSecond = config
                                         .WithKeys(ConfigurationKeys.TraceRateLimit, ConfigurationKeys.MaxTracesSubmittedPerSecond)
#pragma warning restore 618
                                         .AsInt32(defaultValue: 100);

            // mutate dictionary to remove without "env", "version", "git.commit.sha" or "git.repository.url" tags
            // these value are used for "Environment" and "ServiceVersion", "GitCommitSha" and "GitRepositoryUrl" properties
            // or overriden with DD_ENV, DD_VERSION, DD_GIT_COMMIT_SHA and DD_GIT_REPOSITORY_URL respectively
            globalTags.Remove(Tags.Service);
            globalTags.Remove(Tags.Env);
            globalTags.Remove(Tags.Version);
            globalTags.Remove(Ci.Tags.CommonTags.GitCommit);
            globalTags.Remove(Ci.Tags.CommonTags.GitRepository);
            _globalTags = new(globalTags);

            var headerTagsNormalizationFixEnabled = config
                                               .WithKeys(ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled)
                                               .AsBool(defaultValue: true);

            // Filter out tags with empty keys or empty values, and trim whitespaces
            _headerTags = InitializeHeaderTags(config, ConfigurationKeys.HeaderTags, headerTagsNormalizationFixEnabled) ?? ReadOnlyDictionary.Empty;

            PeerServiceTagsEnabled = config
               .WithKeys(ConfigurationKeys.PeerServiceDefaultsEnabled)
               .AsBool(defaultValue: false);
            RemoveClientServiceNamesEnabled = config
               .WithKeys(ConfigurationKeys.RemoveClientServiceNamesEnabled)
               .AsBool(defaultValue: false);
            SpanPointersEnabled = config
               .WithKeys(ConfigurationKeys.SpanPointersEnabled)
               .AsBool(defaultValue: true);

            PeerServiceNameMappings = InitializeServiceNameMappings(config, ConfigurationKeys.PeerServiceNameMappings);

            MetadataSchemaVersion = config
                                   .WithKeys(ConfigurationKeys.MetadataSchemaVersion)
                                   .GetAs(
                                        () => new DefaultResult<SchemaVersion>(SchemaVersion.V0, "V0"),
                                        converter: x => x switch
                                        {
                                            "v1" or "V1" => SchemaVersion.V1,
                                            "v0" or "V0" => SchemaVersion.V0,
                                            _ => ParsingResult<SchemaVersion>.Failure(),
                                        },
                                        validator: null);

            _serviceNameMappings = InitializeServiceNameMappings(config, ConfigurationKeys.ServiceNameMappings) ?? ReadOnlyDictionary.Empty;

            TracerMetricsEnabled = config
                                  .WithKeys(ConfigurationKeys.TracerMetricsEnabled)
                                  .AsBool(defaultValue: false);

            StatsComputationInterval = config.WithKeys(ConfigurationKeys.StatsComputationInterval).AsInt32(defaultValue: 10);

            var otelRuntimeMetricsEnabled = config
                                          .WithKeys(ConfigurationKeys.OpenTelemetry.MetricsExporter)
                                          .AsBoolResult(
                                               value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                                                            ? ParsingResult<bool>.Success(result: false)
                                                            : ParsingResult<bool>.Failure());
            _runtimeMetricsEnabled = config
                                   .WithKeys(ConfigurationKeys.RuntimeMetricsEnabled)
                                   .AsBoolResult()
                                   .OverrideWith(in otelRuntimeMetricsEnabled, ErrorLog, defaultValue: false);

            DataPipelineEnabled = config
                                  .WithKeys(ConfigurationKeys.TraceDataPipelineEnabled)
                                  .AsBool(defaultValue: false);

            if (DataPipelineEnabled)
            {
                // Due to missing quantization and obfuscation in native side, we can't enable the native trace exporter
                // as it may lead to different stats results than the managed one.
                if (StatsComputationEnabled)
                {
                    DataPipelineEnabled = false;
                    Log.Warning(
                        $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but {ConfigurationKeys.StatsComputationEnabled} is enabled. Disabling data pipeline.");
                    _telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }

                // Windows supports UnixDomainSocket https://devblogs.microsoft.com/commandline/af_unix-comes-to-windows/
                // but tokio hasn't added support for it yet https://github.com/tokio-rs/tokio/issues/2201
                if (Exporter.TracesTransport == TracesTransportType.UnixDomainSocket && FrameworkDescription.Instance.IsWindows())
                {
                    DataPipelineEnabled = false;
                    Log.Warning(
                        $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but TracesTransport is set to UnixDomainSocket which is not supported on Windows. Disabling data pipeline.");
                    _telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }

                if (!isLibDatadogAvailable)
                {
                    DataPipelineEnabled = false;
                    Log.Warning(
                        $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but libdatadog is not available. Disabling data pipeline.");
                    _telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }
            }

            // We should also be writing telemetry for OTEL_LOGS_EXPORTER similar to OTEL_METRICS_EXPORTER, but we don't have a corresponding Datadog config
            // When we do, we can insert that here

            _customSamplingRules = config.WithKeys(ConfigurationKeys.CustomSamplingRules).AsString();

            CustomSamplingRulesFormat = config.WithKeys(ConfigurationKeys.CustomSamplingRulesFormat)
                                              .GetAs(
                                                   getDefaultValue: () => new DefaultResult<string>(SamplingRulesFormat.Glob, "glob"),
                                                   converter: value =>
                                                   {
                                                       // We intentionally report invalid values as "valid" in the converter,
                                                       // because we don't want to automatically fall back to the
                                                       // default value.
                                                       if (!SamplingRulesFormat.IsValid(value, out var normalizedFormat))
                                                       {
                                                           Log.Warning(
                                                               "{ConfigurationKey} configuration of {ConfigurationValue} is invalid. Ignoring all trace sampling rules.",
                                                               ConfigurationKeys.CustomSamplingRulesFormat,
                                                               value);
                                                       }

                                                       return normalizedFormat;
                                                   },
                                                   validator: null);

            // record final value of CustomSamplingRulesFormat in telemetry
            _telemetry.Record(
                    key: ConfigurationKeys.CustomSamplingRulesFormat,
                    value: CustomSamplingRulesFormat,
                    recordValue: true,
                    origin: ConfigurationOrigins.Calculated);

            SpanSamplingRules = config.WithKeys(ConfigurationKeys.SpanSamplingRules).AsString();

            _globalSamplingRate = BuildSampleRate(ErrorLog, in config);

            // We need to record a default value for configuration reporting
            // However, we need to keep GlobalSamplingRateInternal null because it changes the behavior of the tracer in subtle ways
            // (= we don't run the sampler at all if it's null, so it changes the tagging of the spans, and it's enforced by system tests)
            if (GlobalSamplingRate is null)
            {
                _telemetry.Record(ConfigurationKeys.GlobalSamplingRate, 1.0, ConfigurationOrigins.Default);
            }

            StartupDiagnosticLogEnabled = config.WithKeys(ConfigurationKeys.StartupDiagnosticLogEnabled).AsBool(defaultValue: true);

            var httpServerErrorStatusCodes = config
#pragma warning disable 618 // This config key has been replaced but may still be used
                                            .WithKeys(ConfigurationKeys.HttpServerErrorStatusCodes, ConfigurationKeys.DeprecatedHttpServerErrorStatusCodes)
#pragma warning restore 618
                                            .AsString(defaultValue: "500-599");

            HttpServerErrorStatusCodes = ParseHttpCodesToArray(httpServerErrorStatusCodes);

            var httpClientErrorStatusCodes = config
#pragma warning disable 618 // This config key has been replaced but may still be used
                                            .WithKeys(ConfigurationKeys.HttpClientErrorStatusCodes, ConfigurationKeys.DeprecatedHttpClientErrorStatusCodes)
#pragma warning restore 618
                                            .AsString(defaultValue: "400-499");

            HttpClientErrorStatusCodes = ParseHttpCodesToArray(httpClientErrorStatusCodes);

            TraceBufferSize = config
                             .WithKeys(ConfigurationKeys.BufferSize)
                             .AsInt32(defaultValue: 1024 * 1024 * 10); // 10MB

            // If Lambda/GCP we don't want to have a flush interval. Some serverless integrations
            // manually calls flush and waits for the result before ending execution.
            // This can artificially increase the execution time of functions.
            var defaultTraceBatchInterval = LambdaMetadata.IsRunningInLambda || IsRunningInGCPFunctions || IsRunningInAzureFunctions ? 0 : 100;
            TraceBatchInterval = config
                                .WithKeys(ConfigurationKeys.SerializationBatchInterval)
                                .AsInt32(defaultTraceBatchInterval);

            RouteTemplateResourceNamesEnabled = config
                                               .WithKeys(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled)
                                               .AsBool(defaultValue: true);

            ExpandRouteTemplatesEnabled = config
                                         .WithKeys(ConfigurationKeys.ExpandRouteTemplatesEnabled)
                                         .AsBool(defaultValue: !RouteTemplateResourceNamesEnabled); // disabled by default if route template resource names enabled

            KafkaCreateConsumerScopeEnabled = config
                                             .WithKeys(ConfigurationKeys.KafkaCreateConsumerScopeEnabled)
                                             .AsBool(defaultValue: true);

            DelayWcfInstrumentationEnabled = config
                                            .WithKeys(ConfigurationKeys.FeatureFlags.DelayWcfInstrumentationEnabled)
                                            .AsBool(defaultValue: true);

            WcfWebHttpResourceNamesEnabled = config
                                            .WithKeys(ConfigurationKeys.FeatureFlags.WcfWebHttpResourceNamesEnabled)
                                            .AsBool(defaultValue: true);

            WcfObfuscationEnabled = config
                                   .WithKeys(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled)
                                   .AsBool(defaultValue: true);

            InferredProxySpansEnabled = config
                                      .WithKeys(ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled)
                                      .AsBool(defaultValue: false);

            ObfuscationQueryStringRegex = config
                                         .WithKeys(ConfigurationKeys.ObfuscationQueryStringRegex)
                                         .AsString(defaultValue: TracerSettingsConstants.DefaultObfuscationQueryStringRegex);

            QueryStringReportingEnabled = config
                                         .WithKeys(ConfigurationKeys.QueryStringReportingEnabled)
                                         .AsBool(defaultValue: true);

            QueryStringReportingSize = config
                                      .WithKeys(ConfigurationKeys.QueryStringReportingSize)
                                      .AsInt32(defaultValue: 5000); // 5000 being the tag value length limit

            ObfuscationQueryStringRegexTimeout = config
                                                .WithKeys(ConfigurationKeys.ObfuscationQueryStringRegexTimeout)
                                                .AsDouble(200, val1 => val1 is > 0).Value;

            Func<string[], bool> injectionValidator = styles => styles is { Length: > 0 };
            Func<string, ParsingResult<string[]>> otelConverter =
                style => TrimSplitString(style, commaSeparator)
                        .Select(
                             s => string.Equals(s, "b3", StringComparison.OrdinalIgnoreCase)
                                      ? ContextPropagationHeaderStyle.B3SingleHeader // OTEL's "b3" maps to "b3 single header"
                                      : s)
                        .ToArray();

            var getDefaultPropagationHeaders = () => new DefaultResult<string[]>(
                [ContextPropagationHeaderStyle.Datadog, ContextPropagationHeaderStyle.W3CTraceContext, ContextPropagationHeaderStyle.W3CBaggage],
                $"{ContextPropagationHeaderStyle.Datadog},{ContextPropagationHeaderStyle.W3CTraceContext},{ContextPropagationHeaderStyle.W3CBaggage}");

            // Same otel config is used for both injection and extraction
            var otelPropagation = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.Propagators)
                            .GetAsClassResult(
                                 validator: injectionValidator, // invalid individual values are rejected later
                                 converter: otelConverter);

            PropagationStyleInject = config
                                    .WithKeys(ConfigurationKeys.PropagationStyleInject, "DD_PROPAGATION_STYLE_INJECT", ConfigurationKeys.PropagationStyle)
                                    .GetAsClassResult(
                                         validator: injectionValidator, // invalid individual values are rejected later
                                         converter: style => TrimSplitString(style, commaSeparator))
                                    .OverrideWith(in otelPropagation, ErrorLog, getDefaultPropagationHeaders);

            PropagationStyleExtract = config
                                     .WithKeys(ConfigurationKeys.PropagationStyleExtract, "DD_PROPAGATION_STYLE_EXTRACT", ConfigurationKeys.PropagationStyle)
                                     .GetAsClassResult(
                                          validator: injectionValidator, // invalid individual values are rejected later
                                          converter: style => TrimSplitString(style, commaSeparator))
                                     .OverrideWith(in otelPropagation, ErrorLog, getDefaultPropagationHeaders);

            PropagationExtractFirstOnly = config
                                         .WithKeys(ConfigurationKeys.PropagationExtractFirstOnly)
                                         .AsBool(false);

            PropagationBehaviorExtract = config
                                         .WithKeys(ConfigurationKeys.PropagationBehaviorExtract)
                                         .GetAs(
                                             () => new DefaultResult<ExtractBehavior>(ExtractBehavior.Continue, "continue"),
                                             converter: x => x.ToLowerInvariant() switch
                                             {
                                                 "continue" => ExtractBehavior.Continue,
                                                 "restart" => ExtractBehavior.Restart,
                                                 "ignore" => ExtractBehavior.Ignore,
                                                 _ => ParsingResult<ExtractBehavior>.Failure(),
                                             },
                                             validator: null);

            BaggageMaximumItems = config
                                 .WithKeys(ConfigurationKeys.BaggageMaximumItems)
                                 .AsInt32(defaultValue: W3CBaggagePropagator.DefaultMaximumBaggageItems);

            BaggageMaximumBytes = config
                                 .WithKeys(ConfigurationKeys.BaggageMaximumBytes)
                                 .AsInt32(defaultValue: W3CBaggagePropagator.DefaultMaximumBaggageBytes);

            BaggageTagKeys = config
                            .WithKeys(ConfigurationKeys.BaggageTagKeys)
                            .AsString(defaultValue: "user.id,session.id,account.id")
                            ?.Split([','], StringSplitOptions.RemoveEmptyEntries) ?? [];

            LogSubmissionSettings = new DirectLogSubmissionSettings(source, _telemetry);

            TraceMethods = config
                          .WithKeys(ConfigurationKeys.TraceMethods)
                          .AsString(string.Empty);

            // Filter out tags with empty keys or empty values, and trim whitespaces
            GrpcTags = InitializeHeaderTags(config, ConfigurationKeys.GrpcTags, headerTagsNormalizationFixEnabled: true)
                     ?? ReadOnlyDictionary.Empty;

            OutgoingTagPropagationHeaderMaxLength = config
                                                   .WithKeys(ConfigurationKeys.TagPropagation.HeaderMaxLength)
                                                   .AsInt32(
                                                        Tagging.TagPropagation.OutgoingTagPropagationHeaderMaxLength,
                                                        x => x is >= 0 and <= Tagging.TagPropagation.OutgoingTagPropagationHeaderMaxLength)
                                                   .Value;

            IpHeader = config
                      .WithKeys(ConfigurationKeys.IpHeader, ConfigurationKeys.AppSec.CustomIpHeader)
                      .AsString();

            IpHeaderEnabled = config
                             .WithKeys(ConfigurationKeys.IpHeaderEnabled)
                             .AsBool(false);

            // DSM is now enabled by default in non-serverless environments
            _isDataStreamsMonitoringEnabled = config
                                            .WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled)
                                            .AsBool(
                                                  !EnvironmentHelpers.IsAwsLambda() &&
                                                  !EnvironmentHelpers.IsAzureAppServices() &&
                                                  !EnvironmentHelpers.IsAzureFunctions() &&
                                                  !EnvironmentHelpers.IsGoogleCloudFunctions());

            _isDataStreamsMonitoringInDefaultState = config
                                                    .WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled)
                                                    .AsBool() == null;

            IsDataStreamsLegacyHeadersEnabled = config
                                               .WithKeys(ConfigurationKeys.DataStreamsMonitoring.LegacyHeadersEnabled)
                                               .AsBool(true);

            IsRareSamplerEnabled = config
                                  .WithKeys(ConfigurationKeys.RareSamplerEnabled)
                                  .AsBool(false);

            StatsComputationEnabled = config
                                     .WithKeys(ConfigurationKeys.StatsComputationEnabled)
                                     .AsBool(false); // default is false, but user config can be overridden below

            if (StatsComputationEnabled && !ApmTracingEnabled)
            {
                // if APM is not enabled, disable stats computation (override user config)
                telemetry.Record(ConfigurationKeys.StatsComputationEnabled, value: false, ConfigurationOrigins.Calculated);
                StatsComputationEnabled = false;
            }

            var urlSubstringSkips = config
                                   .WithKeys(ConfigurationKeys.HttpClientExcludedUrlSubstrings)
                                   .AsString(GetDefaultHttpClientExclusions());

            if (isRunningInCiVisibility)
            {
                // always add the additional exclude in ci vis
                const string fakeSessionEndpoint = "/session/FakeSessionIdForPollingPurposes";
                urlSubstringSkips = string.IsNullOrEmpty(urlSubstringSkips)
                                        ? fakeSessionEndpoint
                                        : $"{urlSubstringSkips},{fakeSessionEndpoint}";
                telemetry.Record(ConfigurationKeys.HttpClientExcludedUrlSubstrings, urlSubstringSkips, recordValue: true, ConfigurationOrigins.Calculated);
            }

            HttpClientExcludedUrlSubstrings = !string.IsNullOrEmpty(urlSubstringSkips)
                                                  ? TrimSplitString(urlSubstringSkips.ToUpperInvariant(), commaSeparator)
                                                  : [];

            DbmPropagationMode = config
                                .WithKeys(ConfigurationKeys.DbmPropagationMode)
                                .GetAs(
                                     () => new DefaultResult<DbmPropagationLevel>(DbmPropagationLevel.Disabled, nameof(DbmPropagationLevel.Disabled)),
                                     converter: x => ToDbmPropagationInput(x) ?? ParsingResult<DbmPropagationLevel>.Failure(),
                                     validator: null);

            RemoteConfigurationEnabled = config.WithKeys(ConfigurationKeys.Rcm.RemoteConfigurationEnabled).AsBool(true);

            TraceId128BitGenerationEnabled = config
                                            .WithKeys(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled)
                                            .AsBool(true);
            TraceId128BitLoggingEnabled = config
                                         .WithKeys(ConfigurationKeys.FeatureFlags.TraceId128BitLoggingEnabled)
                                         .AsBool(TraceId128BitGenerationEnabled);

            CommandsCollectionEnabled = config
                                       .WithKeys(ConfigurationKeys.FeatureFlags.CommandsCollectionEnabled)
                                       .AsBool(false);

            BypassHttpRequestUrlCachingEnabled = config.WithKeys(ConfigurationKeys.FeatureFlags.BypassHttpRequestUrlCachingEnabled)
                                                       .AsBool(false);

            InjectContextIntoStoredProceduresEnabled = config.WithKeys(ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled)
                                                       .AsBool(false);

            var defaultDisabledAdoNetCommandTypes = new string[] { "InterceptableDbCommand", "ProfiledDbCommand" };
            var userDisabledAdoNetCommandTypes = config.WithKeys(ConfigurationKeys.DisabledAdoNetCommandTypes).AsString();

            DisabledAdoNetCommandTypes = new HashSet<string>(defaultDisabledAdoNetCommandTypes, StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(userDisabledAdoNetCommandTypes))
            {
                var userSplit = TrimSplitString(userDisabledAdoNetCommandTypes, commaSeparator);
                DisabledAdoNetCommandTypes.UnionWith(userSplit);
            }

            if (source is CompositeConfigurationSource compositeSource)
            {
                foreach (var nestedSource in compositeSource)
                {
                    if (nestedSource is JsonConfigurationSource { JsonConfigurationFilePath: { } jsonFilePath }
                     && !string.IsNullOrEmpty(jsonFilePath))
                    {
                        JsonConfigurationFilePaths.Add(jsonFilePath);
                    }
                }
            }

            OpenTelemetryMetricsEnabled = config
                                    .WithKeys(ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsEnabled)
                                    .AsBool(defaultValue: false);

            var enabledMeters = config.WithKeys(ConfigurationKeys.FeatureFlags.OpenTelemetryMeterNames).AsString();
            OpenTelemetryMeterNames = !string.IsNullOrEmpty(enabledMeters) ? TrimSplitString(enabledMeters, commaSeparator) : [];

            var disabledActivitySources = config.WithKeys(ConfigurationKeys.DisabledActivitySources).AsString();

            DisabledActivitySources = !string.IsNullOrEmpty(disabledActivitySources) ? TrimSplitString(disabledActivitySources, commaSeparator) : [];

            // we "enrich" with these values which aren't _strictly_ configuration, but which we want to track as we tracked them in v1
            telemetry.Record(ConfigTelemetryData.NativeTracerVersion, Instrumentation.GetNativeTracerVersion(), recordValue: true, ConfigurationOrigins.Default);
            telemetry.Record(ConfigTelemetryData.FullTrustAppDomain, value: AppDomain.CurrentDomain.IsFullyTrusted, ConfigurationOrigins.Default);
            telemetry.Record(ConfigTelemetryData.ManagedTracerTfm, value: ConfigTelemetryData.ManagedTracerTfmValue, recordValue: true, ConfigurationOrigins.Default);

            // these are SSI variables that would be useful for correlation purposes
            telemetry.Record(ConfigTelemetryData.SsiInjectionEnabled, value: EnvironmentHelpers.GetEnvironmentVariable("DD_INJECTION_ENABLED"), recordValue: true, ConfigurationOrigins.EnvVars);
            telemetry.Record(ConfigTelemetryData.SsiAllowUnsupportedRuntimesEnabled, value: EnvironmentHelpers.GetEnvironmentVariable("DD_INJECT_FORCE"), recordValue: true, ConfigurationOrigins.EnvVars);

            var installType = EnvironmentHelpers.GetEnvironmentVariable("DD_INSTRUMENTATION_INSTALL_TYPE");

            var instrumentationSource = installType switch
            {
                "dd_dotnet_launcher" => "cmd_line",
                "dd_trace_tool" => "cmd_line",
                "dotnet_msi" => "env_var",
                "windows_fleet_installer" => "ssi", // windows SSI on IIS
                _ when !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable("DD_INJECTION_ENABLED")) => "ssi", // "normal" ssi
                _ => "manual" // everything else
            };

            telemetry.Record(ConfigTelemetryData.InstrumentationSource, instrumentationSource, recordValue: true, ConfigurationOrigins.Calculated);

            if (AzureAppServiceMetadata is not null)
            {
                telemetry.Record(ConfigTelemetryData.AasConfigurationError, AzureAppServiceMetadata.IsUnsafeToTrace, ConfigurationOrigins.Default);
                telemetry.Record(ConfigTelemetryData.CloudHosting, "Azure", recordValue: true, ConfigurationOrigins.Default);
                telemetry.Record(ConfigTelemetryData.AasAppType, AzureAppServiceMetadata.SiteType, recordValue: true, ConfigurationOrigins.Default);
            }

            GraphQLErrorExtensions = TrimSplitString(
                config.WithKeys(ConfigurationKeys.GraphQLErrorExtensions).AsString(),
                commaSeparator);

            static void RecordDisabledIntegrationsTelemetry(IntegrationSettingsCollection integrations, IConfigurationTelemetry telemetry)
            {
                // Record the final disabled settings values in the telemetry, we can't quite get this information
                // through the IntegrationTelemetryCollector currently so record it here instead
                StringBuilder? sb = null;

                foreach (var setting in integrations.Settings)
                {
                    if (setting.Enabled == false)
                    {
                        sb ??= StringBuilderCache.Acquire();
                        sb.Append(setting.IntegrationName);
                        sb.Append(';');
                    }
                }

                var value = sb is null ? null : StringBuilderCache.GetStringAndRelease(sb);
                telemetry.Record(ConfigurationKeys.DisabledIntegrations, value, recordValue: true, ConfigurationOrigins.Calculated);
            }
        }

        internal HashSet<string> ExperimentalFeaturesEnabled { get; }

        internal OverrideErrorLog ErrorLog { get; }

        internal IConfigurationTelemetry Telemetry => _telemetry;

        /// <summary>
        /// Gets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        public string? Environment { get; }

        /// <summary>
        /// Gets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        public string? ServiceName { get; }

        /// <summary>
        /// Gets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        public string? ServiceVersion { get; }

        /// <summary>
        /// Gets the application's git repository url.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitRepositoryUrl"/>
        internal string? GitRepositoryUrl { get; }

        /// <summary>
        /// Gets the application's git commit hash.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitCommitSha"/>
        internal string? GitCommitSha { get; }

        /// <summary>
        /// Gets a value indicating whether we should tag every telemetry event with git metadata.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitMetadataEnabled"/>
        internal bool GitMetadataEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        public bool TraceEnabled => DynamicSettings.TraceEnabled ?? _traceEnabled;

        /// <summary>
        /// Gets a value indicating whether APM traces are enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ApmTracingEnabled"/>
        internal bool ApmTracingEnabled => DynamicSettings.ApmTracingEnabled ?? _apmTracingEnabled;

        /// <summary>
        /// Gets a value indicating whether profiling is enabled.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ContinuousProfiler.ConfigurationKeys.ProfilingEnabled"/>
        internal bool ProfilingEnabledInternal { get; }

        /// <summary>
        /// Gets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        public HashSet<string> DisabledIntegrationNames { get; }

        /// <summary>
        /// Gets a value indicating whether OpenTelemetry Metrics are enabled.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsEnabled"/>
        internal bool OpenTelemetryMetricsEnabled { get; }

        /// Gets the names of enabled Meters.
        /// <seealso cref="ConfigurationKeys.FeatureFlags.OpenTelemetryMeterNames"/>
        internal string[] OpenTelemetryMeterNames { get; }

        /// <summary>
        /// Gets the names of disabled ActivitySources.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledActivitySources"/>
        internal string[] DisabledActivitySources { get; }

        /// <summary>
        /// Gets the transport settings that dictate how the tracer connects to the agent.
        /// </summary>
        public ExporterSettings Exporter { get; }

        /// <summary>
        /// Gets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        public bool AnalyticsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>, unless <see cref="ConfigurationKeys.DirectLogSubmission.EnabledIntegrations"/>
        /// enables Direct Log Submission.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        public bool LogsInjectionEnabled => DynamicSettings.LogsInjectionEnabled ?? LogSubmissionSettings.LogsInjectionEnabled;

        /// <summary>
        /// Gets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
        public int MaxTracesSubmittedPerSecond { get; }

        /// <summary>
        /// Gets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        public string? CustomSamplingRules => DynamicSettings.SamplingRules ?? _customSamplingRules;

        internal bool CustomSamplingRulesIsRemote => DynamicSettings.SamplingRules != null;

        /// <summary>
        /// Gets a value indicating the format for custom trace sampling rules ("regex" or "glob").
        /// If the value is not recognized, trace sampling rules are disabled.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRulesFormat"/>
        internal string CustomSamplingRulesFormat { get; }

        /// <summary>
        /// Gets a value indicating span sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.SpanSamplingRules"/>
        internal string? SpanSamplingRules { get; }

        /// <summary>
        /// Gets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        public double? GlobalSamplingRate => DynamicSettings.GlobalSamplingRate ?? _globalSamplingRate;

        /// <summary>
        /// Gets a collection of <see cref="IntegrationSettings"/> keyed by integration name.
        /// </summary>
        public IntegrationSettingsCollection Integrations { get; }

        /// <summary>
        /// Gets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        public IReadOnlyDictionary<string, string> GlobalTags => DynamicSettings.GlobalTags ?? _globalTags;

        /// <summary>
        /// Gets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing HTTP requests.
        /// </summary>
        public IReadOnlyDictionary<string, string> HeaderTags => DynamicSettings.HeaderTags ?? _headerTags;

        /// <summary>
        /// Gets a custom request header configured to read the ip from. For backward compatibility, it fallbacks on DD_APPSEC_IPHEADER
        /// </summary>
        internal string? IpHeader { get; }

        /// <summary>
        /// Gets a value indicating whether the ip header should not be collected. The default is false.
        /// </summary>
        internal bool IpHeaderEnabled { get; }

        /// <summary>
        /// Gets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        public IReadOnlyDictionary<string, string> GrpcTags { get; }

        /// <summary>
        /// Gets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        public bool TracerMetricsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether stats are computed on the tracer side
        /// </summary>
        public bool StatsComputationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the use
        /// of System.Diagnostics.DiagnosticSource is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <remark>
        /// This value cannot be set in code. Instead,
        /// set it using the <c>DD_TRACE_DIAGNOSTIC_SOURCE_ENABLED</c>
        /// environment variable or in configuration files.
        /// </remark>
        [PublicApi]
        public bool DiagnosticSourceEnabled
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Get);
                return GlobalSettings.Instance.DiagnosticSourceEnabled;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a span context should be created on exiting a successful Kafka
        /// Consumer.Consume() call, and closed on entering Consumer.Consume().
        /// </summary>
        /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
        public bool KafkaCreateConsumerScopeEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to enable the updated WCF instrumentation that delays execution
        /// until later in the WCF pipeline when the WCF server exception handling is established.
        /// </summary>
        internal bool DelayWcfInstrumentationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to enable improved template-based resource names
        /// when using WCF Web HTTP.
        /// </summary>
        internal bool WcfWebHttpResourceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to obfuscate the <c>LocalPath</c> of a WCF request that goes
        /// into the <c>resourceName</c> of a span.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled"/>
        internal bool WcfObfuscationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to generate an inferred span based on extracted headers from a proxy service.
        /// </summary>
        /// <seeaslo cref="ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled"/>
        internal bool InferredProxySpansEnabled { get; }

        /// <summary>
        /// Gets a value indicating the regex to apply to obfuscate http query strings.
        /// Warning: This regex cause crashes under netcoreapp2.1 / linux / arm64, DON'T use default value on manual instrumentation
        /// </summary>
        internal string ObfuscationQueryStringRegex { get; }

        /// <summary>
        /// Gets a value indicating whether or not http.url should contain the query string, enabled by default
        /// </summary>
        internal bool QueryStringReportingEnabled { get; }

        /// <summary>
        /// Gets a value limiting the size of the querystring to report and obfuscate
        /// Default value is 5000, 0 means that we don't limit the size.
        /// </summary>
        internal int QueryStringReportingSize { get; }

        /// <summary>
        /// Gets a value indicating a timeout in milliseconds to the execution of the query string obfuscation regex
        /// Default value is 200ms
        /// </summary>
        internal double ObfuscationQueryStringRegexTimeout { get; }

        /// <summary>
        /// Gets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        public bool StartupDiagnosticLogEnabled { get; }

        /// <summary>
        /// Gets the time interval (in seconds) for sending stats
        /// </summary>
        internal int StatsComputationInterval { get; }

        /// <summary>
        /// Gets the maximum length of an outgoing propagation header's value ("x-datadog-tags")
        /// when injecting it into downstream service calls.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagPropagation.HeaderMaxLength"/>
        /// <remarks>
        /// This value is not used when extracting an incoming propagation header from an upstream service.
        /// </remarks>
        internal int OutgoingTagPropagationHeaderMaxLength { get; }

        /// <summary>
        /// Gets a value indicating the injection propagation style.
        /// </summary>
        internal string[] PropagationStyleInject { get; }

        /// <summary>
        /// Gets a value indicating the extraction propagation style.
        /// </summary>
        internal string[] PropagationStyleExtract { get; }

        /// <summary>
        /// Gets a value indicating whether the propagation should only try
        /// extract the first header.
        /// </summary>
        internal bool PropagationExtractFirstOnly { get; }

        /// <summary>
        /// Gets a value indicating the behavior when extracting propagation headers.
        /// </summary>
        internal ExtractBehavior PropagationBehaviorExtract { get; }

        /// <summary>
        /// Gets the maximum number of items that can be
        /// injected into the baggage header when propagating to a downstream service.
        /// Default value is 64 items.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.BaggageMaximumItems"/>
        internal int BaggageMaximumItems { get; }

        /// <summary>
        /// Gets the maximum number of bytes that can be
        /// injected into the baggage header when propagating to a downstream service.
        /// Default value is 8192 bytes.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.BaggageMaximumBytes"/>
        internal int BaggageMaximumBytes { get; }

        /// <summary>
        /// Gets the configuration for which baggage keys are converted into span tags.
        /// Default value is "user.id,session.id,account.id".
        /// </summary>
        /// <seealso cref="ConfigurationKeys.BaggageTagKeys"/>
        internal string[] BaggageTagKeys { get; }

        /// <summary>
        /// Gets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled => DynamicSettings.RuntimeMetricsEnabled ?? _runtimeMetricsEnabled;

        /// <summary>
        /// Gets a value indicating whether libdatadog data pipeline
        /// is enabled.
        /// </summary>
        internal bool DataPipelineEnabled { get; }

        /// <summary>
        /// Gets the comma separated list of url patterns to skip tracing.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientExcludedUrlSubstrings"/>
        internal string[] HttpClientExcludedUrlSubstrings { get; }

        /// <summary>
        /// Gets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        internal bool[] HttpServerErrorStatusCodes { get; }

        /// <summary>
        /// Gets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        internal bool[] HttpClientErrorStatusCodes { get; }

        /// <summary>
        /// Gets configuration values for changing service names based on configuration
        /// </summary>
        internal IReadOnlyDictionary<string, string> ServiceNameMappings => DynamicSettings.ServiceNameMappings ?? _serviceNameMappings;

        /// <summary>
        /// Gets configuration values for changing peer service names based on configuration
        /// </summary>
        internal IReadOnlyDictionary<string, string>? PeerServiceNameMappings { get; }

        /// <summary>
        /// Gets a value indicating the size in bytes of the trace buffer
        /// </summary>
        internal int TraceBufferSize { get; }

        /// <summary>
        /// Gets a value indicating the batch interval for the serialization queue, in milliseconds.
        /// Set to 0 to disable.
        /// </summary>
        internal int TraceBatchInterval { get; }

        /// <summary>
        /// Gets a value indicating whether the feature flag to enable the updated ASP.NET resource names is enabled
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled"/>
        internal bool RouteTemplateResourceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether resource names for ASP.NET and ASP.NET Core spans should be expanded. Only applies
        /// when <see cref="RouteTemplateResourceNamesEnabled"/> is <code>true</code>.
        /// </summary>
        internal bool ExpandRouteTemplatesEnabled { get; }

        /// <summary>
        /// Gets the direct log submission settings.
        /// </summary>
        internal DirectLogSubmissionSettings LogSubmissionSettings { get; }

        /// <summary>
        /// Gets a value indicating the trace methods configuration.
        /// </summary>
        internal string TraceMethods { get; }

        /// <summary>
        /// Gets a value indicating whether the activity listener is enabled or not.
        /// </summary>
        internal bool IsActivityListenerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether data streams monitoring is enabled or not.
        /// </summary>
        internal bool IsDataStreamsMonitoringEnabled => DynamicSettings.DataStreamsMonitoringEnabled ?? _isDataStreamsMonitoringEnabled;

        /// <summary>
        /// Gets a value indicating whether data streams configuration is present or not (set to true or false).
        /// </summary>
        internal bool IsDataStreamsMonitoringInDefaultState => DynamicSettings.DataStreamsMonitoringEnabled == null && _isDataStreamsMonitoringInDefaultState;

        /// <summary>
        /// Gets a value indicating whether data streams schema extraction is enabled or not.
        /// </summary>
        internal bool IsDataStreamsSchemaExtractionEnabled => IsDataStreamsMonitoringEnabled && !IsDataStreamsMonitoringInDefaultState;

        /// <summary>
        /// Gets a value indicating whether to inject legacy binary headers for Data Streams.
        /// </summary>
        internal bool IsDataStreamsLegacyHeadersEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the rare sampler is enabled or not.
        /// </summary>
        internal bool IsRareSamplerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in AzureAppServices (AAS).
        /// </summary>
        internal bool IsRunningInAzureAppService => AzureAppServiceMetadata is not null;

        /// <summary>
        /// Gets a value indicating whether the tracer is running in Azure Functions.
        /// </summary>
        internal bool IsRunningInAzureFunctions => AzureAppServiceMetadata?.IsFunctionsApp ?? false;

        /// <summary>
        /// Gets a value indicating whether the tracer is running in an Azure Function on a
        /// consumption plan
        /// </summary>
        internal bool IsRunningMiniAgentInAzureFunctions => AzureAppServiceMetadata?.IsRunningMiniAgentInAzureFunctions ?? false;

        /// <summary>
        /// Gets a value indicating whether the tracer is running in Google Cloud Functions
        /// </summary>
        internal bool IsRunningInGCPFunctions { get; }

        /// <summary>
        /// Gets the AWS Lambda settings, including whether we're currently running in Lambda
        /// </summary>
        internal LambdaMetadata LambdaMetadata { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer should propagate service data in db queries
        /// </summary>
        internal DbmPropagationLevel DbmPropagationMode { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will generate 128-bit trace ids
        /// instead of 64-bits trace ids.
        /// </summary>
        internal bool TraceId128BitGenerationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will inject 128-bit trace ids into logs, if available,
        /// instead of 64-bit trace ids. Note that a 128-bit trace id may be received from an upstream service
        /// even if we are not generating them.
        /// </summary>
        internal bool TraceId128BitLoggingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will send the shell commands of
        /// the "command_execution" integration to the agent.
        /// </summary>
        internal bool CommandsCollectionEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will bypass .NET Framework's
        /// HttpRequestUrl caching when HttpRequest.Url is accessed.
        /// </summary>
        internal bool BypassHttpRequestUrlCachingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer will inject context into
        /// StoredProcedure commands for Microsoft SQL Server.
        /// Requires the <see cref="DbmPropagationMode"/> to be set to either <see cref="DbmPropagationLevel.Service"/> or <see cref="DbmPropagationLevel.Full"/>.
        /// </summary>
        internal bool InjectContextIntoStoredProceduresEnabled { get; }

        /// <summary>
        /// Gets the AAS settings
        /// </summary>
        internal ImmutableAzureAppServiceSettings? AzureAppServiceMetadata { get; }

        /// <summary>
        /// Gets the GCP Function settings
        /// </summary>
        internal ImmutableGCPFunctionSettings? GCPFunctionSettings { get; }

        /// <summary>
        /// Gets a value indicating whether to calculate the peer.service tag from predefined precursor attributes when using the v0 schema.
        /// </summary>
        internal bool PeerServiceTagsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to remove the service names when using the v0 schema.
        /// </summary>
        internal bool RemoveClientServiceNamesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to add span pointers on AWS requests.
        /// </summary>
        internal bool SpanPointersEnabled { get; }

        /// <summary>
        /// Gets the metadata schema version
        /// </summary>
        internal SchemaVersion MetadataSchemaVersion { get; }

        /// <summary>
        /// Gets a value indicating whether remote configuration has been explicitly disabled.
        /// </summary>
        internal bool RemoteConfigurationEnabled { get; }

        /// <summary>
        /// Gets the disabled ADO.NET Command Types that won't have spans generated for them.
        /// </summary>
        internal HashSet<string> DisabledAdoNetCommandTypes { get; }

        internal ImmutableDynamicSettings DynamicSettings { get; init; } = new();

        internal List<string> JsonConfigurationFilePaths { get; } = new();

        /// <summary>
        /// Gets which GraphQL error extensions to capture.
        /// A comma-separated list of extension keys to capture. Empty or not present means no extensions are captured.        /// </summary>
        /// <seealso cref="ConfigurationKeys.GraphQLErrorExtensions"/>
        internal string[] GraphQLErrorExtensions { get; }

        /// <summary>
        /// Gets a value indicating whether remote configuration is potentially available.
        /// RCM requires the "full" Go agent (not just the trace agent, and not the Rust agents),
        /// so is not available in some scenarios. It may also be explicitly disabled.
        /// </summary>
        // NOTE: when we clean this up, see also EnvironmentHelpers.IsServerlessEnvironment()
        internal bool IsRemoteConfigurationAvailable =>
            RemoteConfigurationEnabled &&
            !IsRunningInAzureAppService &&
            !IsRunningInAzureFunctions &&
            !IsRunningInGCPFunctions &&
            !LambdaMetadata.IsRunningInLambda;

        internal static TracerSettings FromDefaultSourcesInternal()
            => new(GlobalConfigurationSource.Instance, new ConfigurationTelemetry(), new());

        internal static ReadOnlyDictionary<string, string>? InitializeServiceNameMappings(ConfigurationBuilder config, string key)
        {
            var mappings = config
               .WithKeys(key)
               .AsDictionary()
              ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
               .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());
            return mappings is not null ? new(mappings) : null;
        }

        internal static ReadOnlyDictionary<string, string>? InitializeHeaderTags(ConfigurationBuilder config, string key, bool headerTagsNormalizationFixEnabled)
        {
            var configurationDictionary = config
                   .WithKeys(key)
                   .AsDictionary(allowOptionalMappings: true, () => new Dictionary<string, string>());

            if (configurationDictionary == null)
            {
                return null;
            }

            var headerTags = new Dictionary<string, string>(configurationDictionary.Count);

            foreach (var kvp in configurationDictionary)
            {
                var headerName = kvp.Key.Trim();

                if (string.IsNullOrEmpty(headerName))
                {
                    continue;
                }

                if (InitializeHeaderTag(tagName: kvp.Value, headerTagsNormalizationFixEnabled, out var finalTagName))
                {
                    headerTags.Add(headerName, finalTagName);
                }
            }

            return new(headerTags);
        }

        internal static bool InitializeHeaderTag(
            string? tagName,
            bool headerTagsNormalizationFixEnabled,
            [NotNullWhen(true)] out string? finalTagName)
        {
            tagName = tagName?.Trim();

            if (string.IsNullOrEmpty(tagName))
            {
                // The user did not provide a tag name. Normalization will happen later, when adding the tag prefix.
                finalTagName = string.Empty;
                return true;
            }

            if (!SpanTagHelper.IsValidTagName(tagName!, out tagName))
            {
                // invalid tag name
                finalTagName = null;
                return false;
            }

            if (headerTagsNormalizationFixEnabled)
            {
                // Default code path: if the user provided a tag name, don't try to normalize it.
                finalTagName = tagName;
                return true;
            }

            // user opted via feature flag into the previous behavior,
            // where tag names were normalized even when specified
            // (but _not_ spaces, due to a bug in the normalization code)
            return SpanTagHelper.TryNormalizeTagName(tagName, normalizeSpaces: false, out finalTagName);
        }

        internal static string[] TrimSplitString(string? textValues, char[] separators)
        {
            if (string.IsNullOrWhiteSpace(textValues))
            {
                return Array.Empty<string>();
            }

            var values = textValues!.Split(separators);
            var list = new List<string>(values.Length);

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var trimmedValue = value.Trim();
                    if (!list.Contains(trimmedValue))
                    {
                        list.Add(trimmedValue);
                    }
                }
            }

            return list.ToArray();
        }

        internal static bool[] ParseHttpCodesToArray(IEnumerable<int> httpStatusErrorCodes)
        {
            var httpErrorCodesArray = new bool[600];
            foreach (var errorCode in httpStatusErrorCodes)
            {
                if (errorCode >= 0 && errorCode < httpErrorCodesArray.Length)
                {
                    httpErrorCodesArray[errorCode] = true;
                }
            }

            return httpErrorCodesArray;
        }

        internal static bool[] ParseHttpCodesToArray(string httpStatusErrorCodes)
        {
            bool[] httpErrorCodesArray = new bool[600];

            void TrySetValue(int index)
            {
                if (index >= 0 && index < httpErrorCodesArray.Length)
                {
                    httpErrorCodesArray[index] = true;
                }
            }

            string[] configurationsArray = httpStatusErrorCodes.Replace(" ", string.Empty).Split(',');

            foreach (string statusConfiguration in configurationsArray)
            {
                int startStatus;

                // Checks that the value about to be used follows the `401-404` structure or single 3 digit number i.e. `401` else log the warning
                if (!Regex.IsMatch(statusConfiguration, @"^\d{3}-\d{3}$|^\d{3}$"))
                {
                    Log.Warning("Wrong format '{0}' for DD_TRACE_HTTP_SERVER/CLIENT_ERROR_STATUSES configuration.", statusConfiguration);
                }

                // If statusConfiguration equals a single value i.e. `401` parse the value and save to the array
                else if (int.TryParse(statusConfiguration, out startStatus))
                {
                    TrySetValue(startStatus);
                }
                else
                {
                    string[] statusCodeLimitsRange = statusConfiguration.Split('-');

                    startStatus = int.Parse(statusCodeLimitsRange[0]);
                    int endStatus = int.Parse(statusCodeLimitsRange[1]);

                    if (endStatus < startStatus)
                    {
                        startStatus = endStatus;
                        endStatus = int.Parse(statusCodeLimitsRange[0]);
                    }

                    for (int statusCode = startStatus; statusCode <= endStatus; statusCode++)
                    {
                        TrySetValue(statusCode);
                    }
                }
            }

            return httpErrorCodesArray;
        }

        internal bool IsErrorStatusCode(int statusCode, bool serverStatusCode)
        {
            var source = serverStatusCode ? HttpServerErrorStatusCodes : HttpClientErrorStatusCodes;

            if (source == null)
            {
                return false;
            }

            if (statusCode >= source.Length)
            {
                return false;
            }

            return source[statusCode];
        }

        internal bool IsIntegrationEnabled(IntegrationId integration, bool defaultValue = true)
        {
            if (TraceEnabled && !_domainMetadata.ShouldAvoidAppDomain())
            {
                return Integrations[integration].Enabled ?? defaultValue;
            }

            return false;
        }

        [Obsolete(DeprecationMessages.AppAnalytics)]
        internal double? GetIntegrationAnalyticsSampleRate(IntegrationId integration, bool enabledWithGlobalSetting)
        {
            var integrationSettings = Integrations[integration];
            var analyticsEnabled = integrationSettings.AnalyticsEnabled ?? (enabledWithGlobalSetting && AnalyticsEnabled);
            return analyticsEnabled ? integrationSettings.AnalyticsSampleRate : (double?)null;
        }

        internal string GetDefaultHttpClientExclusions()
        {
            if (IsRunningInAzureAppService)
            {
                return ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions;
            }

            if (LambdaMetadata.IsRunningInLambda)
            {
                return LambdaMetadata.DefaultHttpClientExclusions;
            }

            return string.Empty;
        }

        private static DbmPropagationLevel? ToDbmPropagationInput(string inputValue)
        {
            inputValue = inputValue.Trim(); // we know inputValue isn't null (and have tests for it)
            if (inputValue.Equals("disabled", StringComparison.OrdinalIgnoreCase))
            {
                return DbmPropagationLevel.Disabled;
            }
            else if (inputValue.Equals("service", StringComparison.OrdinalIgnoreCase))
            {
                return DbmPropagationLevel.Service;
            }
            else if (inputValue.Equals("full", StringComparison.OrdinalIgnoreCase))
            {
                return DbmPropagationLevel.Full;
            }
            else
            {
                Log.Warning("Wrong setting '{PropagationInput}' for DD_DBM_PROPAGATION_MODE supported values include: disabled, service or full", inputValue);
                return null;
            }
        }

        internal static TracerSettings Create(Dictionary<string, object?> settings)
            => Create(settings, LibDatadog.NativeInterop.IsLibDatadogAvailable);

        internal static TracerSettings Create(Dictionary<string, object?> settings, bool isLibDatadogAvailable)
            => new(new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => x.Value?.ToString()!)), new ConfigurationTelemetry(), new(), isLibDatadogAvailable);

        internal void CollectTelemetry(IConfigurationTelemetry destination)
        {
            // copy the current settings into telemetry
            _telemetry.CopyTo(destination);

            // If ExporterSettings has been replaced, it will have its own telemetry collector
            // so we need to record those values too.
            if (Exporter.Telemetry is { } exporterTelemetry
             && exporterTelemetry != _telemetry)
            {
                exporterTelemetry.CopyTo(destination);
            }
        }

        private static double? BuildSampleRate(OverrideErrorLog log, in ConfigurationBuilder config)
        {
            // The "overriding" is complex, so we can't use the usual `OverrideWith()` approach
            var ddSampleRate = config.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDoubleResult();
            var otelSampleType = config.WithKeys(ConfigurationKeys.OpenTelemetry.TracesSampler).AsStringResult();
            var otelSampleRate = config.WithKeys(ConfigurationKeys.OpenTelemetry.TracesSamplerArg).AsDoubleResult();

            double? ddResult = ddSampleRate.ConfigurationResult.IsValid ? ddSampleRate.ConfigurationResult.Result : null;

            // more complex, so can't use built-in `Merge()` support
            if (ddSampleRate.ConfigurationResult.IsPresent)
            {
                if (otelSampleType.ConfigurationResult.IsPresent)
                {
                    log.LogDuplicateConfiguration(ddSampleRate.Key, otelSampleType.Key);
                }

                if (otelSampleRate.ConfigurationResult.IsPresent)
                {
                    log.LogDuplicateConfiguration(ddSampleRate.Key, otelSampleRate.Key);
                }
            }
            else if (otelSampleType.ConfigurationResult is { IsValid: true, Result: { } samplerName })
            {
                const string parentbasedAlwaysOn = "parentbased_always_on";
                const string parentbasedAlwaysOff = "parentbased_always_off";
                const string parentbasedTraceidratio = "parentbased_traceidratio";

                string? supportedSamplerName = samplerName switch
                {
                    parentbasedAlwaysOn => parentbasedAlwaysOn,
                    "always_on" => parentbasedAlwaysOn,
                    parentbasedAlwaysOff => parentbasedAlwaysOff,
                    "always_off" => parentbasedAlwaysOff,
                    parentbasedTraceidratio => parentbasedTraceidratio,
                    "traceidratio" => parentbasedTraceidratio,
                    _ => null,
                };

                if (supportedSamplerName is null)
                {
                    log.EnqueueAction(
                        (log, _) =>
                        {
                            log.Warning(
                                "OpenTelemetry configuration {OpenTelemetryConfiguration}={OpenTelemetryValue} is not supported. Using default configuration.",
                                otelSampleType.Key,
                                samplerName);
                        });
                    return ddResult;
                }

                if (!string.Equals(samplerName, supportedSamplerName, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogUnsupportedConfiguration(otelSampleType.Key, samplerName, supportedSamplerName);
                }

                var openTelemetrySampleRateResult = supportedSamplerName switch
                {
                    parentbasedAlwaysOn => ConfigurationResult<double>.Valid(1.0),
                    parentbasedAlwaysOff => ConfigurationResult<double>.Valid(0.0),
                    parentbasedTraceidratio => otelSampleRate.ConfigurationResult,
                    _ => ConfigurationResult<double>.ParseFailure(),
                };

                if (openTelemetrySampleRateResult is { Result: { } sampleRateResult, IsValid: true })
                {
                    return sampleRateResult;
                }

                log.LogInvalidConfiguration(otelSampleRate.Key);
            }

            return ddResult;
        }

        private static ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> RemapOtelTags(
            in ConfigurationBuilder.ClassConfigurationResultWithKey<IDictionary<string, string>> original)
        {
            if (original.ConfigurationResult is { IsValid: true, Result: { } values })
            {
                // Update well-known service information resources
                if (values.TryGetValue("deployment.environment", out var envValue))
                {
                    values.Remove("deployment.environment");
                    values[Tags.Env] = envValue;
                }

                if (values.TryGetValue("service.name", out var serviceValue))
                {
                    values.Remove("service.name");
                    values[Tags.Service] = serviceValue;
                }

                if (values.TryGetValue("service.version", out var versionValue))
                {
                    values.Remove("service.version");
                    values[Tags.Version] = versionValue;
                }
            }

            return original;
        }

        private string? GetExplicitSettingOrTag(
            string? explicitSetting,
            Dictionary<string, string> globalTags,
            string tag,
            string telemetryKey)
        {
            string? result = null;
            if (!string.IsNullOrWhiteSpace(explicitSetting))
            {
                result = explicitSetting!.Trim();
                if (result != explicitSetting)
                {
                    _telemetry.Record(telemetryKey, result, recordValue: true, ConfigurationOrigins.Calculated);
                }
            }
            else
            {
                var version = globalTags.GetValueOrDefault(tag);
                if (!string.IsNullOrWhiteSpace(version))
                {
                    result = version.Trim();
                    _telemetry.Record(telemetryKey, result, recordValue: true, ConfigurationOrigins.Calculated);
                }
            }

            return result;
        }
    }
}

// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
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
    [GenerateSnapshot]
    public partial class TracerSettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerSettings>();

        private readonly IConfigurationTelemetry _telemetry;
        private readonly TracerSettingsSnapshot _initialSettings;

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
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values,
        /// or initializes the configuration from environment variables and configuration files.
        /// Calling <c>new TracerSettings(true)</c> is equivalent to calling <c>TracerSettings.FromDefaultSources()</c>
        /// </summary>
        /// <param name="useDefaultSources">If <c>true</c>, creates a <see cref="TracerSettings"/> populated from
        /// the default sources such as environment variables etc. If <c>false</c>, uses the default values.</param>
        [PublicApi]
        public TracerSettings(bool useDefaultSources)
            : this(useDefaultSources ? GlobalConfigurationSource.Instance : null, new ConfigurationTelemetry(), new OverrideErrorLog())
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Ctor_UseDefaultSources);
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

        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, OverrideErrorLog errorLog)
        {
            var commaSeparator = new[] { ',' };
            source ??= NullConfigurationSource.Instance;
            _telemetry = telemetry;
            ErrorLog = errorLog;
            var config = new ConfigurationBuilder(source, _telemetry);

            GCPFunctionSettings = new ImmutableGCPFunctionSettings(source, _telemetry);
            IsRunningInGCPFunctions = GCPFunctionSettings.IsGCPFunction;

            LambdaMetadata = LambdaMetadata.Create();

            IsRunningInAzureAppService = ImmutableAzureAppServiceSettings.GetIsAzureAppService(source, telemetry);
            IsRunningMiniAgentInAzureFunctions = ImmutableAzureAppServiceSettings.GetIsFunctionsAppUsingMiniAgent(source, telemetry);

            if (IsRunningInAzureAppService)
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

            EnvironmentInternal = config
                         .WithKeys(ConfigurationKeys.Environment)
                         .AsString();

            var otelServiceName = config.WithKeys(ConfigurationKeys.OpenTelemetry.ServiceName).AsStringResult();
            ServiceNameInternal = config
                                 .WithKeys(ConfigurationKeys.ServiceName, "DD_SERVICE_NAME")
                                 .AsStringResult()
                                 .OverrideWith(in otelServiceName, ErrorLog);

            ServiceVersionInternal = config
                            .WithKeys(ConfigurationKeys.ServiceVersion)
                            .AsString();

            GitCommitSha = config
                          .WithKeys(ConfigurationKeys.GitCommitSha)
                          .AsString();

            GitRepositoryUrl = config
                              .WithKeys(ConfigurationKeys.GitRepositoryUrl)
                              .AsString();

            GitMetadataEnabled = config
                                .WithKeys(ConfigurationKeys.GitMetadataEnabled)
                                .AsBool(defaultValue: true);

            var otelTraceEnabled = config
                                  .WithKeys(ConfigurationKeys.OpenTelemetry.TracesExporter)
                                  .AsBoolResult(
                                       value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                                                    ? ParsingResult<bool>.Success(result: false)
                                                    : ParsingResult<bool>.Failure());
            TraceEnabledInternal = config
                                  .WithKeys(ConfigurationKeys.TraceEnabled)
                                  .AsBoolResult()
                                  .OverrideWith(in otelTraceEnabled, ErrorLog, defaultValue: true);

            AppsecStandaloneEnabledInternal = config
                          .WithKeys(ConfigurationKeys.AppsecStandaloneEnabled)
                          .AsBool(defaultValue: false);

            if (AzureAppServiceMetadata?.IsUnsafeToTrace == true)
            {
                TraceEnabledInternal = false;
            }

            var disabledIntegrationNames = config.WithKeys(ConfigurationKeys.DisabledIntegrations)
                                                               .AsString()
                                                              ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNamesInternal = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            IntegrationsInternal = new IntegrationSettingsCollection(source, unusedParamNotToUsePublicApi: false);

            ExporterInternal = new ExporterSettings(source, _telemetry);

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabledInternal = config.WithKeys(ConfigurationKeys.GlobalAnalyticsEnabled)
                                                   .AsBool(defaultValue: false);
#pragma warning restore 618

#pragma warning disable 618 // this parameter has been replaced but may still be used
            MaxTracesSubmittedPerSecondInternal = config
                                         .WithKeys(ConfigurationKeys.TraceRateLimit, ConfigurationKeys.MaxTracesSubmittedPerSecond)
#pragma warning restore 618
                                         .AsInt32(defaultValue: 100);

            var otelTags = config
                          .WithKeys(ConfigurationKeys.OpenTelemetry.ResourceAttributes)
                          .AsDictionaryResult(separator: '=');

            // backwards compatibility for names used in the past
            GlobalTagsInternal = config
                                .WithKeys(ConfigurationKeys.GlobalTags, "DD_TRACE_GLOBAL_TAGS")
                                .AsDictionaryResult()
                                .OverrideWith(
                                     RemapOtelTags(in otelTags),
                                     ErrorLog,
                                     () => new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), string.Empty))

                                 // Filter out tags with empty keys or empty values, and trim whitespace
                                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            var headerTagsNormalizationFixEnabled = config
                                               .WithKeys(ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled)
                                               .AsBool(defaultValue: true);

            // Filter out tags with empty keys or empty values, and trim whitespaces
            HeaderTagsInternal = InitializeHeaderTags(config, ConfigurationKeys.HeaderTags, headerTagsNormalizationFixEnabled)
                ?? new Dictionary<string, string>();

            PeerServiceTagsEnabled = config
               .WithKeys(ConfigurationKeys.PeerServiceDefaultsEnabled)
               .AsBool(defaultValue: false);
            RemoveClientServiceNamesEnabled = config
               .WithKeys(ConfigurationKeys.RemoveClientServiceNamesEnabled)
               .AsBool(defaultValue: false);

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

            ServiceNameMappings = InitializeServiceNameMappings(config, ConfigurationKeys.ServiceNameMappings);

            TracerMetricsEnabledInternal = config
                                  .WithKeys(ConfigurationKeys.TracerMetricsEnabled)
                                  .AsBool(defaultValue: false);

            StatsComputationInterval = config.WithKeys(ConfigurationKeys.StatsComputationInterval).AsInt32(defaultValue: 10);

            var otelRuntimeMetricsEnabled = config
                                          .WithKeys(ConfigurationKeys.OpenTelemetry.MetricsExporter)
                                          .AsBoolResult(
                                               value => string.Equals(value, "none", StringComparison.OrdinalIgnoreCase)
                                                            ? ParsingResult<bool>.Success(result: false)
                                                            : ParsingResult<bool>.Failure());
            RuntimeMetricsEnabled = config
                                   .WithKeys(ConfigurationKeys.RuntimeMetricsEnabled)
                                   .AsBoolResult()
                                   .OverrideWith(in otelRuntimeMetricsEnabled, ErrorLog, defaultValue: false);

            // We should also be writing telemetry for OTEL_LOGS_EXPORTER similar to OTEL_METRICS_EXPORTER, but we don't have a corresponding Datadog config
            // When we do, we can insert that here

            CustomSamplingRulesInternal = config.WithKeys(ConfigurationKeys.CustomSamplingRules).AsString();

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

            GlobalSamplingRateInternal = BuildSampleRate(ErrorLog, in config);

            // We need to record a default value for configuration reporting
            // However, we need to keep GlobalSamplingRateInternal null because it changes the behavior of the tracer in subtle ways
            // (= we don't run the sampler at all if it's null, so it changes the tagging of the spans, and it's enforced by system tests)
            if (GlobalSamplingRateInternal is null)
            {
                _telemetry.Record(ConfigurationKeys.GlobalSamplingRate, 1.0, ConfigurationOrigins.Default);
            }

            StartupDiagnosticLogEnabledInternal = config.WithKeys(ConfigurationKeys.StartupDiagnosticLogEnabled).AsBool(defaultValue: true);

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

            // If Lambda/GCP we don't want to have a flush interval. The serverless integration
            // manually calls flush and waits for the result before ending execution.
            // This can artificially increase the execution time of functions
            var defaultTraceBatchInterval = LambdaMetadata.IsRunningInLambda || IsRunningInGCPFunctions || IsRunningMiniAgentInAzureFunctions ? 0 : 100;
            TraceBatchInterval = config
                                .WithKeys(ConfigurationKeys.SerializationBatchInterval)
                                .AsInt32(defaultTraceBatchInterval);

            RouteTemplateResourceNamesEnabled = config
                                               .WithKeys(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled)
                                               .AsBool(defaultValue: true);

            ExpandRouteTemplatesEnabled = config
                                         .WithKeys(ConfigurationKeys.ExpandRouteTemplatesEnabled)
                                         .AsBool(defaultValue: !RouteTemplateResourceNamesEnabled); // disabled by default if route template resource names enabled

            KafkaCreateConsumerScopeEnabledInternal = config
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

            Func<string[], bool> injectionValidator = styles => styles is { Length: > 0 };
            Func<string, ParsingResult<string[]>> otelConverter =
                style => TrimSplitString(style, commaSeparator)
                        .Select(
                             s => string.Equals(s, "b3", StringComparison.OrdinalIgnoreCase)
                                      ? ContextPropagationHeaderStyle.B3SingleHeader // OTEL's "b3" maps to "b3 single header"
                                      : s)
                        .ToArray();

            var getDefaultPropagationHeaders = () => new DefaultResult<string[]>(
                [ContextPropagationHeaderStyle.Datadog, ContextPropagationHeaderStyle.W3CTraceContext],
                $"{ContextPropagationHeaderStyle.Datadog},{ContextPropagationHeaderStyle.W3CTraceContext}");

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

            // If Activity support is enabled, we shouldn't enable the W3C Trace Context propagators.
            if (!IsActivityListenerEnabled)
            {
                DisabledIntegrationNamesInternal.Add(nameof(IntegrationId.OpenTelemetry));
            }

            LogSubmissionSettings = new DirectLogSubmissionSettings(source, _telemetry);

            TraceMethods = config
                          .WithKeys(ConfigurationKeys.TraceMethods)
                          .AsString(string.Empty);

            // Filter out tags with empty keys or empty values, and trim whitespaces
            GrpcTagsInternal = InitializeHeaderTags(config, ConfigurationKeys.GrpcTags, headerTagsNormalizationFixEnabled: true)
                ?? new Dictionary<string, string>();

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

            IsDataStreamsMonitoringEnabled = config
                                            .WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled)
                                            .AsBool(false);

            IsRareSamplerEnabled = config
                                  .WithKeys(ConfigurationKeys.RareSamplerEnabled)
                                  .AsBool(false);

            StatsComputationEnabledInternal = config
                                     .WithKeys(ConfigurationKeys.StatsComputationEnabled)
                                     .AsBool(defaultValue: (IsRunningInGCPFunctions || IsRunningMiniAgentInAzureFunctions));
            if (AppsecStandaloneEnabledInternal && StatsComputationEnabledInternal)
            {
                telemetry.Record(ConfigurationKeys.StatsComputationEnabled, false, ConfigurationOrigins.Calculated);
                StatsComputationEnabledInternal = false;
            }

            var urlSubstringSkips = config
                                   .WithKeys(ConfigurationKeys.HttpClientExcludedUrlSubstrings)
                                   .AsString(
                                        IsRunningInAzureAppService ? ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions :
                                        LambdaMetadata is { IsRunningInLambda: true } m ? m.DefaultHttpClientExclusions : string.Empty);

            HttpClientExcludedUrlSubstrings = !string.IsNullOrEmpty(urlSubstringSkips)
                                                  ? TrimSplitString(urlSubstringSkips.ToUpperInvariant(), commaSeparator)
                                                  : Array.Empty<string>();

            DbmPropagationMode = config
                                .WithKeys(ConfigurationKeys.DbmPropagationMode)
                                .GetAs(
                                     () => new DefaultResult<DbmPropagationLevel>(DbmPropagationLevel.Disabled, nameof(DbmPropagationLevel.Disabled)),
                                     converter: x => ToDbmPropagationInput(x) ?? ParsingResult<DbmPropagationLevel>.Failure(),
                                     validator: null);

            TraceId128BitGenerationEnabled = config
                                            .WithKeys(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled)
                                            .AsBool(true);
            TraceId128BitLoggingEnabled = config
                                         .WithKeys(ConfigurationKeys.FeatureFlags.TraceId128BitLoggingEnabled)
                                         .AsBool(false);

            CommandsCollectionEnabled = config
                                       .WithKeys(ConfigurationKeys.FeatureFlags.CommandsCollectionEnabled)
                                       .AsBool(false);

            var defaultDisabledAdoNetCommandTypes = new string[] { "InterceptableDbCommand", "ProfiledDbCommand" };
            var userDisabledAdoNetCommandTypes = config.WithKeys(ConfigurationKeys.DisabledAdoNetCommandTypes).AsString();

            DisabledAdoNetCommandTypes = new HashSet<string>(defaultDisabledAdoNetCommandTypes, StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(userDisabledAdoNetCommandTypes))
            {
                var userSplit = TrimSplitString(userDisabledAdoNetCommandTypes, commaSeparator);
                DisabledAdoNetCommandTypes.UnionWith(userSplit);
            }

            // we "enrich" with these values which aren't _strictly_ configuration, but which we want to track as we tracked them in v1
            telemetry.Record(ConfigTelemetryData.NativeTracerVersion, Instrumentation.GetNativeTracerVersion(), recordValue: true, ConfigurationOrigins.Default);
            telemetry.Record(ConfigTelemetryData.FullTrustAppDomain, value: AppDomain.CurrentDomain.IsFullyTrusted, ConfigurationOrigins.Default);
            telemetry.Record(ConfigTelemetryData.ManagedTracerTfm, value: ConfigTelemetryData.ManagedTracerTfmValue, recordValue: true, ConfigurationOrigins.Default);

            // these are SSI variables that would be useful for correlation purposes
            telemetry.Record(ConfigTelemetryData.SsiInjectionEnabled, value: EnvironmentHelpers.GetEnvironmentVariable("DD_INJECTION_ENABLED"), recordValue: true, ConfigurationOrigins.EnvVars);
            telemetry.Record(ConfigTelemetryData.SsiAllowUnsupportedRuntimesEnabled, value: EnvironmentHelpers.GetEnvironmentVariable("DD_INJECT_FORCE"), recordValue: true, ConfigurationOrigins.EnvVars);

            if (AzureAppServiceMetadata is not null)
            {
                telemetry.Record(ConfigTelemetryData.AasConfigurationError, AzureAppServiceMetadata.IsUnsafeToTrace, ConfigurationOrigins.Default);
                telemetry.Record(ConfigTelemetryData.CloudHosting, "Azure", recordValue: true, ConfigurationOrigins.Default);
                telemetry.Record(ConfigTelemetryData.AasAppType, AzureAppServiceMetadata.SiteType, recordValue: true, ConfigurationOrigins.Default);
            }

            // Take a snapshot of the "original" settings, so that we can record any subsequent changes in code
            _initialSettings = new TracerSettingsSnapshot(this);
        }

        internal OverrideErrorLog ErrorLog { get; }

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_Environment_Get,
            PublicApiUsage.TracerSettings_Environment_Set)]
        [ConfigKey(ConfigurationKeys.Environment)]
        internal string? EnvironmentInternal { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_ServiceName_Get,
            PublicApiUsage.TracerSettings_ServiceName_Set)]
        [ConfigKey(ConfigurationKeys.ServiceName)]
        internal string? ServiceNameInternal { get; set; }

        /// <summary>
        /// Gets or sets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_ServiceVersion_Get,
            PublicApiUsage.TracerSettings_ServiceVersion_Set)]
        [ConfigKey(ConfigurationKeys.ServiceVersion)]
        internal string? ServiceVersionInternal { get; set; }
#pragma warning restore SA1624

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

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_TraceEnabled_Get,
            PublicApiUsage.TracerSettings_TraceEnabled_Set)]
        [ConfigKey(ConfigurationKeys.TraceEnabled)]
        internal bool TraceEnabledInternal { get; set; }

        /// <summary>
        /// Gets a value indicating whether Appsec standalone is enabled.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AppsecStandaloneEnabled"/>
        internal bool AppsecStandaloneEnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether profiling is enabled.
        /// Default is <c>false</c>.
        /// </summary>
        /// <seealso cref="ContinuousProfiler.ConfigurationKeys.ProfilingEnabled"/>
        internal bool ProfilingEnabledInternal { get; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_DisabledIntegrationNames_Get,
            PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set)]
        [ConfigKey(ConfigurationKeys.DisabledIntegrations)]
        internal HashSet<string> DisabledIntegrationNamesInternal { get; set; }

        /// <summary>
        /// Gets or sets the transport settings that dictate how the tracer connects to the agent.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_Exporter_Get,
            PublicApiUsage.TracerSettings_Exporter_Set)]
        [IgnoreForSnapshot] // We record this manually in the snapshot
        internal ExporterSettings ExporterInternal { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether default Analytics are enabled.
        /// Settings this value is a shortcut for setting
        /// <see cref="Configuration.IntegrationSettings.AnalyticsEnabled"/> on some predetermined integrations.
        /// See the documentation for more details.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalAnalyticsEnabled"/>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_AnalyticsEnabled_Get,
            PublicApiUsage.TracerSettings_AnalyticsEnabled_Set)]
#pragma warning disable CS0618 // ConfigurationKeys.GlobalAnalyticsEnabled is obsolete
        [ConfigKey(ConfigurationKeys.GlobalAnalyticsEnabled)]
#pragma warning restore CS0618 // ConfigurationKeys.GlobalAnalyticsEnabled is obsolete
        internal bool AnalyticsEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether correlation identifiers are
        /// automatically injected into the logging context.
        /// Default is <c>false</c>, unless <see cref="ConfigurationKeys.DirectLogSubmission.EnabledIntegrations"/>
        /// enables Direct Log Submission.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.LogsInjectionEnabled"/>
        [PublicApi]
        public bool LogsInjectionEnabled
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_LogsInjectionEnabled_Get);
                return LogSubmissionSettings.LogsInjectionEnabled;
            }

            set
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_LogsInjectionEnabled_Set);
                _telemetry.Record(ConfigurationKeys.LogsInjectionEnabled, value, ConfigurationOrigins.Code);
                LogSubmissionSettings.LogsInjectionEnabled = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating the maximum number of traces set to AutoKeep (p1) per second.
        /// Default is <c>100</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceRateLimit"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Get,
            PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set)]
#pragma warning disable CS0618
        [ConfigKey(ConfigurationKeys.TraceRateLimit)]
#pragma warning restore CS0618
        internal int MaxTracesSubmittedPerSecondInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_CustomSamplingRules_Get,
            PublicApiUsage.TracerSettings_CustomSamplingRules_Set)]
        [ConfigKey(ConfigurationKeys.CustomSamplingRules)]
        internal string? CustomSamplingRulesInternal { get; set; }

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
        /// Gets or sets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_GlobalSamplingRate_Get,
            PublicApiUsage.TracerSettings_GlobalSamplingRate_Set)]
        [ConfigKey(ConfigurationKeys.GlobalSamplingRate)]
        internal double? GlobalSamplingRateInternal { get; set; }

        /// <summary>
        /// Gets a collection of <see cref="IntegrationSettings"/> keyed by integration name.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.TracerSettings_Integrations_Get)]
        internal IntegrationSettingsCollection IntegrationsInternal { get; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all <see cref="Span"/>s.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_GlobalTags_Get,
            PublicApiUsage.TracerSettings_GlobalTags_Set)]
        [ConfigKey(ConfigurationKeys.GlobalTags)]
        internal IDictionary<string, string> GlobalTagsInternal { get; private set; }

        /// <summary>
        /// Gets or sets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing HTTP requests.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_HeaderTags_Get,
            PublicApiUsage.TracerSettings_HeaderTags_Set)]
        [ConfigKey(ConfigurationKeys.HeaderTags)]
        internal IDictionary<string, string> HeaderTagsInternal { get; set; }
#pragma warning restore SA1624

        /// <summary>
        /// Gets a custom request header configured to read the ip from. For backward compatibility, it fallbacks on DD_APPSEC_IPHEADER
        /// </summary>
        internal string? IpHeader { get; }

        /// <summary>
        /// Gets a value indicating whether the ip header should not be collected. The default is false.
        /// </summary>
        internal bool IpHeaderEnabled { get; }

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_GrpcTags_Get,
            PublicApiUsage.TracerSettings_GrpcTags_Set)]
        [ConfigKey(ConfigurationKeys.GrpcTags)]
        internal IDictionary<string, string> GrpcTagsInternal { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_TracerMetricsEnabled_Get,
            PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set)]
        [ConfigKey(ConfigurationKeys.TracerMetricsEnabled)]
        internal bool TracerMetricsEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether stats are computed on the tracer side
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_StatsComputationEnabled_Get,
            PublicApiUsage.TracerSettings_StatsComputationEnabled_Set)]
        [ConfigKey(ConfigurationKeys.StatsComputationEnabled)]
        internal bool StatsComputationEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the use
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

            set
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether a span context should be created on exiting a successful Kafka
        /// Consumer.Consume() call, and closed on entering Consumer.Consume().
        /// </summary>
        /// <seealso cref="ConfigurationKeys.KafkaCreateConsumerScopeEnabled"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Get,
            PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set)]
        [ConfigKey(ConfigurationKeys.KafkaCreateConsumerScopeEnabled)]
        internal bool KafkaCreateConsumerScopeEnabledInternal { get; set; }
#pragma warning restore SA1624

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

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Get,
            PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set)]
        [ConfigKey(ConfigurationKeys.StartupDiagnosticLogEnabled)]
        internal bool StartupDiagnosticLogEnabledInternal { get; set; }
#pragma warning restore SA1624

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
        /// Gets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled { get; }

        /// <summary>
        /// Gets the comma separated list of url patterns to skip tracing.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientExcludedUrlSubstrings"/>
        internal string[] HttpClientExcludedUrlSubstrings { get; }

        /// <summary>
        /// Gets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        [IgnoreForSnapshot] // Changes are recorded in SetHttpServerErrorStatusCodes
        internal bool[] HttpServerErrorStatusCodes { get; private set; }

        /// <summary>
        /// Gets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        [IgnoreForSnapshot] // Changes are recorded in SetHttpClientErrorStatusCodes
        internal bool[] HttpClientErrorStatusCodes { get; private set; }

        /// <summary>
        /// Gets configuration values for changing service names based on configuration
        /// </summary>
        [IgnoreForSnapshot] // Changes are recorded in SetServiceNameMappings
        internal IDictionary<string, string>? ServiceNameMappings { get; private set; }

        /// <summary>
        /// Gets configuration values for changing peer service names based on configuration
        /// </summary>
        internal IDictionary<string, string>? PeerServiceNameMappings { get; }

        /// <summary>
        /// Gets a value indicating the size in bytes of the trace buffer
        /// </summary>
        internal int TraceBufferSize { get; }

        /// <summary>
        /// Gets a value indicating the batch interval for the serialization queue, in milliseconds
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
        internal bool IsDataStreamsMonitoringEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the rare sampler is enabled or not.
        /// </summary>
        internal bool IsRareSamplerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in AAS
        /// </summary>
        internal bool IsRunningInAzureAppService { get; }

        /// <summary>
        /// Gets a value indicating whether the tracer is running in an Azure Function on a
        /// consumption plan
        /// </summary>
        internal bool IsRunningMiniAgentInAzureFunctions { get; }

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
        /// Gets the metadata schema version
        /// </summary>
        internal SchemaVersion MetadataSchemaVersion { get; }

        /// <summary>
        /// Gets the disabled ADO.NET Command Types that won't have spans generated for them.
        /// </summary>
        internal HashSet<string> DisabledAdoNetCommandTypes { get; }

        /// <summary>
        /// Create a <see cref="TracerSettings"/> populated from the default sources
        /// returned by <see cref="GlobalConfigurationSource.Instance"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        [PublicApi]
        public static TracerSettings FromDefaultSources()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_FromDefaultSources);
            return FromDefaultSourcesInternal();
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        [PublicApi]
        public static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_CreateDefaultConfigurationSource);
            return GlobalConfigurationSource.CreateDefaultConfigurationSource();
        }

        internal static TracerSettings FromDefaultSourcesInternal()
            => new(GlobalConfigurationSource.Instance, new ConfigurationTelemetry(), new());

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        [PublicApi]
        public void SetHttpClientErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpClientErrorStatusCodes);
            SetHttpClientErrorStatusCodesInternal(statusCodes);
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        [PublicApi]
        public void SetHttpServerErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetHttpServerErrorStatusCodes);
            SetHttpServerErrorStatusCodesInternal(statusCodes);
        }

        /// <summary>
        /// Sets the mappings to use for service names within a <see cref="Span"/>
        /// </summary>
        /// <param name="mappings">Mappings to use from original service name (e.g. <code>sql-server</code> or <code>graphql</code>)
        /// as the <see cref="KeyValuePair{TKey, TValue}.Key"/>) to replacement service names as <see cref="KeyValuePair{TKey, TValue}.Value"/>).</param>
        [PublicApi]
        public void SetServiceNameMappings(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_SetServiceNameMappings);
            // Could optimise this to remove allocations/linq, but leave that for later if we find it's used a lot
            var dictionary = mappings.ToDictionary(x => x.Key, x => x.Value);
            SetServiceNameMappingsInternal(dictionary);
        }

        /// <summary>
        /// Create an instance of <see cref="ImmutableTracerSettings"/> that can be used to build a <see cref="Tracer"/>
        /// </summary>
        /// <returns>The <see cref="ImmutableTracerSettings"/> that can be passed to a <see cref="Tracer"/> instance</returns>
        [PublicApi]
        public ImmutableTracerSettings Build()
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.TracerSettings_Build);
            return new ImmutableTracerSettings(this, true);
        }

        internal static IDictionary<string, string>? InitializeServiceNameMappings(ConfigurationBuilder config, string key)
        {
            return config
               .WithKeys(key)
               .AsDictionary()
              ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
               .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());
        }

        internal static IDictionary<string, string>? InitializeHeaderTags(ConfigurationBuilder config, string key, bool headerTagsNormalizationFixEnabled)
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

            return headerTags;
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
                    list.Add(value.Trim());
                }
            }

            return list.ToArray();
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

        internal static DbmPropagationLevel? ToDbmPropagationInput(string inputValue)
        {
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
            => new(new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => x.Value?.ToString()!)), new ConfigurationTelemetry(), new());

        internal void SetHttpClientErrorStatusCodesInternal(IEnumerable<int> statusCodes)
        {
            var httpStatusErrorCodes = string.Join(",", statusCodes);
            _telemetry.Record(ConfigurationKeys.HttpClientErrorStatusCodes, httpStatusErrorCodes, recordValue: true, origin: ConfigurationOrigins.Code);
            HttpClientErrorStatusCodes = ParseHttpCodesToArray(httpStatusErrorCodes);
        }

        internal void SetHttpServerErrorStatusCodesInternal(IEnumerable<int> statusCodes)
        {
            var httpStatusErrorCodes = string.Join(",", statusCodes);
            _telemetry.Record(ConfigurationKeys.HttpServerErrorStatusCodes, httpStatusErrorCodes, recordValue: true, origin: ConfigurationOrigins.Code);
            HttpServerErrorStatusCodes = ParseHttpCodesToArray(httpStatusErrorCodes);
        }

        internal void SetServiceNameMappingsInternal(Dictionary<string, string> dictionary)
        {
            _telemetry.Record(
                ConfigurationKeys.ServiceNameMappings,
                string.Join("'", dictionary.Select(kvp => $"{kvp.Key}:{kvp.Value}")),
                recordValue: true,
                origin: ConfigurationOrigins.Code);

            ServiceNameMappings = dictionary;
        }

        internal void CollectTelemetry(IConfigurationTelemetry destination)
        {
            // copy the current settings into telemetry
            _telemetry.CopyTo(destination);

            // record changes made in code directly to destination
            _initialSettings.RecordChanges(this, destination);

            // If ExporterSettings has been replaced, it will have its own telemetry collector
            // so we need to record those values too.
            if (ExporterInternal.Telemetry is { } exporterTelemetry
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
    }
}

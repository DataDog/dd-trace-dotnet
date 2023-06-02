// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Schema;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.Propagators;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public partial class TracerSettings
    {
        private readonly IConfigurationTelemetry _telemetry;

        /// <summary>
        /// Default obfuscation query string regex if none specified via env DD_OBFUSCATION_QUERY_STRING_REGEXP
        /// </summary>
        internal const string DefaultObfuscationQueryStringRegex = @"((?i)(?:p(?:ass)?w(?:or)?d|pass(?:_?phrase)?|secret|(?:api_?|private_?|public_?|access_?|secret_?)key(?:_?id)?|token|consumer_?(?:id|key|secret)|sign(?:ed|ature)?|auth(?:entication|orization)?)(?:(?:\s|%20)*(?:=|%3D)[^&]+|(?:""|%22)(?:\s|%20)*(?::|%3A)(?:\s|%20)*(?:""|%22)(?:%2[^2]|%[^2]|[^""%])+(?:""|%22))|bearer(?:\s|%20)+[a-z0-9\._\-]|token(?::|%3A)[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L](?:[\w=-]|%3D)+\.ey[I-L](?:[\w=-]|%3D)+(?:\.(?:[\w.+\/=-]|%3D|%2F|%2B)+)?|[\-]{5}BEGIN(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY[\-]{5}[^\-]+[\-]{5}END(?:[a-z\s]|%20)+PRIVATE(?:\s|%20)KEY|ssh-rsa(?:\s|%20)*(?:[a-z0-9\/\.+]|%2F|%5C|%2B){100,})";

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values.
        /// </summary>
        public TracerSettings()
            : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class with default values,
        /// or initializes the configuration from environment variables and configuration files.
        /// Calling <c>new TracerSettings(true)</c> is equivalent to calling <c>TracerSettings.FromDefaultSources()</c>
        /// </summary>
        /// <param name="useDefaultSources">If <c>true</c>, creates a <see cref="TracerSettings"/> populated from
        /// the default sources such as environment variables etc. If <c>false</c>, uses the default values.</param>
        public TracerSettings(bool useDefaultSources)
            : this(useDefaultSources ? GlobalConfigurationSource.Instance : null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class
        /// using the specified <see cref="IConfigurationSource"/> to initialize values.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <remarks>
        /// We deliberately don't use the static <see cref="TelemetryFactory.Config"/> collector here
        /// as we don't want to automatically record these values, only once they're "activated",
        /// in <see cref="Tracer.Configure"/>
        /// </remarks>
        public TracerSettings(IConfigurationSource? source)
        : this(source, new ConfigurationTelemetry())
        {
        }

        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            var commaSeparator = new[] { ',' };
            source ??= NullConfigurationSource.Instance;
            _telemetry = telemetry;
            var config = new ConfigurationBuilder(source, _telemetry);

            EnvironmentInternal = config
                         .WithKeys(ConfigurationKeys.Environment)
                         .AsString();

            ServiceNameInternal = config
                         .WithKeys(ConfigurationKeys.ServiceName, "DD_SERVICE_NAME")
                         .AsString();

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

            TraceEnabledInternal = config
                          .WithKeys(ConfigurationKeys.TraceEnabled)
                          .AsBool(defaultValue: true);

            var disabledIntegrationNames = config.WithKeys(ConfigurationKeys.DisabledIntegrations)
                                                               .AsString()
                                                              ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                           Enumerable.Empty<string>();

            DisabledIntegrationNamesInternal = new HashSet<string>(disabledIntegrationNames, StringComparer.OrdinalIgnoreCase);

            IntegrationsInternal = new IntegrationSettingsCollection(source, _telemetry);

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

            GlobalTagsInternal = config
                         // backwards compatibility for names used in the past
                        .WithKeys(ConfigurationKeys.GlobalTags, "DD_TRACE_GLOBAL_TAGS")
                        .AsDictionary()
                         // Filter out tags with empty keys or empty values, and trim whitespace
                       ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                        .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim())
                         // default value (empty)
                      ?? (IDictionary<string, string>)new ConcurrentDictionary<string, string>();

            var inputHeaderTags = config
                                 .WithKeys(ConfigurationKeys.HeaderTags)
                                 .AsDictionary(allowOptionalMappings: true) ??
                                  // default value (empty)
                                  new Dictionary<string, string>();

            var headerTagsNormalizationFixEnabled = config
                                                   .WithKeys(ConfigurationKeys.FeatureFlags.HeaderTagsNormalizationFixEnabled)
                                                   .AsBool(defaultValue: true);

            // Filter out tags with empty keys or empty values, and trim whitespaces
            HeaderTagsInternal = InitializeHeaderTags(inputHeaderTags, headerTagsNormalizationFixEnabled);
            MetadataSchemaVersion = config
                                   .WithKeys(ConfigurationKeys.MetadataSchemaVersion)
                                   .GetAs(
                                        () => SchemaVersion.V0,
                                        converter: x => x switch
                                        {
                                            "v1" or "V1" => SchemaVersion.V1,
                                            "v0" or "V0" => SchemaVersion.V0,
                                            _ => ParsingResult<SchemaVersion>.Failure(),
                                        },
                                        validator: null);

            ServiceNameMappings = config
                                     .WithKeys(ConfigurationKeys.ServiceNameMappings)
                                     .AsDictionary()
                                    ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                    .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            TracerMetricsEnabledInternal = config
                                  .WithKeys(ConfigurationKeys.TracerMetricsEnabled)
                                  .AsBool(defaultValue: false);

            StatsComputationEnabledInternal = config
                                     .WithKeys(ConfigurationKeys.StatsComputationEnabled)
                                     .AsBool(defaultValue: false);

            StatsComputationInterval = config.WithKeys(ConfigurationKeys.StatsComputationInterval).AsInt32(defaultValue: 10);

            RuntimeMetricsEnabled = config.WithKeys(ConfigurationKeys.RuntimeMetricsEnabled).AsBool(defaultValue: false);

            CustomSamplingRulesInternal = config.WithKeys(ConfigurationKeys.CustomSamplingRules).AsString();

            SpanSamplingRules = config.WithKeys(ConfigurationKeys.SpanSamplingRules).AsString();

            GlobalSamplingRateInternal = config.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDouble();

            StartupDiagnosticLogEnabledInternal = config.WithKeys(ConfigurationKeys.StartupDiagnosticLogEnabled).AsBool(defaultValue: true);

            var httpServerErrorStatusCodes = config
                                            .WithKeys(ConfigurationKeys.HttpServerErrorStatusCodes)
                                            .AsString(defaultValue: "500-599");

            HttpServerErrorStatusCodes = ParseHttpCodesToArray(httpServerErrorStatusCodes);

            var httpClientErrorStatusCodes = config
                                            .WithKeys(ConfigurationKeys.HttpClientErrorStatusCodes)
                                            .AsString(defaultValue: "400-499");

            HttpClientErrorStatusCodes = ParseHttpCodesToArray(httpClientErrorStatusCodes);

            TraceBufferSize = config
                             .WithKeys(ConfigurationKeys.BufferSize)
                             .AsInt32(defaultValue: 1024 * 1024 * 10); // 10MB

            TraceBatchInterval = config
                                .WithKeys(ConfigurationKeys.SerializationBatchInterval)
                                .AsInt32(defaultValue: 100);

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
                                            .AsBool(defaultValue: false);

            WcfObfuscationEnabled = config
                                   .WithKeys(ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled)
                                   .AsBool(defaultValue: true);

            ObfuscationQueryStringRegex = config
                                         .WithKeys(ConfigurationKeys.ObfuscationQueryStringRegex)
                                         .AsString(defaultValue: DefaultObfuscationQueryStringRegex);

            QueryStringReportingEnabled = config
                                         .WithKeys(ConfigurationKeys.QueryStringReportingEnabled)
                                         .AsBool(defaultValue: true);

            QueryStringReportingSize = config
                                      .WithKeys(ConfigurationKeys.QueryStringReportingSize)
                                      .AsInt32(defaultValue: 5000); // 5000 being the tag value length limit

            ObfuscationQueryStringRegexTimeout = config
                                                .WithKeys(ConfigurationKeys.ObfuscationQueryStringRegexTimeout)
                                                .AsDouble(200, val1 => val1 is > 0).Value;

            IsActivityListenerEnabled = config
                                       .WithKeys(ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled, "DD_TRACE_ACTIVITY_LISTENER_ENABLED")
                                       .AsBool(false);

            var propagationStyleInject = config
                                        .WithKeys(ConfigurationKeys.PropagationStyleInject, "DD_PROPAGATION_STYLE_INJECT", ConfigurationKeys.PropagationStyle)
                                        .AsString();

            PropagationStyleInject = TrimSplitString(propagationStyleInject, commaSeparator);

            if (PropagationStyleInject.Length == 0)
            {
                // default value
                PropagationStyleInject = new[] { ContextPropagationHeaderStyle.W3CTraceContext, ContextPropagationHeaderStyle.Datadog };
            }

            var propagationStyleExtract = config
                                         .WithKeys(ConfigurationKeys.PropagationStyleExtract, "DD_PROPAGATION_STYLE_EXTRACT", ConfigurationKeys.PropagationStyle)
                                         .AsString();

            PropagationStyleExtract = TrimSplitString(propagationStyleExtract, commaSeparator);

            if (PropagationStyleExtract.Length == 0)
            {
                // default value
                PropagationStyleExtract = new[] { ContextPropagationHeaderStyle.W3CTraceContext, ContextPropagationHeaderStyle.Datadog };
            }

            // If Activity support is enabled, we must enable the W3C Trace Context propagators.
            // It's ok to include W3C multiple times, we handle that later.
            if (IsActivityListenerEnabled)
            {
                PropagationStyleInject = PropagationStyleInject.Concat(ContextPropagationHeaderStyle.W3CTraceContext);
                PropagationStyleExtract = PropagationStyleExtract.Concat(ContextPropagationHeaderStyle.W3CTraceContext);
            }
            else
            {
                DisabledIntegrationNamesInternal.Add(nameof(Configuration.IntegrationId.OpenTelemetry));
            }

            LogSubmissionSettings = new DirectLogSubmissionSettings(source, _telemetry);

            TraceMethods = config
                          .WithKeys(ConfigurationKeys.TraceMethods)
                          .AsString(string.Empty);

            var grpcTags = config
                          .WithKeys(ConfigurationKeys.GrpcTags)
                          .AsDictionary(allowOptionalMappings: true)
                           // default value (empty)
                        ?? new Dictionary<string, string>();

            // Filter out tags with empty keys or empty values, and trim whitespaces
            GrpcTagsInternal = InitializeHeaderTags(grpcTags, headerTagsNormalizationFixEnabled: true);

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

            IsRunningInAzureAppService = config
                                        .WithKeys(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey)
                                        .AsBool(false);
            if (IsRunningInAzureAppService)
            {
                AzureAppServiceMetadata = new ImmutableAzureAppServiceSettings(source, _telemetry);
                if (AzureAppServiceMetadata.IsUnsafeToTrace)
                {
                    TraceEnabledInternal = false;
                }
            }

            var urlSubstringSkips = config
                                   .WithKeys(ConfigurationKeys.HttpClientExcludedUrlSubstrings)
                                   .AsString(
                                        IsRunningInAzureAppService ? ImmutableAzureAppServiceSettings.DefaultHttpClientExclusions :
                                        Serverless.Metadata is { IsRunningInLambda: true } m ? m.DefaultHttpClientExclusions : string.Empty);

            HttpClientExcludedUrlSubstrings = !string.IsNullOrEmpty(urlSubstringSkips)
                                                  ? TrimSplitString(urlSubstringSkips.ToUpperInvariant(), commaSeparator)
                                                  : Array.Empty<string>();

            DbmPropagationMode = config
                                .WithKeys(ConfigurationKeys.DbmPropagationMode)
                                .GetAs(
                                     () => DbmPropagationLevel.Disabled,
                                     converter: x => ToDbmPropagationInput(x) ?? ParsingResult<DbmPropagationLevel>.Failure(),
                                     validator: null);

            TraceId128BitGenerationEnabled = config
                                            .WithKeys(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled)
                                            .AsBool(false);
            TraceId128BitLoggingEnabled = config
                                         .WithKeys(ConfigurationKeys.FeatureFlags.TraceId128BitLoggingEnabled)
                                         .AsBool(false);

            // we "enrich" with these values which aren't _strictly_ configuration, but which we want to track as we tracked them in v1
            telemetry.Record(ConfigTelemetryData.NativeTracerVersion, Instrumentation.GetNativeTracerVersion(), recordValue: true, ConfigurationOrigins.Default);
            telemetry.Record(ConfigTelemetryData.FullTrustAppDomain, value: AppDomain.CurrentDomain.IsFullyTrusted, ConfigurationOrigins.Default);

            if (AzureAppServiceMetadata is not null)
            {
                telemetry.Record(ConfigTelemetryData.AasConfigurationError, AzureAppServiceMetadata.IsUnsafeToTrace, ConfigurationOrigins.Default);
                telemetry.Record(ConfigTelemetryData.CloudHosting, "Azure", recordValue: true, ConfigurationOrigins.Default);
                telemetry.Record(ConfigTelemetryData.AasAppType, AzureAppServiceMetadata.SiteType, recordValue: true, ConfigurationOrigins.Default);
            }
        }

        /// <summary>
        /// Gets or sets the default environment name applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Environment"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_Environment_Get,
            PublicApiUsage.TracerSettings_Environment_Set,
            ConfigurationKeys.Environment)]
        internal string? EnvironmentInternal { get; set; }

        /// <summary>
        /// Gets or sets the service name applied to top-level spans and used to build derived service names.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceName"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_ServiceName_Get,
            PublicApiUsage.TracerSettings_ServiceName_Set,
            ConfigurationKeys.ServiceName)]
        internal string? ServiceNameInternal { get; set; }

        /// <summary>
        /// Gets or sets the version tag applied to all spans.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ServiceVersion"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_ServiceVersion_Get,
            PublicApiUsage.TracerSettings_ServiceVersion_Set,
            ConfigurationKeys.ServiceVersion)]
        internal string? ServiceVersionInternal { get; set; }

        /// <summary>
        /// Gets or sets the application's git repository url.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitRepositoryUrl"/>
        internal string? GitRepositoryUrl { get; set; }

        /// <summary>
        /// Gets or sets the application's git commit hash.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitCommitSha"/>
        internal string? GitCommitSha { get; set; }

        /// <summary>
        /// Gets a value indicating whether we should tag every telemetry event with git metadata.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitMetadataEnabled"/>
        internal bool GitMetadataEnabled { get; }

        /// <summary>
        /// Gets or sets a value indicating whether tracing is enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TraceEnabled"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_TraceEnabled_Get,
            PublicApiUsage.TracerSettings_TraceEnabled_Set,
            ConfigurationKeys.TraceEnabled)]
        internal bool TraceEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets the names of disabled integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledIntegrations"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_DisabledIntegrationNames_Get,
            PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set,
            ConfigurationKeys.DisabledIntegrations)]
        internal HashSet<string> DisabledIntegrationNamesInternal { get; set; }

        /// <summary>
        /// Gets or sets the transport settings that dictate how the tracer connects to the agent.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_Exporter_Get,
            PublicApiUsage.TracerSettings_Exporter_Set)]
        internal ExporterSettings ExporterInternal { get; set; }

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
            PublicApiUsage.TracerSettings_AnalyticsEnabled_Set,
#pragma warning disable 618 // App analytics is deprecated, but still used
            ConfigurationKeys.GlobalAnalyticsEnabled)]
#pragma warning restore 618
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
                return LogSubmissionSettings.LogsInjectionEnabled ?? false;
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
            PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set,
            ConfigurationKeys.TraceRateLimit)]
        internal int MaxTracesSubmittedPerSecondInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating custom sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.CustomSamplingRules"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_CustomSamplingRules_Get,
            PublicApiUsage.TracerSettings_CustomSamplingRules_Set,
            ConfigurationKeys.CustomSamplingRules)]
        internal string? CustomSamplingRulesInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating span sampling rules.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.SpanSamplingRules"/>
        internal string? SpanSamplingRules { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a global rate for sampling.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GlobalSamplingRate"/>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_GlobalSamplingRate_Get,
            PublicApiUsage.TracerSettings_GlobalSamplingRate_Set,
            ConfigurationKeys.GlobalSamplingRate)]
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
            PublicApiUsage.TracerSettings_GlobalTags_Set,
            ConfigurationKeys.GlobalTags)]
        internal IDictionary<string, string> GlobalTagsInternal { get; set; }

        /// <summary>
        /// Gets or sets the map of header keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing HTTP requests.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_HeaderTags_Get,
            PublicApiUsage.TracerSettings_HeaderTags_Set,
            ConfigurationKeys.HeaderTags)]
        internal IDictionary<string, string> HeaderTagsInternal { get; set; }

        /// <summary>
        /// Gets or sets a custom request header configured to read the ip from. For backward compatibility, it fallbacks on DD_APPSEC_IPHEADER
        /// </summary>
        internal string? IpHeader { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the ip header should not be collected. The default is false.
        /// </summary>
        internal bool IpHeaderEnabled { get; set; }

        /// <summary>
        /// Gets or sets the map of metadata keys to tag names, which are applied to the root <see cref="Span"/>
        /// of incoming and outgoing GRPC requests.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_GrpcTags_Get,
            PublicApiUsage.TracerSettings_GrpcTags_Set,
            ConfigurationKeys.GrpcTags)]
        internal IDictionary<string, string> GrpcTagsInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether internal metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_TracerMetricsEnabled_Get,
            PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set,
            ConfigurationKeys.TracerMetricsEnabled)]
        internal bool TracerMetricsEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether stats are computed on the tracer side
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_StatsComputationEnabled_Get,
            PublicApiUsage.TracerSettings_StatsComputationEnabled_Set,
            ConfigurationKeys.StatsComputationEnabled)]
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
            PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set,
            ConfigurationKeys.KafkaCreateConsumerScopeEnabled)]
        internal bool KafkaCreateConsumerScopeEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the updated WCF instrumentation that delays execution
        /// until later in the WCF pipeline when the WCF server exception handling is established.
        /// </summary>
        internal bool DelayWcfInstrumentationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to obfuscate the <c>LocalPath</c> of a WCF request that goes
        /// into the <c>resourceName</c> of a span.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.WcfObfuscationEnabled"/>
        internal bool WcfObfuscationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the regex to apply to obfuscate http query strings.
        /// </summary>
        internal string ObfuscationQueryStringRegex { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether or not http.url should contain the query string, enabled by default
        /// </summary>
        internal bool QueryStringReportingEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value limiting the size of the querystring to report and obfuscate
        /// Default value is 5000, 0 means that we don't limit the size.
        /// </summary>
        internal int QueryStringReportingSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating a timeout in milliseconds to the execution of the query string obfuscation regex
        /// Default value is 100ms
        /// </summary>
        internal double ObfuscationQueryStringRegexTimeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the diagnostic log at startup is enabled
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Get,
            PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set,
            ConfigurationKeys.StartupDiagnosticLogEnabled)]
        internal bool StartupDiagnosticLogEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets the time interval (in seconds) for sending stats
        /// </summary>
        internal int StatsComputationInterval { get; set; }

        /// <summary>
        /// Gets or sets the maximum length of an outgoing propagation header's value ("x-datadog-tags")
        /// when injecting it into downstream service calls.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.TagPropagation.HeaderMaxLength"/>
        /// <remarks>
        /// This value is not used when extracting an incoming propagation header from an upstream service.
        /// </remarks>
        internal int OutgoingTagPropagationHeaderMaxLength { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the injection propagation style.
        /// </summary>
        internal string[] PropagationStyleInject { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the extraction propagation style.
        /// </summary>
        internal string[] PropagationStyleExtract { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled { get; set; }

        /// <summary>
        /// Gets or sets the comma separated list of url patterns to skip tracing.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientExcludedUrlSubstrings"/>
        internal string[] HttpClientExcludedUrlSubstrings { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        internal bool[] HttpServerErrorStatusCodes { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        internal bool[] HttpClientErrorStatusCodes { get; set; }

        /// <summary>
        /// Gets or sets configuration values for changing service names based on configuration
        /// </summary>
        internal IDictionary<string, string>? ServiceNameMappings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the size in bytes of the trace buffer
        /// </summary>
        internal int TraceBufferSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the batch interval for the serialization queue, in milliseconds
        /// </summary>
        internal int TraceBatchInterval { get; set; }

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
        /// Gets or sets the direct log submission settings.
        /// </summary>
        internal DirectLogSubmissionSettings LogSubmissionSettings { get; set; }

        /// <summary>
        /// Gets or sets a value indicating the trace methods configuration.
        /// </summary>
        internal string TraceMethods { get; set; }

        /// <summary>
        /// Gets a value indicating whether the activity listener is enabled or not.
        /// </summary>
        internal bool IsActivityListenerEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether data streams monitoring is enabled or not.
        /// </summary>
        internal bool IsDataStreamsMonitoringEnabled { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the rare sampler is enabled or not.
        /// </summary>
        internal bool IsRareSamplerEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tracer is running in AAS
        /// </summary>
        internal bool IsRunningInAzureAppService { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tracer should propagate service data in db queries
        /// </summary>
        internal DbmPropagationLevel DbmPropagationMode { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tracer will generate 128-bit trace ids
        /// instead of 64-bits trace ids.
        /// </summary>
        internal bool TraceId128BitGenerationEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tracer will inject 128-bit trace ids into logs, if available,
        /// instead of 64-bit trace ids. Note that a 128-bit trace id may be received from an upstream service
        /// even if we are not generating them.
        /// </summary>
        internal bool TraceId128BitLoggingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the AAS settings
        /// </summary>
        internal ImmutableAzureAppServiceSettings? AzureAppServiceMetadata { get; set; }

        /// <summary>
        /// Gets or sets the metadata schema version
        /// </summary>
        internal SchemaVersion MetadataSchemaVersion { get; set; }

        /// <summary>
        /// Create a <see cref="TracerSettings"/> populated from the default sources
        /// returned by <see cref="GlobalConfigurationSource.Instance"/>.
        /// </summary>
        /// <returns>A <see cref="TracerSettings"/> populated from the default sources.</returns>
        public static TracerSettings FromDefaultSources()
        {
            return new TracerSettings(GlobalConfigurationSource.Instance);
        }

        /// <summary>
        /// Creates a <see cref="IConfigurationSource"/> by combining environment variables,
        /// AppSettings where available, and a local datadog.json file, if present.
        /// </summary>
        /// <returns>A new <see cref="IConfigurationSource"/> instance.</returns>
        public static CompositeConfigurationSource CreateDefaultConfigurationSource()
        {
            return GlobalConfigurationSource.CreateDefaultConfigurationSource();
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for client integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpClientErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        public void SetHttpClientErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            HttpClientErrorStatusCodes = ParseHttpCodesToArray(string.Join(",", statusCodes));
        }

        /// <summary>
        /// Sets the HTTP status code that should be marked as errors for server integrations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.HttpServerErrorStatusCodes"/>
        /// <param name="statusCodes">Status codes that should be marked as errors</param>
        public void SetHttpServerErrorStatusCodes(IEnumerable<int> statusCodes)
        {
            HttpServerErrorStatusCodes = ParseHttpCodesToArray(string.Join(",", statusCodes));
        }

        /// <summary>
        /// Sets the mappings to use for service names within a <see cref="Span"/>
        /// </summary>
        /// <param name="mappings">Mappings to use from original service name (e.g. <code>sql-server</code> or <code>graphql</code>)
        /// as the <see cref="KeyValuePair{TKey, TValue}.Key"/>) to replacement service names as <see cref="KeyValuePair{TKey, TValue}.Value"/>).</param>
        public void SetServiceNameMappings(IEnumerable<KeyValuePair<string, string>> mappings)
        {
            ServiceNameMappings = mappings.ToDictionary(x => x.Key, x => x.Value);
        }

        /// <summary>
        /// Create an instance of <see cref="ImmutableTracerSettings"/> that can be used to build a <see cref="Tracer"/>
        /// </summary>
        /// <returns>The <see cref="ImmutableTracerSettings"/> that can be passed to a <see cref="Tracer"/> instance</returns>
        public ImmutableTracerSettings Build()
        {
            return new ImmutableTracerSettings(this);
        }

        internal void CollectTelemetry()
        {
            TelemetryFactory.Config.Merge(_telemetry);
            ExporterInternal.CollectTelemetry();
        }

        private static IDictionary<string, string> InitializeHeaderTags(IDictionary<string, string> configurationDictionary, bool headerTagsNormalizationFixEnabled)
        {
            var headerTags = new Dictionary<string, string>();

            foreach (var kvp in configurationDictionary)
            {
                var headerName = kvp.Key;
                var providedTagName = kvp.Value;
                if (string.IsNullOrWhiteSpace(headerName))
                {
                    continue;
                }

                // The user has not provided a tag name. The normalization will happen later, when adding the prefix.
                if (string.IsNullOrEmpty(providedTagName))
                {
                    headerTags.Add(headerName.Trim(), string.Empty);
                }
                else if (headerTagsNormalizationFixEnabled && providedTagName.TryConvertToNormalizedTagName(normalizePeriods: false, out var normalizedTagName))
                {
                    // If the user has provided a tag name, then we don't normalize periods in the provided tag name
                    headerTags.Add(headerName.Trim(), normalizedTagName);
                }
                else if (!headerTagsNormalizationFixEnabled && providedTagName.TryConvertToNormalizedTagName(normalizePeriods: true, out var normalizedTagNameNoPeriods))
                {
                    // Back to the previous behaviour if the flag is set
                    headerTags.Add(headerName.Trim(), normalizedTagNameNoPeriods);
                }
            }

            return headerTags;
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
                    Log.Warning("Wrong format '{0}' for DD_HTTP_SERVER/CLIENT_ERROR_STATUSES configuration.", statusConfiguration);
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
    }
}

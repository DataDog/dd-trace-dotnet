// <copyright file="TracerSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.LibDatadog;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains Tracer settings.
    /// </summary>
    public partial record TracerSettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TracerSettings>();
        private static readonly HashSet<string> DefaultExperimentalFeatures = ["DD_TAGS", ConfigurationKeys.PropagateProcessTags];

        private readonly Lazy<string> _fallbackApplicationName;

        [TestingOnly]
        internal TracerSettings()
            : this(null, new ConfigurationTelemetry(), new OverrideErrorLog())
        {
        }

        [TestingOnly]
        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry? telemetry = null)
        : this(source, telemetry ?? new ConfigurationTelemetry(), new OverrideErrorLog())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracerSettings"/> class.
        /// The "main" constructor for <see cref="TracerSettings"/> that should be used internally in the library.
        /// </summary>
        /// <param name="source">The configuration source. If <c>null</c> is provided, uses <see cref="NullConfigurationSource"/> </param>
        /// <param name="telemetry">The telemetry collection instance. Typically you should create a new <see cref="ConfigurationTelemetry"/> </param>
        /// <param name="errorLog">Used to record cases where telemetry is overridden </param>
        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, OverrideErrorLog errorLog)
            : this(source, telemetry, errorLog, LibDatadogAvailabilityHelper.IsLibDatadogAvailable)
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
        internal TracerSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, OverrideErrorLog errorLog, LibDatadogAvailableResult isLibDatadogAvailable)
        {
            var commaSeparator = new[] { ',' };
            source ??= NullConfigurationSource.Instance;
            ErrorLog = errorLog;
            var config = new ConfigurationBuilder(source, telemetry);

            ExperimentalFeaturesEnabled = config
                    .WithKeys(ConfigurationKeys.ExperimentalFeaturesEnabled)
                    .AsString()?.Trim() switch
                    {
                        null or "none" => new HashSet<string>(),
                        "all" => DefaultExperimentalFeatures,
                        string s => new HashSet<string>(s.Split([','], StringSplitOptions.RemoveEmptyEntries)),
                    };

            PropagateProcessTags = config
                                       .WithKeys(ConfigurationKeys.PropagateProcessTags)
                                       .AsBool(ExperimentalFeaturesEnabled.Contains(ConfigurationKeys.PropagateProcessTags)); // read it as "defaults to false"

            GCPFunctionSettings = new ImmutableGCPFunctionSettings(source, telemetry);
            IsRunningInGCPFunctions = GCPFunctionSettings.IsGCPFunction;

            // We don't want/need to record this value, so explicitly use null telemetry
            IsRunningInCiVisibility = new ConfigurationBuilder(source, NullConfigurationTelemetry.Instance)
                                     .WithKeys(ConfigurationKeys.CIVisibility.IsRunningInCiVisMode)
                                     .AsBool(false);

            LambdaMetadata = LambdaMetadata.Create();

            if (ImmutableAzureAppServiceSettings.IsRunningInAzureAppServices(source, telemetry))
            {
                AzureAppServiceMetadata = new ImmutableAzureAppServiceSettings(source, telemetry);
            }

            GitMetadataEnabled = config
                                .WithKeys(ConfigurationKeys.GitMetadataEnabled)
                                .AsBool(defaultValue: true);

            ApmTracingEnabled = config
                                      .WithKeys(ConfigurationKeys.ApmTracingEnabled)
                                      .AsBool(defaultValue: true);

            var otelActivityListenerEnabled = config
                                             .WithKeys(ConfigurationKeys.OpenTelemetry.SdkDisabled)
                                             .AsBoolResult(
                                                  value => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                                                               ? ParsingResult<bool>.Success(result: false)
                                                               : ParsingResult<bool>.Failure());
            IsActivityListenerEnabled = config
                                       .WithKeys(ConfigurationKeys.FeatureFlags.OpenTelemetryEnabled)
                                       .AsBoolResult()
                                       .OverrideWith(in otelActivityListenerEnabled, ErrorLog, defaultValue: false);

            PeerServiceTagsEnabled = config
               .WithKeys(ConfigurationKeys.PeerServiceDefaultsEnabled)
               .AsBool(defaultValue: false);
            RemoveClientServiceNamesEnabled = config
               .WithKeys(ConfigurationKeys.RemoveClientServiceNamesEnabled)
               .AsBool(defaultValue: false);
            SpanPointersEnabled = config
               .WithKeys(ConfigurationKeys.SpanPointersEnabled)
               .AsBool(defaultValue: true);

            PeerServiceNameMappings = TrimConfigKeysValues(config.WithKeys(ConfigurationKeys.PeerServiceNameMappings));

            MetadataSchemaVersion = config
                                   .WithKeys(ConfigurationKeys.MetadataSchemaVersion)
                                   .GetAs(
                                        defaultValue: new DefaultResult<SchemaVersion>(SchemaVersion.V0, "V0"),
                                        converter: x => x switch
                                        {
                                            "v1" or "V1" => SchemaVersion.V1,
                                            "v0" or "V0" => SchemaVersion.V0,
                                            _ => ParsingResult<SchemaVersion>.Failure(),
                                        },
                                        validator: null);

            StatsComputationInterval = config.WithKeys(ConfigurationKeys.StatsComputationInterval).AsInt32(defaultValue: 10);

            var otelMetricsExporter = config
               .WithKeys(ConfigurationKeys.OpenTelemetry.MetricsExporter);

            OtelMetricsExporterEnabled = string.Equals(otelMetricsExporter.AsString(defaultValue: "otlp"), "otlp", StringComparison.OrdinalIgnoreCase);

            var otelExporterResult = otelMetricsExporter
               .AsBoolResult(
                    null,
                    value => value switch
                    {
                        not null when string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) => ParsingResult<bool>.Success(result: false),
                        not null when string.Equals(value, "otlp", StringComparison.OrdinalIgnoreCase) => ParsingResult<bool>.Success(result: true),
                        _ => ParsingResult<bool>.Failure()
                    });

            var runtimeMetricsEnabledResult = config
                                             .WithKeys(ConfigurationKeys.RuntimeMetricsEnabled)
                                             .AsBoolResult();

            if (runtimeMetricsEnabledResult.ConfigurationResult.IsPresent && otelExporterResult.ConfigurationResult.IsPresent)
            {
                ErrorLog.LogDuplicateConfiguration(ConfigurationKeys.RuntimeMetricsEnabled, ConfigurationKeys.OpenTelemetry.MetricsExporter);
            }
            else if (otelExporterResult.ConfigurationResult is { IsPresent: true, IsValid: false })
            {
                ErrorLog.LogInvalidConfiguration(ConfigurationKeys.OpenTelemetry.MetricsExporter);
            }

            RuntimeMetricsEnabled = runtimeMetricsEnabledResult.WithDefault(false);

            RuntimeMetricsDiagnosticsMetricsApiEnabled = config.WithKeys(ConfigurationKeys.RuntimeMetricsDiagnosticsMetricsApiEnabled).AsBool(false);

#if !NET6_0_OR_GREATER
            if (RuntimeMetricsEnabled && RuntimeMetricsDiagnosticsMetricsApiEnabled)
            {
                Log.Warning(
                    $"{ConfigurationKeys.RuntimeMetricsDiagnosticsMetricsApiEnabled} was enabled, but System.Diagnostics.Metrics is only available on .NET 6+. Using standard runtime metrics collector.");
                telemetry.Record(ConfigurationKeys.RuntimeMetricsDiagnosticsMetricsApiEnabled, false, ConfigurationOrigins.Calculated);
            }
#endif
            OtelMetricExportIntervalMs = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.MetricExportIntervalMs)
                            .AsInt32(defaultValue: 10_000);

            OtelMetricExportTimeoutMs = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.MetricExportTimeoutMs)
                            .AsInt32(defaultValue: 7_500);

            var defaultAgentHost = config
                .WithKeys(ConfigurationKeys.AgentHost)
                .AsString(defaultValue: "localhost");

            OtlpGeneralProtocol = config
                                .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpProtocol)
                                .GetAs(
                                    defaultValue: new(OtlpProtocol.Grpc, "grpc"),
                                    converter: x => x switch
                                    {
                                        not null when string.Equals(x, "grpc", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.Grpc,
                                        not null when string.Equals(x, "http/protobuf", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.HttpProtobuf,
                                        _ => UnsupportedOtlpProtocol(inputValue: x ?? "null"),
                                    },
                                    validator: null);

            OtlpMetricsProtocol = config
                                 .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsProtocol)
                                 .GetAs(
                                      defaultValue: new(OtlpProtocol.Grpc, "grpc"),
                                      converter: x => x switch
                                      {
                                          not null when string.Equals(x, "grpc", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.Grpc,
                                          not null when string.Equals(x, "http/protobuf", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.HttpProtobuf,
                                          not null when string.Equals(x, "http/json", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.HttpJson,
                                          _ => UnsupportedOtlpProtocol(inputValue: x ?? "null"),
                                      },
                                      validator: null);

            var defaultUri = $"http://{defaultAgentHost}:{(!OtlpGeneralProtocol.Equals(OtlpProtocol.Grpc) ? 4318 : 4317)}/";

            OtlpEndpoint = config
                .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpEndpoint)
                .GetAs(
                    defaultValue: new DefaultResult<Uri>(result: new Uri(defaultUri), telemetryValue: defaultUri),
                    validator: null,
                    converter: uriString => new Uri(uriString));

            OtlpMetricsEndpoint = config
                .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsEndpoint)
                .GetAs(
                    defaultValue: new DefaultResult<Uri>(
                        result: OtlpMetricsProtocol switch
                        {
                            OtlpProtocol.Grpc => OtlpEndpoint,
                            _ => new Uri(OtlpEndpoint, "/v1/metrics")
                        },
                        telemetryValue: $"{OtlpEndpoint}{(!OtlpMetricsProtocol.Equals(OtlpProtocol.Grpc) ? "v1/metrics" : string.Empty)}"),
                    validator: null,
                    converter: uriString => new Uri(uriString));

            OtlpMetricsHeaders = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsHeaders)
                            .AsDictionaryResult(separator: '=')
                            .WithDefault(new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), "[]"))
                            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                            .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value?.Trim() ?? string.Empty);

            OtlpMetricsTimeoutMs = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTimeoutMs)
                            .AsInt32(defaultValue: 10_000);

            OtlpMetricsTemporalityPreference = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTemporalityPreference)
                            .GetAs(
                                   defaultValue: new(OtlpTemporalityPreference.Delta, "delta"),
                                   converter: x => x switch
                                   {
                                       not null when string.Equals(x, "cumulative", StringComparison.OrdinalIgnoreCase) => OtlpTemporalityPreference.Cumulative,
                                       not null when string.Equals(x, "delta", StringComparison.OrdinalIgnoreCase) => OtlpTemporalityPreference.Delta,
                                       not null when string.Equals(x, "lowmemory", StringComparison.OrdinalIgnoreCase) => OtlpTemporalityPreference.LowMemory,
                                       _ => ParsingResult<OtlpTemporalityPreference>.Failure(),
                                   },
                                   validator: null);

            OtlpLogsProtocol = config
                             .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsProtocol)
                             .GetAs(
                                  defaultValue: new(OtlpProtocol.Grpc, "grpc"),
                                  converter: x => x switch
                                  {
                                      not null when string.Equals(x, "grpc", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.Grpc,
                                      not null when string.Equals(x, "http/json", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.HttpJson,
                                      not null when string.Equals(x, "http/protobuf", StringComparison.OrdinalIgnoreCase) => OtlpProtocol.HttpProtobuf,
                                      _ => UnsupportedOtlpProtocol(inputValue: x ?? "null"),
                                  },
                                  validator: null);

            OtlpLogsEndpoint = config
                .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsEndpoint)
                .GetAs(
                    defaultValue: new DefaultResult<Uri>(
                        result: OtlpLogsProtocol switch
                        {
                            OtlpProtocol.Grpc => OtlpEndpoint,
                            _ => new Uri(OtlpEndpoint, "/v1/logs")
                        },
                        telemetryValue: $"{OtlpEndpoint}{(!OtlpLogsProtocol.Equals(OtlpProtocol.Grpc) ? "v1/logs" : string.Empty)}"),
                    validator: null,
                    converter: uriString => new Uri(uriString));

            OtlpLogsHeaders = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsHeaders)
                            .AsDictionaryResult(separator: '=')
                            .WithDefault(new DefaultResult<IDictionary<string, string>>(new Dictionary<string, string>(), "[]"))
                            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                            .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value?.Trim() ?? string.Empty);

            OtlpLogsTimeoutMs = config
                            .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeoutMs)
                            .AsInt32(defaultValue: 10_000);

            var otelLogsExporter = config
                    .WithKeys(ConfigurationKeys.OpenTelemetry.LogsExporter);

            var otelLogsExporterResult = otelLogsExporter
               .AsBoolResult(
                    null,
                    value => value switch
                    {
                        not null when string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) => ParsingResult<bool>.Success(result: false),
                        not null when string.Equals(value, "otlp", StringComparison.OrdinalIgnoreCase) => ParsingResult<bool>.Success(result: true),
                        _ => ParsingResult<bool>.Failure()
                    });

            // Per OpenTelemetry spec, OTEL_LOGS_EXPORTER defaults to "otlp" if not set
            OtelLogsExporterEnabled = otelLogsExporterResult.ConfigurationResult switch
            {
                { IsPresent: true, IsValid: true, Result: true } => true,
                { IsPresent: true, IsValid: true, Result: false } => false,
                _ => true // Default to otlp per spec
            };

            if (otelLogsExporterResult.ConfigurationResult is { IsPresent: true, IsValid: false })
            {
                ErrorLog.LogInvalidConfiguration(ConfigurationKeys.OpenTelemetry.LogsExporter);
            }

            OpenTelemetryLogsEnabled = config
                                    .WithKeys(ConfigurationKeys.FeatureFlags.OpenTelemetryLogsEnabled)
                                    .AsBool(defaultValue: false);

            OpenTelemetryLogsEnabled = OpenTelemetryLogsEnabled && OtelLogsExporterEnabled;

            // We should also be writing telemetry for OTEL_LOGS_EXPORTER similar to OTEL_METRICS_EXPORTER, but we don't have a corresponding Datadog config
            // When we do, we can insert that here
            CustomSamplingRulesFormat = config.WithKeys(ConfigurationKeys.CustomSamplingRulesFormat)
                                              .GetAs(
                                                   defaultValue: new DefaultResult<string>(SamplingRulesFormat.Glob, "glob"),
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
            telemetry.Record(
                    key: ConfigurationKeys.CustomSamplingRulesFormat,
                    value: CustomSamplingRulesFormat,
                    recordValue: true,
                    origin: ConfigurationOrigins.Calculated);

            SpanSamplingRules = config.WithKeys(ConfigurationKeys.SpanSamplingRules).AsString();

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

            SingleSpanAspNetCoreEnabled = config
                                         .WithKeys(ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled)
                                         .AsBool(defaultValue: false);
#if !NET6_0_OR_GREATER
            // single span aspnetcore is only supported in .NET 6+, so override for telemetry purposes
            if (SingleSpanAspNetCoreEnabled)
            {
                SingleSpanAspNetCoreEnabled = false;
                Log.Warning(
                    $"{ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled} is set to true, but is only supported in .NET 6+. Using false instead.");
                telemetry.Record(ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled, false, ConfigurationOrigins.Calculated);
            }
#endif

            ExpandRouteTemplatesEnabled = config
                                         .WithKeys(ConfigurationKeys.ExpandRouteTemplatesEnabled)
                                         .AsBool(defaultValue: !(RouteTemplateResourceNamesEnabled || SingleSpanAspNetCoreEnabled)); // disabled by default if route template resource names or single-span enabled

            AzureServiceBusBatchLinksEnabled = config
                                             .WithKeys(ConfigurationKeys.AzureServiceBusBatchLinksEnabled)
                                             .AsBool(defaultValue: true);

            AzureEventHubsBatchLinksEnabled = config
                                             .WithKeys(ConfigurationKeys.AzureEventHubsBatchLinksEnabled)
                                             .AsBool(defaultValue: true);

            AgentFeaturePollingEnabled = config
                                        .WithKeys(ConfigurationKeys.AgentFeaturePollingEnabled)
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
               .WithKeys(ConfigurationKeys.PropagationStyleInject)
               .GetAsClassResult(
                    validator: injectionValidator, // invalid individual values are rejected later
                    converter: style => TrimSplitString(style, commaSeparator))
               .OverrideWith(in otelPropagation, ErrorLog, getDefaultPropagationHeaders);

            PropagationStyleExtract = config
                                     .WithKeys(ConfigurationKeys.PropagationStyleExtract)
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
                                             defaultValue: new(ExtractBehavior.Continue, "continue"),
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

            BaggageTagKeys = new HashSet<string>(
                            config
                            .WithKeys(ConfigurationKeys.BaggageTagKeys)
                            .AsString(defaultValue: "user.id,session.id,account.id")
                            ?.Split([','], StringSplitOptions.RemoveEmptyEntries) ?? []);

            LogSubmissionSettings = new DirectLogSubmissionSettings(source, telemetry, OpenTelemetryLogsEnabled);

            TraceMethods = config
                          .WithKeys(ConfigurationKeys.TraceMethods)
                          .AsString(string.Empty);

            OutgoingTagPropagationHeaderMaxLength = config
                                                   .WithKeys(ConfigurationKeys.TagPropagation.HeaderMaxLength)
                                                   .AsInt32(
                                                        Tagging.TagPropagation.OutgoingTagPropagationHeaderMaxLength,
                                                        x => x is >= 0 and <= Tagging.TagPropagation.OutgoingTagPropagationHeaderMaxLength)
                                                   .Value;

            IpHeader = config
                      .WithKeys(ConfigurationKeys.IpHeader)
                      .AsString();

            IpHeaderEnabled = config
                             .WithKeys(ConfigurationKeys.IpHeaderEnabled)
                             .AsBool(false);

            // DSM is now enabled by default in non-serverless environments
            IsDataStreamsMonitoringEnabled = config
                                            .WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled)
                                            .AsBool(
                                                  !EnvironmentHelpers.IsAwsLambda() &&
                                                  !EnvironmentHelpers.IsAzureAppServices() &&
                                                  !EnvironmentHelpers.IsAzureFunctions() &&
                                                  !EnvironmentHelpers.IsGoogleCloudFunctions());

            IsDataStreamsMonitoringInDefaultState = config
                                                    .WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled)
                                                    .AsBool() == null;

            // no legacy headers if we are in "enbaled by default" state
            IsDataStreamsLegacyHeadersEnabled = config
                                               .WithKeys(ConfigurationKeys.DataStreamsMonitoring.LegacyHeadersEnabled)
                                               .AsBool(!IsDataStreamsMonitoringInDefaultState);

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

            if (IsRunningInCiVisibility)
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
                                     defaultValue: new(DbmPropagationLevel.Disabled, nameof(DbmPropagationLevel.Disabled)),
                                     converter: x => ToDbmPropagationInput(x) ?? ParsingResult<DbmPropagationLevel>.Failure(),
                                     validator: null);

            InjectSqlBasehash = config
                .WithKeys(ConfigurationKeys.InjectSqlBasehash)
                .AsBool(false);

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

            IsFlaggingProviderEnabled = config.WithKeys(ConfigurationKeys.FeatureFlags.FlaggingProviderEnabled)
                                                       .AsBool(false);

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
            OpenTelemetryMeterNames = !string.IsNullOrEmpty(enabledMeters)
                ? new HashSet<string>(TrimSplitString(enabledMeters, commaSeparator), StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

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

            PartialFlushEnabled = config.WithKeys(ConfigurationKeys.PartialFlushEnabled).AsBool(false);
            PartialFlushMinSpans = config
                                  .WithKeys(ConfigurationKeys.PartialFlushMinSpans)
                                  .AsInt32(500, value => value > 0).Value;

            GraphQLErrorExtensions = TrimSplitString(
                config.WithKeys(ConfigurationKeys.GraphQLErrorExtensions).AsString(),
                commaSeparator);

            // We create a lazy here because this is kind of expensive, and we want to avoid calling it if we can
            _fallbackApplicationName = new(() => ApplicationNameHelpers.GetFallbackApplicationName(this));

            // There's a circular dependency here because DataPipeline depends on ExporterSettings,
            // but the settings manager depends on TracerSettings. Basically this is all fine as long
            // as nothing in the MutableSettings or ExporterSettings depends on the value of DataPipelineEnabled!
            Manager = new(source, this, telemetry, errorLog);

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
                    telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }

                // Windows supports UnixDomainSocket https://devblogs.microsoft.com/commandline/af_unix-comes-to-windows/
                // but tokio hasn't added support for it yet https://github.com/tokio-rs/tokio/issues/2201
                // There's an issue here, in that technically a user can initially be configured to send over TCP/named pipes,
                // and so we allow and enable the datapipeline. Later, they could configure the app in code to send over UDS.
                // This is a problem, as we currently don't support toggling the data pipeline at runtime, so we explicitly block
                // this scenario in the public API.
                if (Manager.InitialExporterSettings.TracesTransport == TracesTransportType.UnixDomainSocket && FrameworkDescription.Instance.IsWindows())
                {
                    DataPipelineEnabled = false;
                    Log.Warning(
                        $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but TracesTransport is set to UnixDomainSocket which is not supported on Windows. Disabling data pipeline.");
                    telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }

                if (!isLibDatadogAvailable.IsAvailable)
                {
                    DataPipelineEnabled = false;
                    if (isLibDatadogAvailable.Exception is not null)
                    {
                        Log.Warning(
                            isLibDatadogAvailable.Exception,
                            $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but libdatadog is not available. Disabling data pipeline.");
                    }
                    else
                    {
                        Log.Warning(
                            $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but libdatadog is not available. Disabling data pipeline.");
                    }

                    telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }

                // SSI already utilizes libdatadog. To prevent unexpected behavior,
                // we proactively disable the data pipeline when SSI is enabled. Theoretically, this should not cause any issues,
                // but as a precaution, we are taking a conservative approach during the initial rollout phase.
                if (!string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable("DD_INJECTION_ENABLED")))
                {
                    DataPipelineEnabled = false;
                    Log.Warning(
                        $"{ConfigurationKeys.TraceDataPipelineEnabled} is enabled, but SSI is enabled. Disabling data pipeline.");
                    telemetry.Record(ConfigurationKeys.TraceDataPipelineEnabled, false, ConfigurationOrigins.Calculated);
                }
            }
        }

        internal bool IsRunningInCiVisibility { get; }

        internal HashSet<string> ExperimentalFeaturesEnabled { get; }

        internal bool PropagateProcessTags { get; }

        internal OverrideErrorLog ErrorLog { get; }

        internal string FallbackApplicationName => _fallbackApplicationName.Value;

        /// <summary>
        /// Gets a value indicating whether we should tag every telemetry event with git metadata.
        /// Default value is <c>true</c> (enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.GitMetadataEnabled"/>
        internal bool GitMetadataEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether APM traces are enabled.
        /// Default is <c>true</c>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.ApmTracingEnabled"/>
        internal bool ApmTracingEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether OpenTelemetry Metrics are enabled.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.OpenTelemetryMetricsEnabled"/>
        internal bool OpenTelemetryMetricsEnabled { get; }

        /// Gets the names of enabled Meters.
        /// <seealso cref="ConfigurationKeys.FeatureFlags.OpenTelemetryMeterNames"/>
        internal HashSet<string> OpenTelemetryMeterNames { get; }

        /// <summary>
        /// Gets a value indicating whether the OpenTelemetry metrics exporter is enabled.
        /// This is derived from <see cref="ConfigurationKeys.OpenTelemetry.MetricsExporter"/> config where 'otlp' enables the exporter
        /// and 'none' disables it and runtime metrics if related DD env var is not set.
        /// Default is enabled (true).
        /// </summary>
        internal bool OtelMetricsExporterEnabled { get; }

        /// <summary>
        /// Gets the OTLP protocol for metrics export with fallback behavior.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsProtocol"/>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpProtocol"/>
        internal OtlpProtocol OtlpMetricsProtocol { get; }

        /// <summary>
        /// Gets the OTLP endpoint URL for metrics export fallbacks on <see cref="OtlpEndpoint"/>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsEndpoint"/>
        internal Uri OtlpMetricsEndpoint { get; }

        /// <summary>
        /// Gets the OTLP base endpoint URL for otlp export.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpEndpoint"/>
        internal Uri OtlpEndpoint { get; }

        /// <summary>
        /// Gets the OTLP headers for metrics export with fallback behavior.
        /// Parsed from comma-separated key-value pairs (api-key=key,other=value).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsHeaders"/>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpHeaders"/>
        internal IReadOnlyDictionary<string, string> OtlpMetricsHeaders { get; }

        /// <summary>
        /// Gets the OpenTelemetry metric export interval (in milliseconds) between export attempts.
        /// Default is 10000ms (10s) for Datadog - deviates from OTel spec default of 60000ms (60s).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.MetricExportIntervalMs"/>
        internal int OtelMetricExportIntervalMs { get; }

        /// <summary>
        /// Gets the OpenTelemetry metric export timeout (in milliseconds) for collection and export.
        /// Default is 7500ms (7.5s) for Datadog - deviates from OTel spec default of 30000ms (30s).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.MetricExportTimeoutMs"/>
        internal int OtelMetricExportTimeoutMs { get; }

        /// <summary>
        /// Gets the OTLP request timeout (in milliseconds).
        /// Default is 10000ms (10s).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTimeoutMs"/>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeoutMs"/>
        internal int OtlpMetricsTimeoutMs { get; }

        /// <summary>
        /// Gets the OTLP metrics temporality preference.
        /// Default is 'delta' for Datadog - deviates from OTel spec default of 'cumulative'.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpMetricsTemporalityPreference"/>
        internal OtlpTemporalityPreference OtlpMetricsTemporalityPreference { get; }

       /// <summary>
        /// Gets a value indicating whether the OpenTelemetry metrics exporter is enabled.
        /// This is derived from <see cref="ConfigurationKeys.OpenTelemetry.LogsExporter"/> config where 'otlp' enables the exporter
        /// and 'none' disables it and runtime metrics if related DD env var is not set.
        /// Default is enabled (true).
        /// </summary>
        internal bool OtelLogsExporterEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether Datadog's OTLP logs feature is enabled.
        /// This is set via DD_LOGS_OTEL_ENABLED (parallel to DD_METRICS_OTEL_ENABLED).
        /// Default is disabled (false).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.OpenTelemetryLogsEnabled"/>
        internal bool OpenTelemetryLogsEnabled { get; }

        /// <summary>
        /// Gets the OTLP protocol for logs export with fallback behavior.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsProtocol"/>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpProtocol"/>
        internal OtlpProtocol OtlpLogsProtocol { get; }

        /// <summary>
        /// Gets the OTLP endpoint URL for logs export fallbacks on <see cref="OtlpEndpoint"/>.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsEndpoint"/>
        internal Uri OtlpLogsEndpoint { get; }

        /// <summary>
        /// Gets the OTLP headers for logs export with fallback behavior.
        /// Parsed from comma-separated key-value pairs (api-key=key,other=value).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsHeaders"/>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpHeaders"/>
        internal IReadOnlyDictionary<string, string> OtlpLogsHeaders { get; }

        /// <summary>
        /// Gets the OTLP request timeout (in milliseconds) for logs.
        /// Default is 10000ms (10s).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeoutMs"/>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeoutMs"/>
        internal int OtlpLogsTimeoutMs { get; }

       /// <summary>
        /// Gets the non siganl specific OTLP protocol.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpProtocol"/>
        internal OtlpProtocol OtlpGeneralProtocol { get; }

        /// <summary>
        /// Gets the names of disabled ActivitySources.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DisabledActivitySources"/>
        internal string[] DisabledActivitySources { get; }

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
        /// Gets a custom request header configured to read the ip from. For backward compatibility, it fallbacks on DD_APPSEC_IPHEADER
        /// </summary>
        internal string? IpHeader { get; }

        /// <summary>
        /// Gets a value indicating whether the ip header should not be collected. The default is false.
        /// </summary>
        internal bool IpHeaderEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether stats are computed on the tracer side
        /// </summary>
        public bool StatsComputationEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether to enable span linking for individual messages
        /// when using Azure Service Bus batch operations.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AzureServiceBusBatchLinksEnabled"/>
        public bool AzureServiceBusBatchLinksEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether span links should be created for Azure EventHubs batch operations.
        /// When enabled, TryAdd spans are created and linked to the send span.
        /// When disabled, TryAdd spans are not created.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AzureEventHubsBatchLinksEnabled"/>
        public bool AzureEventHubsBatchLinksEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the agent discovery service is enabled.
        /// When disabled, the tracer will not query the agent for available endpoints.
        /// This is useful in environments where the discovery endpoint is not available (e.g., Azure Functions with Rust agent).
        /// Default value is true (discovery service enabled).
        /// </summary>
        /// <seealso cref="ConfigurationKeys.AgentFeaturePollingEnabled"/>
        public bool AgentFeaturePollingEnabled { get; }

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
        internal HashSet<string> BaggageTagKeys { get; }

        /// <summary>
        /// Gets a value indicating whether runtime metrics
        /// are enabled and sent to DogStatsd.
        /// </summary>
        internal bool RuntimeMetricsEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether the experimental runtime metrics collector which uses the
        /// <a href="https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics">System.Diagnostics.Metrics</a> API.
        /// This collector can only be enabled when using .NET 6+, and will only include ASP.NET Core metrics
        /// when using .NET 8+.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.RuntimeMetricsDiagnosticsMetricsApiEnabled"/>
        internal bool RuntimeMetricsDiagnosticsMetricsApiEnabled { get; }

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
        /// when <see cref="RouteTemplateResourceNamesEnabled"/> or <see cref="SingleSpanAspNetCoreEnabled"/> are <code>true</code>.
        /// </summary>
        internal bool ExpandRouteTemplatesEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether single-span ASP.NET Core instrumentation should be used.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.FeatureFlags.SingleSpanAspNetCoreEnabled"/>
        internal bool SingleSpanAspNetCoreEnabled { get; }

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
        /// Gets a value indicating whether data streams configuration is present or not (set to true or false).
        /// </summary>
        internal bool IsDataStreamsMonitoringInDefaultState { get; }

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
        /// Gets a value indicating whether the tracer should inject Base Hash in SQL Comments.
        /// Default value is false (disabled).
        /// </summary>
        internal bool InjectSqlBasehash { get; }

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

        /// <summary>
        /// Gets a value indicating whether remote Feature Flags Provider is enabled
        /// </summary>
        internal bool IsFlaggingProviderEnabled { get; }

        /// <summary>
        /// Gets a value indicating whether partial flush is enabled
        /// </summary>
        public bool PartialFlushEnabled { get; }

        /// <summary>
        /// Gets the minimum number of closed spans in a trace before it's partially flushed
        /// </summary>
        public int PartialFlushMinSpans { get; }

        internal SettingsManager Manager { get; }

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
            => new(GlobalConfigurationSource.Instance, TelemetryFactory.Config, new());

        internal static ReadOnlyDictionary<string, string>? TrimConfigKeysValues(ConfigurationBuilder.HasKeys key)
        {
            var mappings = key.AsDictionary()
              ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
               .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());
            return mappings is not null ? new(mappings) : null;
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

        private static ParsingResult<OtlpProtocol> UnsupportedOtlpProtocol(string inputValue)
        {
            Log.Warning("Unsupported OTLP protocol '{Protocol}'. Supported values are 'grpc', 'http/protobuf' and 'http/json'. Using default: http/protobuf", inputValue);
            return ParsingResult<OtlpProtocol>.Failure();
        }

        internal static TracerSettings Create(Dictionary<string, object?> settings)
            => Create(settings, LibDatadogAvailabilityHelper.IsLibDatadogAvailable);

        internal static TracerSettings Create(Dictionary<string, object?> settings, LibDatadogAvailableResult isLibDatadogAvailable)
            => new(new DictionaryConfigurationSource(settings.ToDictionary(x => x.Key, x => x.Value?.ToString()!)), new ConfigurationTelemetry(), new(), isLibDatadogAvailable);
    }
}

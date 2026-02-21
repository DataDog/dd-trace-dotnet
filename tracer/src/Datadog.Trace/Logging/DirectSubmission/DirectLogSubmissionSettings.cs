// <copyright file="DirectLogSubmissionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Serverless;
using Datadog.Trace.Util;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// Contains settings for Direct Log Submission.
    /// </summary>
    internal sealed class DirectLogSubmissionSettings
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DirectLogSubmissionSettings>();

        internal const string DefaultSource = "csharp";
        internal const DirectSubmissionLogLevel DefaultMinimumLevel = DirectSubmissionLogLevel.Information;
        internal const int DatadogDefaultBatchSizeLimit = 1000;
        internal const int DatadogDefaultQueueSizeLimit = 100_000;
        internal const int DatadogDefaultBatchPeriodSeconds = 2;

        // OpenTelemetry OTLP logs defaults (OTEL_BLRP_* environment variables)
        internal const int OtlpBatchSizeLimit = 512;        // OTEL_BLRP_MAX_EXPORT_BATCH_SIZE
        internal const int OtlpQueueSizeLimit = 2048;       // OTEL_BLRP_MAX_QUEUE_SIZE
        internal const int OtlpBatchPeriodSeconds = 1;      // OTEL_BLRP_SCHEDULE_DELAY

        private const string IntakePrefix = "https://http-intake.logs.";
        private const string DefaultSite = "datadoghq.com";
        private const string IntakeSuffix = ":443";
        private readonly bool[]? _enabledIntegrations;

        internal static readonly IntegrationId[] SupportedIntegrations =
        {
            IntegrationId.Serilog,
            IntegrationId.ILogger,
            IntegrationId.Log4Net,
            IntegrationId.NLog,
            IntegrationId.XUnit,
        };

        public DirectLogSubmissionSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry, bool otlpLogsEnabled = false)
        {
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);
            Host = config
                                     .WithKeys(ConfigurationKeys.DirectLogSubmission.Host)
                                     .AsString(HostMetadata.Instance.Hostname ?? string.Empty);
            Source = config
                                       .WithKeys(ConfigurationKeys.DirectLogSubmission.Source)
                                       .AsString(DefaultSource);

            var directLogSubmissionUrl = config
                                    .WithKeys(ConfigurationKeys.DirectLogSubmission.Url)
                                    .AsString(
                                         getDefaultValue: () =>
                                         {
                                             // They didn't provide a URL, use the default (With DD_SITE if provided)
                                             var ddSite = config
                                                         .WithKeys(ConfigurationKeys.Site)
                                                         .AsString(DefaultSite, x => !string.IsNullOrEmpty(x));

                                             return $"{IntakePrefix}{ddSite}{IntakeSuffix}";
                                         },
                                         validator: x => !string.IsNullOrEmpty(x));

            MinimumLevel = config
                                             .WithKeys(ConfigurationKeys.DirectLogSubmission.MinimumLevel)
                                             .GetAs(
                                                  defaultValue: new(DefaultMinimumLevel, nameof(DirectSubmissionLogLevel.Information)),
                                                  converter: x => DirectSubmissionLogLevelExtensions.Parse(x) ?? ParsingResult<DirectSubmissionLogLevel>.Failure(),
                                                  validator: null);

            var globalTags = config
                            .WithKeys(ConfigurationKeys.DirectLogSubmission.GlobalTags)
                            .AsDictionary()
                           ?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                            .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim());

            GlobalTags = new ReadOnlyDictionary<string, string>(globalTags ?? []);

            var defaultBatchSize = otlpLogsEnabled ? OtlpBatchSizeLimit : DatadogDefaultBatchSizeLimit;
            var defaultQueueSize = otlpLogsEnabled ? OtlpQueueSizeLimit : DatadogDefaultQueueSizeLimit;
            var defaultBatchPeriod = otlpLogsEnabled ? OtlpBatchPeriodSeconds : DatadogDefaultBatchPeriodSeconds;

            BatchSizeLimit = config
                                               .WithKeys(ConfigurationKeys.DirectLogSubmission.BatchSizeLimit)
                                               .AsInt32(defaultBatchSize, x => x > 0)
                                               .Value;

            QueueSizeLimit = config
                                               .WithKeys(ConfigurationKeys.DirectLogSubmission.QueueSizeLimit)
                                               .AsInt32(defaultQueueSize, x => x > 0)
                                               .Value;

            var seconds = config
                     .WithKeys(ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds)
                     .AsInt32(defaultBatchPeriod, x => x > 0)
                     .Value;

            BatchPeriod = TimeSpan.FromSeconds(seconds);

            AzureFunctionsHostEnabled = config.WithKeys(ConfigurationKeys.DirectLogSubmission.AzureFunctionsHostEnabled)
                                              .AsBool(false);

            ApiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString() ?? string.Empty;
            bool[]? enabledIntegrations = null;

            List<string> validationErrors = [];
            var logSubmissionIntegrations = config
                                           .WithKeys(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations)
                                           .AsString()
                                          ?.Split([';'], StringSplitOptions.RemoveEmptyEntries);

            if (logSubmissionIntegrations is { Length: > 0 })
            {
                foreach (var integrationName in logSubmissionIntegrations)
                {
                    if (!IntegrationRegistry.TryGetIntegrationId(integrationName, out var integrationId))
                    {
                        validationErrors.Add(
                            "Unknown integration: " + integrationName +
                            ". Use a valid logs integration name: " +
                            string.Join(", ", SupportedIntegrations.Select(x => IntegrationRegistry.GetName(x))));
                        continue;
                    }

                    if (!SupportedIntegrations.Contains(integrationId))
                    {
                        validationErrors.Add(
                            "Integration: " + integrationName + " is not a supported direct log submission integration. " +
                            "Use one of " + string.Join(", ", SupportedIntegrations.Select(x => IntegrationRegistry.GetName(x))));
                        continue;
                    }

                    enabledIntegrations ??= new bool[IntegrationRegistry.Ids.Count];
                    if (!enabledIntegrations[(int)integrationId])
                    {
                        enabledIntegrations[(int)integrationId] = true;
                    }
                }
            }

            if (otlpLogsEnabled)
            {
                enabledIntegrations = new bool[IntegrationRegistry.Ids.Count];

                // Explicitly disable other log integrations to avoid duplicate log submission
                foreach (var integrationId in SupportedIntegrations)
                {
                    enabledIntegrations[(int)integrationId] = false;
                }

                enabledIntegrations[(int)IntegrationId.ILogger] = true;

                Log.Information("OTLP logs enabled: ILogger integration is enabled, other log integrations (Serilog, NLog, Log4Net) are disabled to prevent duplicate log submission.");
            }

            _enabledIntegrations = enabledIntegrations;

            var isEnabled = enabledIntegrations is not null;

            if (!AzureFunctionsHostEnabled && Azure.Default.IsIsolatedFunctionHostProcess)
            {
                isEnabled = false;
            }

            if (!otlpLogsEnabled)
            {
                if (string.IsNullOrWhiteSpace(Host))
                {
                    isEnabled = false;
                    validationErrors.Add($"Missing required setting '{ConfigurationKeys.DirectLogSubmission.Host}'.");
                }

                if (string.IsNullOrWhiteSpace(Source))
                {
                    isEnabled = false;
                    validationErrors.Add($"Missing required setting '{ConfigurationKeys.DirectLogSubmission.Source}'.");
                }

                if (!Uri.TryCreate(directLogSubmissionUrl, UriKind.Absolute, out var uri))
                {
                    isEnabled = false;
                    validationErrors.Add($"The intake url '{directLogSubmissionUrl}' was not a valid URL.");
                }
                else
                {
                    IntakeUrl = uri;
                }

                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    isEnabled = false;
                    validationErrors.Add($"Missing required settings '{ConfigurationKeys.ApiKey}'.");
                }
            }

            ValidationErrors = validationErrors;
            IsEnabled = isEnabled;
        }

        /// <summary>
        /// Gets a value indicating whether direct log submission is enabled
        /// </summary>
        internal bool IsEnabled { get; }

        /// <summary>
        /// Gets the validation errors, if any
        /// </summary>
        internal IReadOnlyList<string> ValidationErrors { get; }

        /// <summary>
        /// Gets the originating host name for direct logs submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Host" />
        internal string Host { get; }

        /// <summary>
        /// Gets the originating source for direct logs submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Source" />
        internal string Source { get; }

        /// <summary>
        /// Gets the global tags, which are applied to all directly submitted logs. If not provided,
        /// <see cref="MutableSettings.GlobalTags"/> are used instead
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.GlobalTags" />
        internal IReadOnlyDictionary<string, string> GlobalTags { get; }

        /// <summary>
        /// Gets the url to send logs to
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Url" />
        internal Uri? IntakeUrl { get; }

        /// <summary>
        /// Gets the minimum level logs should have to be sent to the intake.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Url" />
        internal DirectSubmissionLogLevel MinimumLevel { get; }

        /// <summary>
        /// Gets the maximum number of logs to send at one time
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.BatchSizeLimit"/>
        internal int BatchSizeLimit { get; }

        /// <summary>
        /// Gets the maximum number of logs to hold in internal queue at any one time
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.QueueSizeLimit"/>
        internal int QueueSizeLimit { get; }

        /// <summary>
        /// Gets or sets the time to wait between checking for batches
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds"/>
        internal TimeSpan BatchPeriod { get; set; }

        /// <summary>
        /// Gets a value indicating whether direct log submission is enabled for the Azure Functions host.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.AzureFunctionsHostEnabled"/>
        internal bool AzureFunctionsHostEnabled { get; } = false;

        /// <summary>
        /// Gets the Datadog API key
        /// </summary>
        internal string ApiKey { get; }

        public IEnumerable<string> EnabledIntegrationNames
            => SupportedIntegrations.Where(x => _enabledIntegrations?[(int)x] == true).Select(x => IntegrationRegistry.GetName(x));

        public BatchingSinkOptions CreateBatchingSinkOptions()
            => new(
                batchSizeLimit: BatchSizeLimit,
                queueLimit: QueueSizeLimit,
                period: BatchPeriod);

        public bool IsIntegrationEnabled(IntegrationId integrationId)
        {
            return IsEnabled && _enabledIntegrations is not null && _enabledIntegrations[(int)integrationId];
        }
    }
}

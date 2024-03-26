// <copyright file="DirectLogSubmissionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// Contains settings for Direct Log Submission.
    /// </summary>
    internal class DirectLogSubmissionSettings
    {
        internal const string DefaultSource = "csharp";
        internal const DirectSubmissionLogLevel DefaultMinimumLevel = DirectSubmissionLogLevel.Information;
        internal const int DefaultBatchSizeLimit = 1000;
        internal const int DefaultQueueSizeLimit = 100_000;
        internal const int DefaultBatchPeriodSeconds = 2;
        private const string IntakePrefix = "https://http-intake.logs.";
        private const string DefaultSite = "datadoghq.com";
        private const string IntakeSuffix = ":443";

        public DirectLogSubmissionSettings(IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            // TODO: Combine DirectLogSubmissionSettings and ImmutableDirectLogSubmissionSettings
            source ??= NullConfigurationSource.Instance;
            var config = new ConfigurationBuilder(source, telemetry);

            DirectLogSubmissionHost = config
                                     .WithKeys(ConfigurationKeys.DirectLogSubmission.Host)
                                     .AsString(HostMetadata.Instance.Hostname ?? string.Empty);
            DirectLogSubmissionSource = config
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

            DirectLogSubmissionMinimumLevel = config
                                             .WithKeys(ConfigurationKeys.DirectLogSubmission.MinimumLevel)
                                             .GetAs(
                                                  () => new DefaultResult<DirectSubmissionLogLevel>(DefaultMinimumLevel, nameof(DirectSubmissionLogLevel.Information)),
                                                  converter: x => DirectSubmissionLogLevelExtensions.Parse(x) ?? ParsingResult<DirectSubmissionLogLevel>.Failure(),
                                                  validator: null);

            var globalTags = config
                            .WithKeys(ConfigurationKeys.DirectLogSubmission.GlobalTags)
                            .AsDictionary();

            DirectLogSubmissionGlobalTags = globalTags?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                                       .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim())
                                         ?? new Dictionary<string, string>();

            var logSubmissionIntegrations = config
                                           .WithKeys(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations)
                                           .AsString()
                                          ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                            Enumerable.Empty<string>();
            DirectLogSubmissionEnabledIntegrations = new HashSet<string>(logSubmissionIntegrations, StringComparer.OrdinalIgnoreCase);

            DirectLogSubmissionBatchSizeLimit = config
                                               .WithKeys(ConfigurationKeys.DirectLogSubmission.BatchSizeLimit)
                                               .AsInt32(DefaultBatchSizeLimit, x => x > 0)
                                               .Value;

            DirectLogSubmissionQueueSizeLimit = config
                                               .WithKeys(ConfigurationKeys.DirectLogSubmission.QueueSizeLimit)
                                               .AsInt32(DefaultQueueSizeLimit, x => x > 0)
                                               .Value;

            var seconds = config
                     .WithKeys(ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds)
                     .AsInt32(DefaultBatchPeriodSeconds, x => x > 0)
                     .Value;

            DirectLogSubmissionBatchPeriod = TimeSpan.FromSeconds(seconds);

            ApiKey = config.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();

            var isEnabled = DirectLogSubmissionEnabledIntegrations.Count > 0;
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(DirectLogSubmissionHost))
            {
                isEnabled = false;
                validationErrors.Add($"Missing required setting '{ConfigurationKeys.DirectLogSubmission.Host}'.");
            }

            if (string.IsNullOrWhiteSpace(DirectLogSubmissionSource))
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
                DirectLogSubmissionUrl = uri;
            }

            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                isEnabled = false;
                validationErrors.Add($"Missing required settings '{ConfigurationKeys.ApiKey}'.");
            }

            ValidationErrors = validationErrors;
            IsEnabled = isEnabled;

            // Logs injection is enabled by default if direct log submission is enabled, otherwise disabled by default
            LogsInjectionEnabled = config.WithKeys(ConfigurationKeys.LogsInjectionEnabled).AsBool(defaultValue: isEnabled);
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
        /// Gets the integrations enabled for direct log submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.EnabledIntegrations" />
        internal HashSet<string> DirectLogSubmissionEnabledIntegrations { get; }

        /// <summary>
        /// Gets the originating host name for direct logs submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Host" />
        internal string DirectLogSubmissionHost { get; }

        /// <summary>
        /// Gets the originating source for direct logs submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Source" />
        internal string DirectLogSubmissionSource { get; }

        /// <summary>
        /// Gets the global tags, which are applied to all directly submitted logs. If not provided,
        /// <see cref="TracerSettings.GlobalTags"/> are used instead
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.GlobalTags" />
        internal IDictionary<string, string> DirectLogSubmissionGlobalTags { get; }

        /// <summary>
        /// Gets the url to send logs to
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Url" />
        internal Uri? DirectLogSubmissionUrl { get; }

        /// <summary>
        /// Gets the minimum level logs should have to be sent to the intake.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Url" />
        internal DirectSubmissionLogLevel DirectLogSubmissionMinimumLevel { get; }

        /// <summary>
        /// Gets the maximum number of logs to send at one time
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.BatchSizeLimit"/>
        internal int DirectLogSubmissionBatchSizeLimit { get; }

        /// <summary>
        /// Gets the maximum number of logs to hold in internal queue at any one time
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.QueueSizeLimit"/>
        internal int DirectLogSubmissionQueueSizeLimit { get; }

        /// <summary>
        /// Gets or sets the time to wait between checking for batches
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds"/>
        internal TimeSpan DirectLogSubmissionBatchPeriod { get; set; }

        /// <summary>
        /// Gets the Datadog API key
        /// </summary>
        internal string? ApiKey { get; }

        /// <summary>
        /// Gets or sets a value indicating whether logs injection is enabled or disabled
        /// </summary>
        internal bool LogsInjectionEnabled { get; set; }
    }
}

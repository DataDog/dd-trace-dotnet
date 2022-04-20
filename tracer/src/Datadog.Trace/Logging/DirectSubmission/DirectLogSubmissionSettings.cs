﻿// <copyright file="DirectLogSubmissionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// Contains settings for Direct Log Submission.
    /// </summary>
    internal class DirectLogSubmissionSettings
    {
        private const string DefaultSource = "csharp";
        private const string IntakePrefix = "https://http-intake.logs.";
        private const string DefaultSite = "datadoghq.com";
        private const string IntakeSuffix = ":443";
        private const DirectSubmissionLogLevel DefaultMinimumLevel = DirectSubmissionLogLevel.Information;
        private const int DefaultBatchSizeLimit = 1000;
        private const int DefaultQueueSizeLimit = 100_000;
        private const int DefaultBatchPeriodSeconds = 2;

        public DirectLogSubmissionSettings()
            : this(source: null)
        {
        }

        public DirectLogSubmissionSettings(IConfigurationSource? source)
        {
            DirectLogSubmissionHost = source?.GetString(ConfigurationKeys.DirectLogSubmission.Host)
                                   ?? HostMetadata.Instance.Hostname;
            DirectLogSubmissionSource = source?.GetString(ConfigurationKeys.DirectLogSubmission.Source) ?? DefaultSource;

            var overriddenSubmissionUrl = source?.GetString(ConfigurationKeys.DirectLogSubmission.Url);
            if (!string.IsNullOrEmpty(overriddenSubmissionUrl))
            {
                // if they provide a url, use it
                DirectLogSubmissionUrl = overriddenSubmissionUrl;
            }
            else
            {
                // They didn't provide a URL, use the default (With DD_SITE if provided)
                var specificSite = source?.GetString(ConfigurationKeys.Site);
                var ddSite = string.IsNullOrEmpty(specificSite)
                                 ? DefaultSite
                                 : specificSite;

                DirectLogSubmissionUrl = $"{IntakePrefix}{ddSite}{IntakeSuffix}";
            }

            DirectLogSubmissionMinimumLevel = DirectSubmissionLogLevelExtensions.Parse(
                source?.GetString(ConfigurationKeys.DirectLogSubmission.MinimumLevel), DefaultMinimumLevel);

            var globalTags = source?.GetDictionary(ConfigurationKeys.DirectLogSubmission.GlobalTags)
                          ?? source?.GetDictionary(ConfigurationKeys.GlobalTags)
                             // backwards compatibility for names used in the past
                          ?? source?.GetDictionary("DD_TRACE_GLOBAL_TAGS");

            DirectLogSubmissionGlobalTags = globalTags?.Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
                                                       .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value.Trim())
                                         ?? new Dictionary<string, string>();

            var logSubmissionIntegrations = source?.GetString(ConfigurationKeys.DirectLogSubmission.EnabledIntegrations)
                                                  ?.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries) ??
                                            Enumerable.Empty<string>();
            DirectLogSubmissionEnabledIntegrations = new HashSet<string>(logSubmissionIntegrations, StringComparer.OrdinalIgnoreCase);

            var batchSizeLimit = source?.GetInt32(ConfigurationKeys.DirectLogSubmission.BatchSizeLimit);
            DirectLogSubmissionBatchSizeLimit = batchSizeLimit is null or <= 0
                                                    ? DefaultBatchSizeLimit
                                                    : batchSizeLimit.Value;

            var queueSizeLimit = source?.GetInt32(ConfigurationKeys.DirectLogSubmission.QueueSizeLimit);
            DirectLogSubmissionQueueSizeLimit = queueSizeLimit is null or <= 0
                                                    ? DefaultQueueSizeLimit
                                                    : queueSizeLimit.Value;

            var seconds = source?.GetInt32(ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds);
            DirectLogSubmissionBatchPeriod = TimeSpan.FromSeconds(
                seconds is null or <= 0
                    ? DefaultBatchPeriodSeconds
                    : seconds.Value);

            ApiKey = source?.GetString(ConfigurationKeys.ApiKey);

            LogsInjectionEnabled = source?.GetBool(ConfigurationKeys.LogsInjectionEnabled);
        }

        /// <summary>
        /// Gets or Sets the integrations enabled for direct log submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.EnabledIntegrations" />
        internal HashSet<string> DirectLogSubmissionEnabledIntegrations { get; set; }

        /// <summary>
        /// Gets or Sets the originating host name for direct logs submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Host" />
        internal string DirectLogSubmissionHost { get; set; }

        /// <summary>
        /// Gets or Sets the originating source for direct logs submission
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Source" />
        internal string DirectLogSubmissionSource { get; set; }

        /// <summary>
        /// Gets or sets the global tags, which are applied to all directly submitted logs. If not provided,
        /// <see cref="TracerSettings.GlobalTags"/> are used instead
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.GlobalTags" />
        internal IDictionary<string, string> DirectLogSubmissionGlobalTags { get; set; }

        /// <summary>
        /// Gets or sets the url to send logs to
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Url" />
        internal string? DirectLogSubmissionUrl { get; set; }

        /// <summary>
        /// Gets or sets the minimum level logs should have to be sent to the intake.
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.Url" />
        internal DirectSubmissionLogLevel DirectLogSubmissionMinimumLevel { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of logs to send at one time
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.BatchSizeLimit"/>
        internal int DirectLogSubmissionBatchSizeLimit { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of logs to hold in internal queue at any one time
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.QueueSizeLimit"/>
        internal int DirectLogSubmissionQueueSizeLimit { get; set; }

        /// <summary>
        /// Gets or sets the time to wait between checking for batches
        /// </summary>
        /// <seealso cref="ConfigurationKeys.DirectLogSubmission.BatchPeriodSeconds"/>
        internal TimeSpan DirectLogSubmissionBatchPeriod { get; set; }

        /// <summary>
        /// Gets or sets the Datadog API key
        /// </summary>
        internal string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets whether logs injection has been explicitly enabled or disabled
        /// </summary>
        internal bool? LogsInjectionEnabled { get; set; }
    }
}

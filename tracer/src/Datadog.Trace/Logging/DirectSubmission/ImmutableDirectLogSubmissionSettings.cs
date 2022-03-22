// <copyright file="ImmutableDirectLogSubmissionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission.Sink.PeriodicBatching;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// Contains direct-log-submission-specific settings
    /// </summary>
    internal class ImmutableDirectLogSubmissionSettings
    {
        internal static readonly IntegrationId[] SupportedIntegrations =
        {
            IntegrationId.Serilog,
            IntegrationId.ILogger,
            IntegrationId.Log4Net,
            IntegrationId.NLog,
            IntegrationId.XUnit,
        };

        private readonly bool[] _enabledIntegrations;

        private ImmutableDirectLogSubmissionSettings(
            string host,
            string source,
            string globalTags,
            Uri intakeUrl,
            string apiKey,
            bool isEnabled,
            DirectSubmissionLogLevel minimumLevel,
            bool[] enabledIntegrations,
            List<string> validationErrors,
            List<string> enabledIntegrationNames,
            BatchingSinkOptions batchingOptions)
        {
            Host = host;
            Source = source;
            GlobalTags = globalTags;
            IntakeUrl = intakeUrl;
            ApiKey = apiKey;
            ValidationErrors = validationErrors;
            EnabledIntegrationNames = enabledIntegrationNames;
            MinimumLevel = minimumLevel;
            _enabledIntegrations = enabledIntegrations;
            IsEnabled = isEnabled;
            BatchingOptions = batchingOptions;
        }

        public bool IsEnabled { get; }

        public DirectSubmissionLogLevel MinimumLevel { get; }

        public string Host { get; }

        public string Source { get; }

        public string GlobalTags { get; }

        public Uri IntakeUrl { get; }

        public string ApiKey { get; }

        public List<string> ValidationErrors { get; }

        public List<string> EnabledIntegrationNames { get; }

        public BatchingSinkOptions BatchingOptions { get; }

        public static ImmutableDirectLogSubmissionSettings Create(TracerSettings settings)
            => Create(settings.LogSubmissionSettings);

        public static ImmutableDirectLogSubmissionSettings Create(DirectLogSubmissionSettings settings)
            => Create(
                host: settings.DirectLogSubmissionHost,
                source: settings.DirectLogSubmissionSource,
                intakeUrl: settings.DirectLogSubmissionUrl,
                apiKey: settings.ApiKey,
                minimumLevel: settings.DirectLogSubmissionMinimumLevel,
                globalTags: settings.DirectLogSubmissionGlobalTags,
                enabledLogShippingIntegrations: settings.DirectLogSubmissionEnabledIntegrations,
                batchingOptions: new BatchingSinkOptions(
                    batchSizeLimit: settings.DirectLogSubmissionBatchSizeLimit,
                    queueLimit: settings.DirectLogSubmissionQueueSizeLimit,
                    period: settings.DirectLogSubmissionBatchPeriod));

        public static ImmutableDirectLogSubmissionSettings Create(
            string? host,
            string? source,
            string? intakeUrl,
            string? apiKey,
            DirectSubmissionLogLevel minimumLevel,
            IDictionary<string, string> globalTags,
            ICollection<string> enabledLogShippingIntegrations,
            BatchingSinkOptions batchingOptions)
        {
            if (enabledLogShippingIntegrations.Count == 0)
            {
                // not trying to enable log submission, so don't log any errors and create a _null_ implementation
                return CreateNullSettings();
            }

            var isEnabled = true;
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(host))
            {
                isEnabled = false;
                validationErrors.Add($"Missing required setting '{ConfigurationKeys.DirectLogSubmission.Host}'.");
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                isEnabled = false;
                validationErrors.Add($"Missing required setting '{ConfigurationKeys.DirectLogSubmission.Source}'.");
            }

            if (!Uri.TryCreate(intakeUrl, UriKind.Absolute, out var intakeUri))
            {
                isEnabled = false;
                validationErrors.Add($"The intake url '{intakeUrl}' was not a valid URL.");
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                isEnabled = false;
                validationErrors.Add($"Missing required settings '{ConfigurationKeys.ApiKey}'.");
            }

            var stringifiedTags = StringifyGlobalTags(globalTags);
            var enabledIntegrations = new bool[IntegrationRegistry.Ids.Count];
            var enabledIntegrationNames = new List<string>(SupportedIntegrations.Length);

            foreach (var integrationName in enabledLogShippingIntegrations)
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

                if (!enabledIntegrations[(int)integrationId])
                {
                    enabledIntegrationNames.Add(IntegrationRegistry.GetName(integrationId));
                    enabledIntegrations[(int)integrationId] = true;
                }
            }

            return new ImmutableDirectLogSubmissionSettings(
                host: host ?? string.Empty,
                source: source ?? string.Empty,
                globalTags: stringifiedTags,
                intakeUrl: intakeUri!,
                apiKey: apiKey ?? string.Empty,
                isEnabled: isEnabled,
                minimumLevel: minimumLevel,
                enabledIntegrations: enabledIntegrations,
                validationErrors,
                enabledIntegrationNames,
                batchingOptions);
        }

        private static string StringifyGlobalTags(IDictionary<string, string> globalTags)
        {
            if (globalTags.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var tagPair in globalTags)
            {
                sb.Append(tagPair.Key)
                  .Append(':')
                  .Append(tagPair.Value)
                  .Append(';');
            }

            // remove final joiner
            return sb.ToString(startIndex: 0, length: sb.Length - 1);
        }

        public static ImmutableDirectLogSubmissionSettings CreateNullSettings()
        {
            var emptyList = new List<string>(0);
            // not trying to enable log submission, so don't log any errors and create a _null_ implementation
            return new ImmutableDirectLogSubmissionSettings(
                host: string.Empty,
                source: string.Empty,
                globalTags: string.Empty,
                intakeUrl: new Uri("http://localhost"),
                apiKey: string.Empty,
                isEnabled: false,
                minimumLevel: DirectSubmissionLogLevel.Fatal,
                enabledIntegrations: Array.Empty<bool>(),
                validationErrors: emptyList,
                enabledIntegrationNames: emptyList,
                batchingOptions: new BatchingSinkOptions(batchSizeLimit: 1, queueLimit: 1, TimeSpan.MaxValue));
        }

        public bool IsIntegrationEnabled(IntegrationId integrationId)
        {
            return IsEnabled && _enabledIntegrations[(int)integrationId];
        }
    }
}

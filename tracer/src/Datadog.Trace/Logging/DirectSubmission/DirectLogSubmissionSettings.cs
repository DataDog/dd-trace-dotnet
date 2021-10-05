// <copyright file="DirectLogSubmissionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// Contains direct-log-submission-specific settings
    /// </summary>
    internal class DirectLogSubmissionSettings
    {
        internal static readonly IntegrationId[] SupportedIntegrations =
        {
            IntegrationId.Serilog,
            IntegrationId.ILogger,
            IntegrationId.Log4Net,
            IntegrationId.NLog,
        };

        private readonly bool[] _enabledIntegrations;

        private DirectLogSubmissionSettings(
            string host,
            string source,
            string transport,
            string globalTags,
            Uri intakeUrl,
            string apiKey,
            bool isEnabled,
            DirectSubmissionLogLevel minimumLevel,
            bool[] enabledIntegrations,
            List<string> validationErrors,
            List<string> enabledIntegrationNames)
        {
            Host = host;
            Source = source;
            GlobalTags = globalTags;
            IntakeUrl = intakeUrl;
            ApiKey = apiKey;
            ValidationErrors = validationErrors;
            EnabledIntegrationNames = enabledIntegrationNames;
            MinimumLevel = minimumLevel;
            Transport = transport;
            _enabledIntegrations = enabledIntegrations;
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }

        public DirectSubmissionLogLevel MinimumLevel { get; }

        public string Transport { get; }

        public string Host { get; }

        public string Source { get; }

        public string GlobalTags { get; }

        public Uri IntakeUrl { get; }

        public string ApiKey { get; }

        public List<string> ValidationErrors { get; }

        public List<string> EnabledIntegrationNames { get; }

        public static DirectLogSubmissionSettings Create(
            TracerSettings settings,
            string apiKey,
            ImmutableIntegrationSettingsCollection enabledIntegrations)
            => Create(
                host: settings.DirectLogSubmissionHost,
                source: settings.DirectLogSubmissionSource,
                transport: settings.DirectLogSubmissionTransport,
                intakeUrl: settings.DirectLogSubmissionUrl,
                apiKey: apiKey,
                minimumLevel: settings.DirectLogSubmissionMinimumLevel,
                globalTags: settings.DirectLogSubmissionGlobalTags,
                enabledLogShippingIntegrations: settings.DirectLogSubmissionEnabledIntegrations,
                isLogsInjectionEnabled: settings.LogsInjectionEnabled,
                globallyEnabledIntegrations: enabledIntegrations);

        public static DirectLogSubmissionSettings Create(
            string host,
            string source,
            string transport,
            string intakeUrl,
            string apiKey,
            DirectSubmissionLogLevel minimumLevel,
            IDictionary<string, string> globalTags,
            ICollection<string> enabledLogShippingIntegrations,
            bool isLogsInjectionEnabled,
            ImmutableIntegrationSettingsCollection globallyEnabledIntegrations)
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

            transport = transport?.ToUpperInvariant();
            if (transport is null || !LogsTransportStrategy.ValidTransports.Contains(transport))
            {
                validationErrors.Add($"Unknown transport '{transport}'. Defaulting to HTTP.");
                transport = LogsTransportStrategy.Http;
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
                if (!IntegrationRegistry.Ids.TryGetValue(integrationName, out var integrationId))
                {
                    validationErrors.Add(
                        "Unknown integration: " + integrationName +
                        ". Use a valid logs integration name: " +
                        string.Join(", ", SupportedIntegrations.Select(x => IntegrationRegistry.Names[(int)x])));
                    continue;
                }

                if (!SupportedIntegrations.Contains((IntegrationId)integrationId))
                {
                    validationErrors.Add(
                        "Integration: " + integrationName + " is not a supported direct log submission integration. " +
                        "Use one of " + string.Join(", ", SupportedIntegrations.Select(x => IntegrationRegistry.Names[(int)x])));
                    continue;
                }

                if (globallyEnabledIntegrations[(IntegrationId)integrationId].Enabled == false)
                {
                    validationErrors.Add("Cannot use integration: " + integrationName + ", integration is disabled. ");
                    continue;
                }

                enabledIntegrationNames.Add(integrationName);
                enabledIntegrations[integrationId] = true;
            }

            if (enabledIntegrationNames.Count != 0 && !isLogsInjectionEnabled)
            {
                validationErrors.Add(
                    "Logs injection is not enabled so logs will not be correlated with traces. " +
                    $"Enable logs injection by setting {ConfigurationKeys.LogsInjectionEnabled}=1.");
            }

            return new DirectLogSubmissionSettings(
                host: host,
                source: source,
                transport: transport,
                globalTags: stringifiedTags,
                intakeUrl: intakeUri,
                apiKey: apiKey,
                isEnabled: isEnabled,
                minimumLevel: minimumLevel,
                enabledIntegrations: enabledIntegrations,
                validationErrors,
                enabledIntegrationNames);
        }

        private static string StringifyGlobalTags(IDictionary<string, string> globalTags)
        {
            if (globalTags.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (var tagPair in globalTags)
            {
                sb.Append(tagPair.Key)
                  .Append(':')
                  .Append(tagPair.Value);
            }

            return sb.ToString();
        }

        public static DirectLogSubmissionSettings CreateNullSettings()
        {
            var emptyList = new List<string>(0);
            // not trying to enable log submission, so don't log any errors and create a _null_ implementation
            return new DirectLogSubmissionSettings(
                null,
                null,
                transport: LogsTransportStrategy.Http,
                globalTags: null,
                intakeUrl: new Uri("http://localhost"),
                apiKey: null,
                isEnabled: false,
                minimumLevel: DirectSubmissionLogLevel.Fatal,
                enabledIntegrations: null,
                validationErrors: emptyList,
                enabledIntegrationNames: emptyList);
        }

        public bool IsIntegrationEnabled(IntegrationId integrationId)
        {
            return IsEnabled && _enabledIntegrations[(int)integrationId];
        }
    }
}

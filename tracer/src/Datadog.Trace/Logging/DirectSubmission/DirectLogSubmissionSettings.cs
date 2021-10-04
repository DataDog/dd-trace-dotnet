// <copyright file="DirectLogSubmissionSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Logging.DirectSubmission
{
    /// <summary>
    /// Contains direct-log-submission-specific settings
    /// </summary>
    internal class DirectLogSubmissionSettings
    {
        internal static readonly IntegrationIds[] SupportedIntegrations =
        {
            IntegrationIds.Serilog,
            IntegrationIds.ILogger,
            IntegrationIds.Log4Net,
        };

        private readonly bool[] _enabledIntegrations;

        private DirectLogSubmissionSettings(
            string hostname,
            string source,
            string transport,
            string globalTags,
            Uri intakeUrl,
            string apiKey,
            bool isEnabled,
            string service,
            DirectSubmissionLogLevel minimumLevel,
            bool[] enabledIntegrations,
            List<string> validationErrors,
            List<string> enabledIntegrationNames)
        {
            Hostname = hostname;
            Source = source;
            GlobalTags = globalTags;
            IntakeUrl = intakeUrl;
            ApiKey = apiKey;
            ValidationErrors = validationErrors;
            EnabledIntegrationNames = enabledIntegrationNames;
            MinimumLevel = minimumLevel;
            Service = service;
            Transport = transport;
            _enabledIntegrations = enabledIntegrations;
            IsEnabled = isEnabled;
        }

        public bool IsEnabled { get; }

        public DirectSubmissionLogLevel MinimumLevel { get; }

        public string Transport { get; }

        public string Hostname { get; }

        public string Source { get; }

        public string Service { get; }

        public string GlobalTags { get; }

        public Uri IntakeUrl { get; }

        public string ApiKey { get; }

        public List<string> ValidationErrors { get; }

        public List<string> EnabledIntegrationNames { get; }

        public static DirectLogSubmissionSettings Create(TracerSettings settings, string apiKey, string serviceName)
            => Create(
                hostname: settings.DirectLogSubmissionHostname,
                source: settings.DirectLogSubmissionSource,
                transport: settings.DirectLogSubmissionTransport,
                intakeUrl: settings.DirectLogSubmissionUrl,
                apiKey: apiKey,
                serviceName: serviceName,
                minimumLevel: settings.DirectLogSubmissionMinimumLevel,
                globalTags: settings.DirectLogSubmissionGlobalTags,
                enabledLogShippingIntegrations: settings.DirectLogSubmissionEnabledIntegrations,
                isLogsInjectionEnabled: settings.LogsInjectionEnabled,
                isIntegrationEnabledCallback: integrationInfo => settings.IsIntegrationEnabled(integrationInfo));

        public static DirectLogSubmissionSettings Create(
            string hostname,
            string source,
            string transport,
            string intakeUrl,
            string apiKey,
            string serviceName,
            DirectSubmissionLogLevel minimumLevel,
            IDictionary<string, string> globalTags,
            ICollection<string> enabledLogShippingIntegrations,
            bool isLogsInjectionEnabled,
            Func<IntegrationInfo, bool> isIntegrationEnabledCallback)
        {
            if (enabledLogShippingIntegrations.Count == 0)
            {
                // not trying to enable log submission, so don't log any errors and create a _null_ implementation
                return CreateNullSettings();
            }

            var isEnabled = true;
            var validationErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(hostname))
            {
                isEnabled = false;
                validationErrors.Add($"Missing required setting '{ConfigurationKeys.DirectLogSubmission.Hostname}'.");
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

            // TODO: un-LINQ this?
            var stringifiedTags = globalTags.Count == 0
                                      ? null
                                      : string.Join(",", globalTags.Select(x => $"{x.Key}:{x.Value}"));

            var enabledIntegrations = new bool[IntegrationRegistry.Ids.Count];
            var enabledIntegrationNames = new List<string>(SupportedIntegrations.Length);

            foreach (var integrationName in enabledLogShippingIntegrations)
            {
                if (!IntegrationRegistry.Ids.TryGetValue(integrationName, out var integrationId))
                {
                    validationErrors.Add("Unknown integration: " + integrationName + ". Use a valid logs integration name.");
                    continue;
                }

                if (!SupportedIntegrations.Contains((IntegrationIds)integrationId))
                {
                    validationErrors.Add("Integration: " + integrationName + " is not a supported direct log submission integration.");
                    continue;
                }

                if (!isIntegrationEnabledCallback(new IntegrationInfo(integrationId)))
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
                hostname: hostname,
                source: source,
                transport: transport,
                globalTags: stringifiedTags,
                intakeUrl: intakeUri,
                apiKey: apiKey,
                isEnabled: isEnabled,
                service: serviceName,
                minimumLevel: minimumLevel,
                enabledIntegrations: enabledIntegrations,
                validationErrors,
                enabledIntegrationNames);
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
                service: string.Empty,
                minimumLevel: DirectSubmissionLogLevel.Fatal,
                enabledIntegrations: null,
                validationErrors: emptyList,
                enabledIntegrationNames: emptyList);
        }

        public bool IsIntegrationEnabled(IntegrationIds integrationId)
        {
            return IsEnabled && _enabledIntegrations[(int)integrationId];
        }
    }
}

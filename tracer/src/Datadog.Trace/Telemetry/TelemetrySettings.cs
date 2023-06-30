// <copyright file="TelemetrySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetrySettings
    {
        public TelemetrySettings(
            bool telemetryEnabled,
            string? configurationError,
            AgentlessSettings? agentlessSettings,
            bool agentProxyEnabled,
            TimeSpan heartbeatInterval,
            bool dependencyCollectionEnabled,
            bool v2Enabled,
            bool metricsEnabled,
            bool debugEnabled)
        {
            TelemetryEnabled = telemetryEnabled;
            ConfigurationError = configurationError;
            Agentless = agentlessSettings;
            AgentProxyEnabled = agentProxyEnabled;
            HeartbeatInterval = heartbeatInterval;
            DependencyCollectionEnabled = dependencyCollectionEnabled;
            V2Enabled = v2Enabled;
            MetricsEnabled = metricsEnabled;
            DebugEnabled = debugEnabled;
        }

        /// <summary>
        /// Gets a value indicating whether internal telemetry is enabled
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Telemetry.Enabled"/>
        public bool TelemetryEnabled { get; }

        public string? ConfigurationError { get; }

        public AgentlessSettings? Agentless { get; }

        public TimeSpan HeartbeatInterval { get; }

        public bool AgentProxyEnabled { get; }

        public bool DependencyCollectionEnabled { get; }

        public bool V2Enabled { get; }

        public bool DebugEnabled { get; }

        public bool MetricsEnabled { get; }

        public static TelemetrySettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry)
            => FromSource(source, telemetry, IsAgentAvailable);

        public static TelemetrySettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry, Func<bool?> isAgentAvailable)
        {
            string? configurationError = null;
            var config = new ConfigurationBuilder(source, telemetry);

            // TODO: we already fetch this, so this will overwrite the telemetry.... Need a solution to that...
            var apiKey = config
                        .WithKeys(ConfigurationKeys.ApiKey)
                        .AsRedactedString();

            var haveApiKey = !string.IsNullOrEmpty(apiKey);

            var agentlessEnabled = config
                                  .WithKeys(ConfigurationKeys.Telemetry.AgentlessEnabled)
                                  .AsBool(
                                       defaultValue: haveApiKey, // if there's an API key, we can use agentless mode by default, otherwise we can only use the agent
                                       validator: isEnabled =>
                                       {
                                           if (isEnabled && !haveApiKey)
                                           {
                                               configurationError = "Telemetry configuration error: Agentless mode was enabled, but no API key was available.";
                                               return false;
                                           }

                                           return true;
                                       });

            var agentProxyEnabled = config
                                   .WithKeys(ConfigurationKeys.Telemetry.AgentProxyEnabled)
                                   .AsBool(() => isAgentAvailable() ?? true, validator: null)
                                   .Value;

            // enabled by default if we have any transports
            var telemetryEnabled = config
                                  .WithKeys(ConfigurationKeys.Telemetry.Enabled)
                                  .AsBool(agentlessEnabled || agentProxyEnabled);

            AgentlessSettings? agentless = null;
            if (telemetryEnabled && agentlessEnabled)
            {
                // We have an API key, so try to send directly to intake
                var agentlessUri = config
                                  .WithKeys(ConfigurationKeys.Telemetry.Uri)
                                  .AsString(
                                       getDefaultValue: () =>
                                       {
                                           // use the default intake. Use DD_SITE if provided, otherwise use default
                                           // TODO: we already fetch this, so this will overwrite the telemetry.... Need a solution to that...
                                           var ddSite = config
                                                       .WithKeys(ConfigurationKeys.Site)
                                                       .AsString(
                                                            defaultValue: "datadoghq.com",
                                                            validator: siteFromEnv => !string.IsNullOrEmpty(siteFromEnv));
                                           return $"{TelemetryConstants.TelemetryIntakePrefix}.{ddSite}/";
                                       },
                                       validator: requestedTelemetryUri =>
                                       {
                                           if (string.IsNullOrEmpty(requestedTelemetryUri)
                                            || !Uri.TryCreate(requestedTelemetryUri, UriKind.Absolute, out _))
                                           {
                                               // URI parsing failed
                                               configurationError = configurationError is null
                                                                        ? $"Telemetry configuration error: The provided telemetry Uri '{requestedTelemetryUri}' was not a valid absolute Uri. Using default intake Uri."
                                                                        : configurationError + $", The provided telemetry Uri '{requestedTelemetryUri}' was not a valid absolute Uri. Using default intake Uri.";
                                               return false;
                                           }

                                           return true;
                                       });

                // The uri is already validated in the above code, so this won't fail
                var finalUri = UriHelpers.Combine(new Uri(agentlessUri, UriKind.Absolute), "/");
                agentless = new AgentlessSettings(finalUri, apiKey!);
            }

            var heartbeatInterval = config
                                   .WithKeys(ConfigurationKeys.Telemetry.HeartbeatIntervalSeconds)
                                   .AsDouble(defaultValue: 60, rawInterval => rawInterval is > 0 and <= 3600)
                                   .Value;

            var dependencyCollectionEnabled = config.WithKeys(ConfigurationKeys.Telemetry.DependencyCollectionEnabled).AsBool(true);

            // TODO: Remove when we rollout telemetry v2. As of now, it will be activated by default only in AAS.
            // we already fetch this, so this will overwrite the telemetry.... no biggy as temporary
            var isRunningInAzureAppService = config
                                        .WithKeys(ConfigurationKeys.AzureAppService.AzureAppServicesContextKey)
                                        .AsBool(false);

            var v2Enabled = config.WithKeys(ConfigurationKeys.Telemetry.V2Enabled).AsBool(defaultValue: isRunningInAzureAppService);

            // For testing purposes only
            var debugEnabled = config.WithKeys(ConfigurationKeys.Telemetry.DebugEnabled).AsBool(false);

            // Currently disabled, will be flipped to true in later versions as part of the rollout
            // Also, will require v2 enabled
            var metricsEnabled = config
                                .WithKeys(ConfigurationKeys.Telemetry.MetricsEnabled)
                                .AsBool(
                                     defaultValue: isRunningInAzureAppService,
                                     validator: enabled =>
                                     {
                                         if (v2Enabled || !enabled)
                                         {
                                             return true;
                                         }

                                         configurationError = configurationError is null
                                                                  ? "Telemetry configuration error: Cannot enable telemetry metrics unless telemetry V2 is enabled"
                                                                  : configurationError + ", Cannot enable telemetry metrics unless telemetry V2 is enabled";
                                         return false;
                                     });

            return new TelemetrySettings(
                telemetryEnabled,
                configurationError,
                agentless,
                agentProxyEnabled,
                TimeSpan.FromSeconds(heartbeatInterval),
                dependencyCollectionEnabled,
                v2Enabled,
                metricsEnabled,
                debugEnabled);
        }

        private static bool? IsAgentAvailable()
        {
            // if CIVisibility is enabled and in agentless mode, we probably don't have an agent available
            if (CIVisibility.IsRunning || CIVisibility.Enabled)
            {
                return !CIVisibility.Settings.Agentless;
            }

            return null;
        }

        public class AgentlessSettings
        {
            public AgentlessSettings(Uri agentlessUri, string apiKey)
            {
                AgentlessUri = agentlessUri;
                ApiKey = apiKey;
            }

            /// <summary>
            /// Gets the URL to send agentless telemetry
            /// </summary>
            public Uri AgentlessUri { get; }

            /// <summary>
            /// Gets the api key to use when sending requests to the agentless telemetry intake
            /// </summary>
            public string ApiKey { get; }
        }
    }
}

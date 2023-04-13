// <copyright file="TelemetrySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
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
            TimeSpan heartbeatInterval)
        {
            TelemetryEnabled = telemetryEnabled;
            ConfigurationError = configurationError;
            Agentless = agentlessSettings;
            HeartbeatInterval = heartbeatInterval;
            AgentProxyEnabled = agentProxyEnabled;
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

        public static TelemetrySettings FromDefaultSources()
            => FromSource(GlobalConfigurationSource.Instance, IsAgentAvailable);

        public static TelemetrySettings FromSource(IConfigurationSource source, Func<bool?> isAgentAvailable)
        {
            string? configurationError = null;

            var apiKey = source.GetString(ConfigurationKeys.ApiKey);
            var agentlessExplicitlyEnabled = source.GetBool(ConfigurationKeys.Telemetry.AgentlessEnabled);
            var agentProxyEnabled = source.GetBool(ConfigurationKeys.Telemetry.AgentProxyEnabled)
                                 ?? isAgentAvailable()
                                 ?? true;

            var agentlessEnabled = false;

            if (agentlessExplicitlyEnabled == true)
            {
                if (string.IsNullOrEmpty(apiKey))
                {
                    configurationError = "Telemetry configuration error: Agentless mode was enabled, but no API key was available.";
                }
                else
                {
                    agentlessEnabled = true;
                }
            }
            else if (agentlessExplicitlyEnabled is null)
            {
                // if there's an API key, we can use agentless mode, otherwise we can only use the agent
                agentlessEnabled = !string.IsNullOrEmpty(apiKey);
            }

            // enabled by default if we have any transports
            var telemetryEnabled = source.GetBool(ConfigurationKeys.Telemetry.Enabled)
                                ?? (agentlessEnabled || agentProxyEnabled);

            AgentlessSettings? agentless = null;
            if (telemetryEnabled && agentlessEnabled)
            {
                // We have an API key, so try to send directly to intake
                Uri agentlessUri;

                var requestedTelemetryUri = source.GetString(ConfigurationKeys.Telemetry.Uri);
                if (!string.IsNullOrEmpty(requestedTelemetryUri)
                 && Uri.TryCreate(requestedTelemetryUri, UriKind.Absolute, out var telemetryUri))
                {
                    // telemetry URI provided and well-formed
                    agentlessUri = UriHelpers.Combine(telemetryUri, "/");
                }
                else
                {
                    if (!string.IsNullOrEmpty(requestedTelemetryUri))
                    {
                        // URI parsing failed
                        configurationError = configurationError is null
                                                 ? $"Telemetry configuration error: The provided telemetry Uri '{requestedTelemetryUri}' was not a valid absolute Uri. Using default intake Uri."
                                                 : configurationError + ", The provided telemetry Uri '{requestedTelemetryUri}' was not a valid absolute Uri. Using default intake Uri.";
                    }

                    // use the default intake. Use DD_SITE if provided, otherwise use default
                    var siteFromEnv = source.GetString(ConfigurationKeys.Site);
                    var ddSite = string.IsNullOrEmpty(siteFromEnv) ? "datadoghq.com" : siteFromEnv;
                    agentlessUri = new Uri($"{TelemetryConstants.TelemetryIntakePrefix}.{ddSite}/");
                }

                agentless = new AgentlessSettings(agentlessUri, apiKey!);
            }

            var rawInterval = source.GetInt32(ConfigurationKeys.Telemetry.HeartbeatIntervalSeconds);
            var heartbeatInterval = rawInterval is { } interval and > 0 and <= 3600 ? interval : 60;

            return new TelemetrySettings(telemetryEnabled, configurationError, agentless, agentProxyEnabled, TimeSpan.FromSeconds(heartbeatInterval));
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

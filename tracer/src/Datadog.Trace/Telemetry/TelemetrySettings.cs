// <copyright file="TelemetrySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
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
            bool metricsEnabled,
            bool debugEnabled)
        {
            TelemetryEnabled = telemetryEnabled;
            ConfigurationError = configurationError;
            Agentless = agentlessSettings;
            AgentProxyEnabled = agentProxyEnabled;
            HeartbeatInterval = heartbeatInterval;
            DependencyCollectionEnabled = dependencyCollectionEnabled;
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

        public bool DebugEnabled { get; }

        public bool MetricsEnabled { get; }

        public static TelemetrySettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry, ImmutableTracerSettings tracerSettings, bool? isAgentAvailable)
            => FromSource(source, telemetry, isAgentAvailable, isServerless: tracerSettings.LambdaMetadata.IsRunningInLambda || tracerSettings.IsRunningMiniAgentInAzureFunctions || tracerSettings.IsRunningInGCPFunctions);

        public static TelemetrySettings FromSource(IConfigurationSource source, IConfigurationTelemetry telemetry, bool? isAgentAvailable, bool isServerless)
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
                                   .AsBool(isAgentAvailable ?? true);

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
                agentless = AgentlessSettings.Create(finalUri, apiKey!);
            }

            var heartbeatInterval = config
                                   .WithKeys(ConfigurationKeys.Telemetry.HeartbeatIntervalSeconds)
                                   .AsDouble(defaultValue: 60, rawInterval => rawInterval is > 0 and <= 3600)
                                   .Value;

            var dependencyCollectionEnabled = config.WithKeys(ConfigurationKeys.Telemetry.DependencyCollectionEnabled).AsBool(true);

            // For testing purposes only
            var debugEnabled = config.WithKeys(ConfigurationKeys.Telemetry.DebugEnabled).AsBool(false);

            bool metricsEnabled;
            if (isServerless)
            {
                // disable metrics by default in serverless, because we can't guarantee the correctness
                metricsEnabled = false;
                telemetry.Record(ConfigurationKeys.Telemetry.MetricsEnabled, false, ConfigurationOrigins.Default);
            }
            else
            {
                metricsEnabled = config
                                .WithKeys(ConfigurationKeys.Telemetry.MetricsEnabled)
                                .AsBool(defaultValue: true);
            }

            return new TelemetrySettings(
                telemetryEnabled,
                configurationError,
                agentless,
                agentProxyEnabled,
                TimeSpan.FromSeconds(heartbeatInterval),
                dependencyCollectionEnabled,
                metricsEnabled,
                debugEnabled);
        }

        public class AgentlessSettings
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AgentlessSettings"/> class.
            /// For testing only, prefer using <see cref="Create"/> instead
            /// </summary>
            public AgentlessSettings(
                Uri agentlessUri,
                string apiKey,
                CloudSettings? cloudSettings)
            {
                AgentlessUri = agentlessUri;
                ApiKey = apiKey;
                Cloud = cloudSettings;
            }

            /// <summary>
            /// Gets the URL to send agentless telemetry
            /// </summary>
            public Uri AgentlessUri { get; }

            /// <summary>
            /// Gets the api key to use when sending requests to the agentless telemetry intake
            /// </summary>
            public string ApiKey { get; }

            public CloudSettings? Cloud { get; }

            public static AgentlessSettings Create(Uri agentlessUri, string apiKey)
            {
                CloudSettings? cloud = null;
                if (EnvironmentHelpers.GetEnvironmentVariable(TelemetryConstants.GcpServiceVariable) is { Length: >0 } gcp)
                {
                    cloud = new("GCP", "GCPCloudRun", gcp);
                }
                else if (EnvironmentHelpers.GetEnvironmentVariable(TelemetryConstants.AzureContainerAppVariable) is { Length: >0 } aca)
                {
                    cloud = new("Azure", "AzureContainerApp", aca);
                }
                else if (!string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(TelemetryConstants.AzureAppServiceVariable1))
                    || !string.IsNullOrEmpty(EnvironmentHelpers.GetEnvironmentVariable(TelemetryConstants.AzureAppServiceVariable2)))
                {
                    cloud = new("Azure", "AzureAppService", EnvironmentHelpers.GetEnvironmentVariable(TelemetryConstants.AzureAppServiceIdentifierVariable));
                }

                // TODO: Handle AWS Lambda. We don't currently have a good way to get the ARN as the identifier so skip for now

                return new AgentlessSettings(agentlessUri, apiKey, cloud);
            }

            public class CloudSettings
            {
                public CloudSettings(
                    string provider,
                    string resourceType,
                    string? resourceIdentifier)
                {
                    Provider = provider;
                    ResourceType = resourceType;
                    ResourceIdentifier = resourceIdentifier;
                }

                public string Provider { get; }

                public string ResourceType { get; }

                public string? ResourceIdentifier { get; }
            }
        }
    }
}

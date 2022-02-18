// <copyright file="TelemetrySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetrySettings
    {
        public TelemetrySettings(IConfigurationSource source, ImmutableTracerSettings tracerSettings)
        {
            var explicitlyEnabled = source?.GetBool(ConfigurationKeys.Telemetry.Enabled);
            TelemetryEnabled = explicitlyEnabled ?? false;

            var apiKey = source?.GetString(ConfigurationKeys.ApiKey);

            if (explicitlyEnabled != false && !string.IsNullOrEmpty(apiKey))
            {
                // We have an API key, so try to send directly to intake
                ApiKey = apiKey;
                TelemetryEnabled = true;

                var requestedTelemetryUri = source?.GetString(ConfigurationKeys.Telemetry.Uri);
                if (!string.IsNullOrEmpty(requestedTelemetryUri)
                 && Uri.TryCreate(requestedTelemetryUri, UriKind.Absolute, out var telemetryUri))
                {
                    // telemetry URI provided and well-formed
                    TelemetryUri = telemetryUri;
                }
                else
                {
                    // use the default intake. Use DD_SITE if provided, otherwise use default
                    var siteFromEnv = source.GetString(ConfigurationKeys.Site);
                    var ddSite = string.IsNullOrEmpty(siteFromEnv) ? "datadoghq.com" : siteFromEnv;
                    TelemetryUri = new Uri($"{TelemetryConstants.TelemetryIntakePrefix}.{ddSite}/");
                }
            }
            else if (TelemetryEnabled)
            {
                // no API key provided, so send to the agent instead
                // We only support http at the moment so disable telemetry for now if we're using something else
                if (tracerSettings.Exporter.TracesTransport == TracesTransportType.Default)
                {
                    TelemetryUri = new Uri(tracerSettings.Exporter.AgentUri, TelemetryConstants.AgentTelemetryEndpoint);
                }
                else
                {
                    TelemetryEnabled = false;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether internal telemetry is enabled
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Telemetry.Enabled"/>
        public bool TelemetryEnabled { get; }

        /// <summary>
        /// Gets a value indicating the URL where telemetry should be sent
        /// </summary>
        /// <seealso cref="ConfigurationKeys.Telemetry.Uri"/>
        public Uri TelemetryUri { get; }

        /// <summary>
        /// Gets the api key to use when sending requests to the telemetry intake
        /// </summary>
        public string ApiKey { get; }

        public static TelemetrySettings FromDefaultSources(ImmutableTracerSettings tracerSettings)
            => new TelemetrySettings(GlobalSettings.CreateDefaultConfigurationSource(), tracerSettings);
    }
}

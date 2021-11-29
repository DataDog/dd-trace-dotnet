// <copyright file="TelemetrySettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry
{
    internal class TelemetrySettings
    {
        public TelemetrySettings(IConfigurationSource source, ImmutableTracerSettings tracerSettings)
        {
            TelemetryEnabled = source?.GetBool(ConfigurationKeys.Telemetry.Enabled) ??
                               // default value
                               true;

            var requestedTelemetryUri = source?.GetString(ConfigurationKeys.Telemetry.Uri);

            TelemetryUrl = !string.IsNullOrEmpty(requestedTelemetryUri) && Uri.TryCreate(requestedTelemetryUri, UriKind.Absolute, out var telemetryUri)
                               ? telemetryUri
                               : new Uri(tracerSettings.AgentUri, TelemetryConstants.AgentTelemetryEndpoint);
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
        public Uri TelemetryUrl { get; }

        public static TelemetrySettings FromDefaultSources(ImmutableTracerSettings tracerSettings)
            => new TelemetrySettings(GlobalSettings.CreateDefaultConfigurationSource(), tracerSettings);
    }
}

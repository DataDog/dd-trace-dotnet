// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains integration-specific settings.
    /// </summary>
    public class IntegrationSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettings"/> class.
        /// </summary>
        /// <param name="integrationName">The integration name.</param>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <param name="isExplicitlyDisabled">Has the integration been explicitly disabled</param>
        /// <param name="fallback">The fallback values to use. Only used in manual instrumentation scenarios</param>
        internal IntegrationSettings(string integrationName, IConfigurationSource? source, bool isExplicitlyDisabled, IntegrationSettings? fallback = null)
        {
            if (integrationName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(integrationName));
            }

            IntegrationName = integrationName;

            // We don't record these in telemetry, because they're blocked anyway
            var config = new ConfigurationBuilder(source ?? NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
            var upperName = integrationName.ToUpperInvariant();
            Enabled = isExplicitlyDisabled ? false : (config
                                                  .WithKeys(
                                                       string.Format(ConfigurationKeys.Integrations.Enabled, upperName),
                                                       string.Format(ConfigurationKeys.Integrations.Enabled, integrationName),
                                                       $"DD_{integrationName}_ENABLED")
                                                  .AsBool()
                                                   ?? fallback?.Enabled);

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = config
                              .WithKeys(
                                   string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, upperName),
                                   string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName),
                                   $"DD_{integrationName}_ANALYTICS_ENABLED")
                              .AsBool()
                            ?? fallback?.AnalyticsEnabled;

            AnalyticsSampleRate = config
                                 .WithKeys(
                                      string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, upperName),
                                      string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName),
                                      $"DD_{integrationName}_ANALYTICS_SAMPLE_RATE")
                                 .AsDouble(fallback?.AnalyticsSampleRate ?? 1.0);
#pragma warning restore 618
        }

        /// <summary>
        /// Gets the name of the integration. Used to retrieve integration-specific settings.
        /// </summary>
        public string IntegrationName { get; }

        /// <summary>
        /// Gets a value indicating whether
        /// this integration is enabled.
        /// </summary>
        public bool? Enabled { get; }

        /// <summary>
        /// Gets a value indicating whether
        /// Analytics are enabled for this integration.
        /// </summary>
        public bool? AnalyticsEnabled { get; }

        /// <summary>
        /// Gets a value between 0 and 1 (inclusive)
        /// that determines the sampling rate for this integration.
        /// </summary>
        public double AnalyticsSampleRate { get; }
    }
}

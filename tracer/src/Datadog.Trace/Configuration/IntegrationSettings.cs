// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains integration-specific settings.
    /// </summary>
    public partial class IntegrationSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettings"/> class.
        /// </summary>
        /// <param name="integrationName">The integration name.</param>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        [PublicApi]
        public IntegrationSettings(string integrationName, IConfigurationSource? source)
            : this(integrationName, source, false)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_Ctor);
        }

        internal IntegrationSettings(string integrationName, IConfigurationSource? source, bool unusedParamNotToUsePublicApi)
        {
            // unused parameter is to give us a non-public API we can use
            if (integrationName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(integrationName));
            }

            IntegrationName = integrationName;

            if (source == null)
            {
                return;
            }

            // We don't record these in telemetry, because they're blocked anyway
            var config = new ConfigurationBuilder(source, NullConfigurationTelemetry.Instance);
            var upperName = integrationName.ToUpperInvariant();
            Enabled = config
                     .WithKeys(
                          string.Format(ConfigurationKeys.Integrations.Enabled, upperName),
                          string.Format(ConfigurationKeys.Integrations.Enabled, integrationName),
                          $"DD_{integrationName}_ENABLED")
                     .AsBool();

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = config
                              .WithKeys(
                                   string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, upperName),
                                   string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName),
                                   $"DD_{integrationName}_ANALYTICS_ENABLED")
                              .AsBool();

            AnalyticsSampleRate = config
                                 .WithKeys(
                                      string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, upperName),
                                      string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName),
                                      $"DD_{integrationName}_ANALYTICS_SAMPLE_RATE")
                                 .AsDouble(1.0);
#pragma warning restore 618
        }

        /// <summary>
        /// Gets the name of the integration. Used to retrieve integration-specific settings.
        /// </summary>
        public string IntegrationName { get; }

        /// <summary>
        /// Gets or sets a value indicating whether
        /// this integration is enabled.
        /// </summary>
        public bool? Enabled { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether
        /// Analytics are enabled for this integration.
        /// </summary>
        public bool? AnalyticsEnabled { get; set; }

        /// <summary>
        /// Gets or sets a value between 0 and 1 (inclusive)
        /// that determines the sampling rate for this integration.
        /// </summary>
        public double AnalyticsSampleRate { get; set; }
    }
}

// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

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
        [PublicApi]
        public IntegrationSettings(string integrationName, IConfigurationSource? source)
            : this(integrationName, source, TelemetryFactoryV2.GetConfigTelemetry())
        {
        }

        internal IntegrationSettings(string integrationName, IConfigurationSource? source, IConfigurationTelemetry telemetry)
        {
            if (integrationName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(integrationName));
            }

            IntegrationName = integrationName;

            if (source == null)
            {
                return;
            }

            var config = new ConfigurationBuilder(source, telemetry);
            Enabled = config
                     .WithKeys(
                          string.Format(ConfigurationKeys.Integrations.Enabled, integrationName),
                          string.Format("DD_{0}_ENABLED", integrationName))
                     .AsBool()
                     .Get();

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = config
                              .WithKeys(
                                   string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName),
                                   string.Format("DD_{0}_ANALYTICS_ENABLED", integrationName))
                              .AsBool()
                              .Get();

            AnalyticsSampleRate = config
                                 .WithKeys(
                                      string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName),
                                      string.Format("DD_{0}_ANALYTICS_SAMPLE_RATE", integrationName))
                                 .AsDouble()
                                 .Get(1.0);
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

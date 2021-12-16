// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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
        public IntegrationSettings(string integrationName, IConfigurationSource source)
        {
            ThrowHelper.ThrowArgumentNullExceptionIfNull(integrationName);

            IntegrationName = integrationName;

            if (source == null)
            {
                return;
            }

            Enabled = source.GetBool(string.Format(ConfigurationKeys.Integrations.Enabled, integrationName)) ??
                      source.GetBool(string.Format("DD_{0}_ENABLED", integrationName));

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = source.GetBool(string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName)) ??
                               source.GetBool(string.Format("DD_{0}_ANALYTICS_ENABLED", integrationName));

            AnalyticsSampleRate = source.GetDouble(string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName)) ??
                                  source.GetDouble(string.Format("DD_{0}_ANALYTICS_SAMPLE_RATE", integrationName)) ??
                                  // default value
                                  1.0;
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

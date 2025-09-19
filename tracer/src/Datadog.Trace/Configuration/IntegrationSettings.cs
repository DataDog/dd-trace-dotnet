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
        internal IntegrationSettings(string? integrationName, IConfigurationSource? source, bool isExplicitlyDisabled)
        {
            if (integrationName is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(integrationName));
            }

            IntegrationName = integrationName;

            // We don't record these in telemetry, because they're blocked anyway
            var config = new ConfigurationBuilder(source ?? NullConfigurationSource.Instance, NullConfigurationTelemetry.Instance);
            Enabled = isExplicitlyDisabled ? false : config
                                                  .WithIntegrationKey(integrationName)
                                                  .AsBool();

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = config
                              .WithIntegrationAnalyticsKey(integrationName)
                              .AsBool();

            AnalyticsSampleRate = config
                                 .WithIntegrationAnalyticsSampleRateKey(integrationName)
                                 .AsDouble(1.0);
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

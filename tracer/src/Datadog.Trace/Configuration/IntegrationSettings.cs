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

            IntegrationNameInternal = integrationName;

            if (source == null)
            {
                return;
            }

            // We don't record these in telemetry, because they're blocked anyway
            var config = new ConfigurationBuilder(source, NullConfigurationTelemetry.Instance);
            EnabledInternal = config
                     .WithKeys(
                          string.Format(ConfigurationKeys.Integrations.Enabled, integrationName),
                          string.Format("DD_{0}_ENABLED", integrationName))
                     .AsBool();

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabledInternal = config
                              .WithKeys(
                                   string.Format(ConfigurationKeys.Integrations.AnalyticsEnabled, integrationName),
                                   string.Format("DD_{0}_ANALYTICS_ENABLED", integrationName))
                              .AsBool();

            AnalyticsSampleRateInternal = config
                                 .WithKeys(
                                      string.Format(ConfigurationKeys.Integrations.AnalyticsSampleRate, integrationName),
                                      string.Format("DD_{0}_ANALYTICS_SAMPLE_RATE", integrationName))
                                 .AsDouble(1.0);
#pragma warning restore 618
        }

        /// <summary>
        /// Gets the name of the integration. Used to retrieve integration-specific settings.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.IntegrationSettings_IntegrationName_Get)]
        internal string IntegrationNameInternal { get; }

#pragma warning disable SA1624 // Documentation summary should begin with "Gets" - the documentation is primarily for public property
        /// <summary>
        /// Gets or sets a value indicating whether
        /// this integration is enabled.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.IntegrationSettings_Enabled_Get,
            PublicApiUsage.IntegrationSettings_Enabled_Set)]
        internal bool? EnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether
        /// Analytics are enabled for this integration.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Get,
            PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set)]
        internal bool? AnalyticsEnabledInternal { get; set; }

        /// <summary>
        /// Gets or sets a value between 0 and 1 (inclusive)
        /// that determines the sampling rate for this integration.
        /// </summary>
        [GeneratePublicApi(
            PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Get,
            PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Set)]
        internal double AnalyticsSampleRateInternal { get; set; }
#pragma warning restore SA1624
    }
}

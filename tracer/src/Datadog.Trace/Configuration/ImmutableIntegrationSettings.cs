// <copyright file="ImmutableIntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains integration-specific settings.
    /// </summary>
    public partial class ImmutableIntegrationSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableIntegrationSettings"/> class from an instance of
        /// <see cref="IntegrationSettings"/>.
        /// </summary>
        /// <param name="settings">The values to use.</param>
        /// <param name="isExplicitlyDisabled">If true forces the setting Enabled = false. Otherwise, uses <see cref="IntegrationSettings.EnabledInternal"/></param>
        internal ImmutableIntegrationSettings(IntegrationSettings settings, bool isExplicitlyDisabled)
        {
            IntegrationNameInternal = settings.IntegrationNameInternal;
            EnabledInternal = isExplicitlyDisabled ? false : settings.EnabledInternal;
            AnalyticsEnabledInternal = settings.AnalyticsEnabledInternal;
            AnalyticsSampleRateInternal = settings.AnalyticsSampleRateInternal;
        }

        internal ImmutableIntegrationSettings(string name)
        {
            IntegrationNameInternal = name;
        }

        /// <summary>
        /// Gets the name of the integration. Used to retrieve integration-specific settings.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableIntegrationSettings_IntegrationName_Get)]
        internal string IntegrationNameInternal { get; }

        /// <summary>
        /// Gets a value indicating whether
        /// this integration is enabled.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableIntegrationSettings_Enabled_Get)]
        internal bool? EnabledInternal { get; }

        /// <summary>
        /// Gets a value indicating whether
        /// Analytics are enabled for this integration.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableIntegrationSettings_AnalyticsEnabled_Get)]
        internal bool? AnalyticsEnabledInternal { get; }

        /// <summary>
        /// Gets a value between 0 and 1 (inclusive)
        /// that determines the sampling rate for this integration.
        /// </summary>
        [GeneratePublicApi(PublicApiUsage.ImmutableIntegrationSettings_AnalyticsSampleRate_Get)]
        internal double AnalyticsSampleRateInternal { get; }
    }
}

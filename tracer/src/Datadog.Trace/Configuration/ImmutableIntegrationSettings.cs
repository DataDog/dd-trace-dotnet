// <copyright file="ImmutableIntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains integration-specific settings.
    /// </summary>
    public class ImmutableIntegrationSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableIntegrationSettings"/> class from an instance of
        /// <see cref="IntegrationSettings"/>.
        /// </summary>
        /// <param name="settings">The values to use.</param>
        /// <param name="isExplicitlyDisabled">If true forces the setting Enabled = false. Otherwise, uses <see cref="IntegrationSettings.Enabled"/></param>
        internal ImmutableIntegrationSettings(IntegrationSettings settings, bool isExplicitlyDisabled)
        {
            IntegrationName = settings.IntegrationName;
            Enabled = isExplicitlyDisabled ? false : settings.Enabled;
            AnalyticsEnabled = settings.AnalyticsEnabled;
            AnalyticsSampleRate = settings.AnalyticsSampleRate;
        }

        internal ImmutableIntegrationSettings(string name)
        {
            IntegrationName = name;
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

        /// <summary>
        /// Gets the <c>ImmutableIntegrationSettings</c>  as a string.
        /// </summary>
        /// <remarks>
        /// To simplify viewing in the debugger.
        /// </remarks>
        /// <returns>
        /// The object in a human readable format.
        /// </returns>
        public override string ToString() => $"{this.IntegrationName} - {(this.Enabled == true ? "Enabled" : "Disabled")} - Analytics: {(this.AnalyticsEnabled == true ? $"Sample Rate {this.AnalyticsSampleRate}" : "Disabled")}";
    }
}

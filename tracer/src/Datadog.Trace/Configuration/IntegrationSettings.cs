// <copyright file="IntegrationSettings.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Contains integration-specific settings.
    /// </summary>
    public class IntegrationSettings : IEquatable<IntegrationSettings>
    {
        /// <summary>
        /// Configuration key pattern for enabling or disabling an integration.
        /// </summary>
        public const string IntegrationEnabled = "DD_TRACE_{0}_ENABLED";

        /// <summary>
        /// Configuration key pattern for enabling or disabling Analytics in an integration.
        /// </summary>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        public const string AnalyticsEnabledKey = "DD_TRACE_{0}_ANALYTICS_ENABLED";

        /// <summary>
        /// Configuration key pattern for setting Analytics sampling rate in an integration.
        /// </summary>
        [Obsolete(DeprecationMessages.AppAnalytics)]
        public const string AnalyticsSampleRateKey = "DD_TRACE_{0}_ANALYTICS_SAMPLE_RATE";

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettings"/> class.
        /// </summary>
        /// <param name="integrationName">The integration name. Callers shouldn't pass a null value, but as it's available in Datadog.Trace.Manual, we still need to check</param>
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
            Enabled = isExplicitlyDisabled
                          ? false
                          : config
                           .WithKeys(
                                string.Format(IntegrationEnabled, upperName),
                                string.Format(IntegrationEnabled, integrationName),
                                $"DD_{integrationName}_ENABLED")
                           .AsBool()
                         ?? fallback?.Enabled;

#pragma warning disable 618 // App analytics is deprecated, but still used
            AnalyticsEnabled = config
                              .WithKeys(
                                   string.Format(AnalyticsEnabledKey, upperName),
                                   string.Format(AnalyticsEnabledKey, integrationName),
                                   $"DD_{integrationName}_ANALYTICS_ENABLED")
                              .AsBool()
                            ?? fallback?.AnalyticsEnabled;

            AnalyticsSampleRate = config
                                 .WithKeys(
                                      string.Format(AnalyticsSampleRateKey, upperName),
                                      string.Format(AnalyticsSampleRateKey, integrationName),
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

        /// <inheritdoc/>
        public bool Equals(IntegrationSettings? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return IntegrationName == other.IntegrationName &&
                   Enabled == other.Enabled &&
                   AnalyticsEnabled == other.AnalyticsEnabled &&
                   AnalyticsSampleRate.Equals(other.AnalyticsSampleRate);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((IntegrationSettings)obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(IntegrationName, Enabled, AnalyticsEnabled, AnalyticsSampleRate);
        }
    }
}

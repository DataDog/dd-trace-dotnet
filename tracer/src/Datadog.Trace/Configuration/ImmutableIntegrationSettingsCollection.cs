// <copyright file="ImmutableIntegrationSettingsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="ImmutableIntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public class ImmutableIntegrationSettingsCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<ImmutableIntegrationSettingsCollection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableIntegrationSettingsCollection"/> class.
        /// </summary>
        /// <param name="settings">The <see cref="IntegrationSettingsCollection"/> to populate the immutable settings.</param>
        /// <param name="disabledIntegrationNames">Additional integrations that should be disabled</param>
        internal ImmutableIntegrationSettingsCollection(
            IntegrationSettingsCollection settings,
            HashSet<string> disabledIntegrationNames)
        {
            Settings = GetIntegrationSettingsById(settings, disabledIntegrationNames);
        }

        internal ImmutableIntegrationSettings[] Settings { get; }

        /// <summary>
        /// Gets the <see cref="ImmutableIntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        [PublicApi]
        public ImmutableIntegrationSettings this[string integrationName]
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.ImmutableIntegrationSettingsCollection_Indexer_Name);
                if (IntegrationRegistry.TryGetIntegrationId(integrationName, out var integrationId))
                {
                    return Settings[(int)integrationId];
                }

                Log.Warning(
                    "Accessed integration settings for unknown integration {IntegrationName}. Returning default settings",
                    integrationName);

                return new ImmutableIntegrationSettings(integrationName);
            }
        }

        internal ImmutableIntegrationSettings this[IntegrationId integration]
            => Settings[(int)integration];

        private static ImmutableIntegrationSettings[] GetIntegrationSettingsById(
            IntegrationSettingsCollection settings,
            HashSet<string> disabledIntegrationNames)
        {
            var allExistingSettings = settings.Settings;
            var integrations = new ImmutableIntegrationSettings[allExistingSettings.Length];

            for (int i = 0; i < integrations.Length; i++)
            {
                var existingSettings = allExistingSettings[i];
                var explicitlyDisabled = disabledIntegrationNames.Contains(existingSettings.IntegrationNameInternal);

                integrations[i] = new ImmutableIntegrationSettings(existingSettings, explicitlyDisabled);
            }

            return integrations;
        }
    }
}

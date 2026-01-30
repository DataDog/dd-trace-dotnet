// <copyright file="IntegrationSettingsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="IntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public sealed class IntegrationSettingsCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<IntegrationSettingsCollection>();

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettingsCollection"/> class.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        /// <param name="disabledIntegrationNames">Integrations already disabled by name</param>
        /// <param name="fallback">Fallback values to use. Only used in manual instrumentation</param>
        internal IntegrationSettingsCollection(IConfigurationSource source, HashSet<string> disabledIntegrationNames, IntegrationSettingsCollection? fallback = null)
        {
            Settings = GetIntegrationSettings(source, disabledIntegrationNames, fallback);
        }

        internal IntegrationSettings[] Settings { get; }

        /// <summary>
        /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        public IntegrationSettings this[string integrationName]
        {
            get
            {
                if (IntegrationRegistry.TryGetIntegrationId(integrationName, out var integrationId))
                {
                    return Settings[(int)integrationId];
                }

                Log.Warning(
                    "Accessed integration settings for unknown integration {IntegrationName}. " +
                    "Returning default settings, changes will not be saved",
                    integrationName);

                return new IntegrationSettings(integrationName, source: null, false);
            }
        }

        internal IntegrationSettings this[IntegrationId integration]
            => Settings[(int)integration];

        private static IntegrationSettings[] GetIntegrationSettings(IConfigurationSource source, HashSet<string> disabledIntegrationNames, IntegrationSettingsCollection? fallbackValues)
        {
            var integrations = new IntegrationSettings[IntegrationRegistry.Names.Length];

            for (var i = 0; i < integrations.Length; i++)
            {
                var name = IntegrationRegistry.Names[i];

                if (name != null)
                {
                    var explicitlyDisabled = disabledIntegrationNames.Contains(name);
                    integrations[i] = new IntegrationSettings(name, source, explicitlyDisabled, fallbackValues?[name]);
                }
            }

            return integrations;
        }
    }
}

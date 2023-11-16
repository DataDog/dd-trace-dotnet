// <copyright file="IntegrationSettingsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="IntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public class IntegrationSettingsCollection
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<IntegrationSettingsCollection>();
        private readonly IntegrationSettings[] _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettingsCollection"/> class.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        [PublicApi]
        public IntegrationSettingsCollection(IConfigurationSource source)
            : this(source, false)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettingsCollection_Ctor_Source);
        }

        internal IntegrationSettingsCollection(IConfigurationSource source, bool unusedParamNotToUsePublicApi)
        {
            _settings = GetIntegrationSettings(source);
        }

        internal IntegrationSettings[] Settings => _settings;

        /// <summary>
        /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        [PublicApi]
        public IntegrationSettings this[string integrationName]
        {
            get
            {
                TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettingsCollection_Indexer_Name);
                if (IntegrationRegistry.TryGetIntegrationId(integrationName, out var integrationId))
                {
                    return _settings[(int)integrationId];
                }

                Log.Warning(
                    "Accessed integration settings for unknown integration {IntegrationName}. " +
                    "Returning default settings, changes will not be saved",
                    integrationName);

                return new IntegrationSettings(integrationName, source: null, false);
            }
        }

        private static IntegrationSettings[] GetIntegrationSettings(IConfigurationSource source)
        {
            var integrations = new IntegrationSettings[IntegrationRegistry.Names.Length];

            for (int i = 0; i < integrations.Length; i++)
            {
                var name = IntegrationRegistry.Names[i];

                if (name != null)
                {
                    integrations[i] = new IntegrationSettings(name, source, false);
                }
            }

            return integrations;
        }
    }
}

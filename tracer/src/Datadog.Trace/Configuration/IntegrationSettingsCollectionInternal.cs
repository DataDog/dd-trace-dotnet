// <copyright file="IntegrationSettingsCollectionInternal.cs" company="Datadog">
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
    /// A collection of <see cref="IntegrationSettingsInternal"/> instances, referenced by name.
    /// </summary>
    public class IntegrationSettingsCollectionInternal
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<IntegrationSettingsCollectionInternal>();
        private readonly IntegrationSettingsInternal[] _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettingsCollectionInternal"/> class.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        [PublicApi]
        public IntegrationSettingsCollectionInternal(IConfigurationSource source)
            : this(source, false)
        {
            TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettingsCollection_Ctor_Source);
        }

        internal IntegrationSettingsCollectionInternal(IConfigurationSource source, bool unusedParamNotToUsePublicApi)
        {
            _settings = GetIntegrationSettings(source);
        }

        internal IntegrationSettingsInternal[] Settings => _settings;

        /// <summary>
        /// Gets the <see cref="IntegrationSettingsInternal"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        [PublicApi]
        public IntegrationSettingsInternal this[string integrationName]
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

                return new IntegrationSettingsInternal(integrationName, source: null, false);
            }
        }

        private static IntegrationSettingsInternal[] GetIntegrationSettings(IConfigurationSource source)
        {
            var integrations = new IntegrationSettingsInternal[IntegrationRegistry.Names.Length];

            for (int i = 0; i < integrations.Length; i++)
            {
                var name = IntegrationRegistry.Names[i];

                if (name != null)
                {
                    integrations[i] = new IntegrationSettingsInternal(name, source, false);
                }
            }

            return integrations;
        }
    }
}

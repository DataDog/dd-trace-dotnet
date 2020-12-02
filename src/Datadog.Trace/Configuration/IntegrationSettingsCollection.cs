using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="IntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public class IntegrationSettingsCollection
    {
        private readonly IConfigurationSource _source;
        private readonly ConcurrentDictionary<string, IntegrationSettings> _settingsByName;
        private readonly Func<string, IntegrationSettings> _valueFactory;
        private readonly IntegrationSettings[] _settingsById;
        private ICollection<string> _disabledIntegrations;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettingsCollection"/> class.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public IntegrationSettingsCollection(IConfigurationSource source)
        {
            _source = source;
            _settingsByName = new ConcurrentDictionary<string, IntegrationSettings>();
            _settingsById = GetIntegrationSettings(source);
            _valueFactory = name =>
            {
                if (IntegrationRegistry.Ids.TryGetValue(name, out var id))
                {
                    return _settingsById[id];
                }

                // We have no id for this integration, it will only be available in _settingsByName
                var settings = new IntegrationSettings(name, _source);

                if (_disabledIntegrations?.Contains(name) == true)
                {
                    settings.Enabled = false;
                }

                return settings;
            };
        }

        /// <summary>
        /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        public IntegrationSettings this[string integrationName] => this[new IntegrationInfo(integrationName)];

        internal IntegrationSettings this[IntegrationInfo integration]
        {
            get
            {
                return integration.Name == null ? _settingsById[integration.Id] : _settingsByName.GetOrAdd(integration.Name, _valueFactory);
            }
        }

        internal void SetDisabledIntegrations(HashSet<string> disabledIntegrationNames)
        {
            if (disabledIntegrationNames == null || disabledIntegrationNames.Count == 0)
            {
                return;
            }

            _disabledIntegrations = disabledIntegrationNames;

            foreach (var settings in _settingsById.Concat(_settingsByName.Values))
            {
                if (disabledIntegrationNames.Contains(settings.IntegrationName))
                {
                    settings.Enabled = false;
                }
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
                    integrations[i] = new IntegrationSettings(name, source);
                }
            }

            return integrations;
        }
    }
}

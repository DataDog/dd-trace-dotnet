// <copyright file="ImmutableIntegrationSettingsCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="ImmutableIntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public class ImmutableIntegrationSettingsCollection
    {
        private readonly IConfigurationSource _source;
        private readonly HashSet<string> _disabledIntegrations;
        private readonly ConcurrentDictionary<string, ImmutableIntegrationSettings> _settingsByName;
        private readonly Func<string, ImmutableIntegrationSettings> _valueFactory;
        private readonly ImmutableIntegrationSettings[] _settingsById;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmutableIntegrationSettingsCollection"/> class.
        /// </summary>
        /// <param name="settings">The <see cref="IntegrationSettingsCollection"/> to populate the immutable settings.</param>
        /// <param name="disabledIntegrationNames">Additional integrations that should be disabled</param>
        public ImmutableIntegrationSettingsCollection(
            IntegrationSettingsCollection settings,
            HashSet<string> disabledIntegrationNames)
        {
            _source = settings.Source;
            _disabledIntegrations = disabledIntegrationNames ?? throw new ArgumentNullException(nameof(disabledIntegrationNames));
            _settingsById = GetIntegrationSettingsById(settings, disabledIntegrationNames);
            _settingsByName = GetIntegrationSettingsByName(settings, disabledIntegrationNames);
            _valueFactory = name =>
            {
                if (IntegrationRegistry.Ids.TryGetValue(name, out var id))
                {
                    return _settingsById[id];
                }

                // We have no id for this integration, it will only be available in _settingsByName
                var initialSettings = new IntegrationSettings(name, _source);
                var isExplicitlyDisabled = _disabledIntegrations.Contains(name);

                return new ImmutableIntegrationSettings(initialSettings, isExplicitlyDisabled);
            };
        }

        internal ICollection<string> DisabledIntegrations => _disabledIntegrations;

        /// <summary>
        /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        public ImmutableIntegrationSettings this[string integrationName] => this[new IntegrationInfo(integrationName)];

        internal ImmutableIntegrationSettings this[IntegrationInfo integration]
        {
            get
            {
                return integration.Name == null
                           ? _settingsById[integration.Id]
                           : _settingsByName.GetOrAdd(integration.Name, _valueFactory);
            }
        }

        private static ImmutableIntegrationSettings[] GetIntegrationSettingsById(
            IntegrationSettingsCollection settings,
            HashSet<string> disabledIntegrationNames)
        {
            var allExistingSettings = settings.SettingsById;
            var integrations = new ImmutableIntegrationSettings[allExistingSettings.Length];

            for (int i = 0; i < integrations.Length; i++)
            {
                var existingSettings = allExistingSettings[i];
                var explicitlyDisabled = disabledIntegrationNames.Contains(existingSettings.IntegrationName);

                integrations[i] = new ImmutableIntegrationSettings(existingSettings, explicitlyDisabled);
            }

            return integrations;
        }

        private static ConcurrentDictionary<string, ImmutableIntegrationSettings> GetIntegrationSettingsByName(
            IntegrationSettingsCollection settings,
            HashSet<string> disabledIntegrationNames)
        {
            var existingSettings = settings
                                  .SettingsByName
                                  .Values
                                  .Select(
                                       setting => new KeyValuePair<string, ImmutableIntegrationSettings>(
                                           setting.IntegrationName,
                                           new ImmutableIntegrationSettings(setting, disabledIntegrationNames.Contains(setting.IntegrationName))));

            return new ConcurrentDictionary<string, ImmutableIntegrationSettings>(existingSettings);
        }
    }
}

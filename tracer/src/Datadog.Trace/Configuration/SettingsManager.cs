// <copyright file="SettingsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;

namespace Datadog.Trace.Configuration;

public sealed partial record TracerSettings
{
    internal sealed class SettingsManager
    {
        private readonly TracerSettings _tracerSettings;
        private readonly List<SettingChangeSubscription> _subscribers = [];

        private IConfigurationSource _dynamicConfigurationSource = NullConfigurationSource.Instance;
        private ManualInstrumentationConfigurationSourceBase _manualConfigurationSource =
            new ManualInstrumentationConfigurationSource(new Dictionary<string, object?>(), useDefaultSources: true);

        // We delay creating these, as we likely won't need them
        private MutableSettings? _noDefaultSourcesSettings;

        private SettingChanges? _latest;

        public SettingsManager(IConfigurationSource source, TracerSettings tracerSettings, OverrideErrorLog errorLog)
        {
            InitialMutableSettings = MutableSettings.CreateInitialMutableSettings(source, errorLog, tracerSettings);
            _tracerSettings = tracerSettings;
        }

        /// <summary>
        /// Gets the initial <see cref="MutableSettings"/>. On app startup, these will be the values read from
        /// static sources. To subscribe to updates to these settings, from code or remote config, call <see cref="SubscribeToChanges"/>.
        /// </summary>
        public MutableSettings InitialMutableSettings { get; }

        /// <summary>
        /// Subscribe to changes in <see cref="MutableSettings"/>.
        /// <paramref name="callback"/> is called whenever these settings change. If the settings have already changed when <see cref="SubscribeToChanges"/>
        /// is called, <paramref name="callback"/> is synchronously invoked immediately with the latest configuration.
        /// Also note that calling <see cref="SubscribeToChanges"/> twice with the same callback
        /// will invoke the callback twice. Callbacks should complete quickly to avoid blocking other operations.
        /// </summary>
        /// <param name="callback">The method to invoke</param>
        /// <returns>An <see cref="IDisposable"/> that should be disposed to unsubscribe</returns>
        public IDisposable SubscribeToChanges(Action<SettingChanges> callback)
        {
            var subscription = new SettingChangeSubscription(this, callback);
            lock (_subscribers)
            {
                _subscribers.Add(subscription);

                if (_latest is { } currentConfig)
                {
                    try
                    {
                        // If we already have updates, call this immediately
                        callback(currentConfig);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error notifying subscriber of updated MutableSettings during subscribe");
                    }
                }
            }

            return subscription;
        }

        /// <summary>
        /// Regenerate the application's new <see cref="MutableSettings"/>
        /// based on runtime configuration sources.
        /// </summary>
        /// <param name="manualSource">An <see cref="IConfigurationSource"/> containing the new settings created by manual configuration (in code)</param>
        /// <returns>True if changes were detected and consumers were updated, false otherwise</returns>
        public bool UpdateManualConfigurationSettings(
            ManualInstrumentationConfigurationSourceBase manualSource)
        {
            // we lock this whole method so that we can't conflict with UpdateDynamicConfigurationSettings calls too
            lock (_subscribers)
            {
                _manualConfigurationSource = manualSource;
                return UpdateSettings(_dynamicConfigurationSource, manualSource);
            }
        }

        /// <summary>
        /// Regenerate the application's new <see cref="MutableSettings"/>
        /// based on runtime configuration sources.
        /// </summary>
        /// <param name="dynamicConfigSource">An <see cref="IConfigurationSource"/> for dynamic config via remote config</param>
        /// <returns>True if changes were detected and consumers were updated, false otherwise</returns>
        public bool UpdateDynamicConfigurationSettings(
            IConfigurationSource dynamicConfigSource)
        {
            lock (_subscribers)
            {
                _dynamicConfigurationSource = dynamicConfigSource;
                return UpdateSettings(dynamicConfigSource, _manualConfigurationSource);
            }
        }

        private bool UpdateSettings(
            IConfigurationSource dynamicConfigSource,
            ManualInstrumentationConfigurationSourceBase manualSource)
        {
            if (BuildNewSettings(dynamicConfigSource, manualSource) is { } newSettings)
            {
                NotifySubscribers(newSettings);
                return true;
            }

            return false;
        }

        // Internal for testing
        internal SettingChanges? BuildNewSettings(
            IConfigurationSource dynamicConfigSource,
            ManualInstrumentationConfigurationSourceBase manualSource)
        {
            // Set the correct initial settings depending on whether the manual config source explicitly disables using the default sources
            MutableSettings initialSettings;
            if (manualSource.UseDefaultSources)
            {
                initialSettings = InitialMutableSettings;
            }
            else
            {
                // We only need to initialize the "no default sources" settings once
                // and we don't want to initialize them if we don't _need_ to
                // so lazy-initialize here
                if (_noDefaultSourcesSettings is null)
                {
                    InitialiseNoDefaultSourceSettings();
                }

                initialSettings = _noDefaultSourcesSettings;
            }

            var current = _latest;
            var currentMutable = current?.UpdatedMutable ?? current?.PreviousMutable ?? InitialMutableSettings;

            var overrideErrorLog = new OverrideErrorLog();
            var newMutableSettings = MutableSettings.CreateUpdatedMutableSettings(
                dynamicConfigSource,
                manualSource,
                initialSettings,
                _tracerSettings,
                overrideErrorLog);

            var isSameMutableSettings = currentMutable.Equals(newMutableSettings);

            if (isSameMutableSettings)
            {
                Log.Debug("No changes detected in the new configuration");
                return null;
            }

            Log.Information("Notifying consumers of new settings");
            var updatedMutableSettings = isSameMutableSettings ? null : newMutableSettings;

            return new SettingChanges(updatedMutableSettings, currentMutable);
        }

        [MemberNotNull(nameof(_noDefaultSourcesSettings))]
        private void InitialiseNoDefaultSourceSettings()
        {
            if (_noDefaultSourcesSettings is not null)
            {
                return;
            }

            _noDefaultSourcesSettings = MutableSettings.CreateWithoutDefaultSources(_tracerSettings);
        }

        private void NotifySubscribers(SettingChanges settings)
        {
            _latest = settings;

            foreach (var subscriber in _subscribers)
            {
                try
                {
                    subscriber.Notify(settings);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error notifying subscriber of MutableSettings change");
                }
            }
        }

        private sealed class SettingChangeSubscription(SettingsManager owner, Action<SettingChanges> notify) : IDisposable
        {
            private readonly SettingsManager _owner = owner;

            public Action<SettingChanges> Notify { get; } = notify;

            public void Dispose()
            {
                lock (_owner._subscribers)
                {
                    _owner._subscribers.Remove(this);
                }
            }
        }

        public sealed class SettingChanges(MutableSettings? updatedMutable, MutableSettings previousMutable)
        {
            /// <summary>
            /// Gets the new <see cref="MutableSettings"/>, if they have changed.
            /// If there are no changes, returns null.
            /// </summary>
            public MutableSettings? UpdatedMutable { get; } = updatedMutable;

            /// <summary>
            /// Gets the previous <see cref="MutableSettings"/>, prior to this update.
            /// </summary>
            public MutableSettings PreviousMutable { get; } = previousMutable;
        }
    }
}

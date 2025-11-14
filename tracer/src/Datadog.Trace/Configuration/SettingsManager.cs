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
using Datadog.Trace.Configuration.Telemetry;

namespace Datadog.Trace.Configuration;

public partial record TracerSettings
{
    internal class SettingsManager
    {
        private readonly TracerSettings _tracerSettings;
        private readonly ConfigurationTelemetry _initialTelemetry;
        private readonly List<SettingChangeSubscription> _subscribers = [];

        private IConfigurationSource _dynamicConfigurationSource = NullConfigurationSource.Instance;
        private ManualInstrumentationConfigurationSourceBase _manualConfigurationSource =
            new ManualInstrumentationConfigurationSource(new Dictionary<string, object?>(), useDefaultSources: true);

        // We delay creating these, as we likely won't need them
        private ConfigurationTelemetry? _noDefaultSettingsTelemetry;
        private MutableSettings? _noDefaultSourcesSettings;

        private SettingChanges? _latest;

        public SettingsManager(IConfigurationSource source, TracerSettings tracerSettings, IConfigurationTelemetry telemetry, OverrideErrorLog errorLog)
        {
            // We record the telemetry for the initial settings in a dedicated ConfigurationTelemetry,
            // because we need to be able to reapply this configuration on dynamic config updates
            // We don't re-record error logs, so we just use the built-in for that
            var initialTelemetry = new ConfigurationTelemetry();
            InitialMutableSettings = MutableSettings.CreateInitialMutableSettings(source, initialTelemetry, errorLog, tracerSettings);
            InitialExporterSettings = new ExporterSettings(source, initialTelemetry);
            _tracerSettings = tracerSettings;
            _initialTelemetry = initialTelemetry;
            initialTelemetry.CopyTo(telemetry);
        }

        /// <summary>
        /// Gets the initial <see cref="MutableSettings"/>. On app startup, these will be the values read from
        /// static sources. To subscribe to updates to these settings, from code or remote config, call <see cref="SubscribeToChanges"/>.
        /// </summary>
        public MutableSettings InitialMutableSettings { get; }

        /// <summary>
        /// Gets the initial <see cref="ExporterSettings"/>. On app startup, these will be the values read from
        /// static sources. To subscribe to updates to these settings, from code or remote config, call <see cref="SubscribeToChanges"/>.
        /// </summary>
        public ExporterSettings InitialExporterSettings { get; }

        /// <summary>
        /// Subscribe to changes in <see cref="MutableSettings"/> and/or <see cref="ExporterSettings"/>.
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
        /// Regenerate the application's new <see cref="MutableSettings"/> and <see cref="ExporterSettings"/>
        /// based on runtime configuration sources.
        /// </summary>
        /// <param name="manualSource">An <see cref="IConfigurationSource"/> containing the new settings created by manual configuration (in code)</param>
        /// <param name="centralTelemetry">The central <see cref="IConfigurationTelemetry"/> to report config telemetry updates to</param>
        /// <returns>True if changes were detected and consumers were updated, false otherwise</returns>
        public bool UpdateManualConfigurationSettings(
            ManualInstrumentationConfigurationSourceBase manualSource,
            IConfigurationTelemetry centralTelemetry)
        {
            // we lock this whole method so that we can't conflict with UpdateDynamicConfigurationSettings calls too
            lock (_subscribers)
            {
                _manualConfigurationSource = manualSource;
                return UpdateSettings(_dynamicConfigurationSource, manualSource, centralTelemetry);
            }
        }

        /// <summary>
        /// Regenerate the application's new <see cref="MutableSettings"/> and <see cref="ExporterSettings"/>
        /// based on runtime configuration sources.
        /// </summary>
        /// <param name="dynamicConfigSource">An <see cref="IConfigurationSource"/> for dynamic config via remote config</param>
        /// <param name="centralTelemetry">The central <see cref="IConfigurationTelemetry"/> to report config telemetry updates to</param>
        /// <returns>True if changes were detected and consumers were updated, false otherwise</returns>
        public bool UpdateDynamicConfigurationSettings(
            IConfigurationSource dynamicConfigSource,
            IConfigurationTelemetry centralTelemetry)
        {
            lock (_subscribers)
            {
                _dynamicConfigurationSource = dynamicConfigSource;
                return UpdateSettings(dynamicConfigSource, _manualConfigurationSource, centralTelemetry);
            }
        }

        private bool UpdateSettings(
            IConfigurationSource dynamicConfigSource,
            ManualInstrumentationConfigurationSourceBase manualSource,
            IConfigurationTelemetry centralTelemetry)
        {
            if (BuildNewSettings(dynamicConfigSource, manualSource, centralTelemetry) is { } newSettings)
            {
                NotifySubscribers(newSettings);
                return true;
            }

            return false;
        }

        // Internal for testing
        internal SettingChanges? BuildNewSettings(
            IConfigurationSource dynamicConfigSource,
            ManualInstrumentationConfigurationSourceBase manualSource,
            IConfigurationTelemetry telemetry)
        {
            // Set the correct default telemetry and initial settings depending
            // on whether the manual config source explicitly disables using the default sources
            ConfigurationTelemetry defaultTelemetry;
            MutableSettings initialSettings;
            if (manualSource.UseDefaultSources)
            {
                defaultTelemetry = _initialTelemetry;
                initialSettings = InitialMutableSettings;
            }
            else
            {
                // We only need to initialize the "no default sources" settings once
                // and we don't want to initialize them if we don't _need_ to
                // so lazy-initialize here
                if (_noDefaultSourcesSettings is null || _noDefaultSettingsTelemetry is null)
                {
                    InitialiseNoDefaultSourceSettings();
                }

                defaultTelemetry = _noDefaultSettingsTelemetry;
                initialSettings = _noDefaultSourcesSettings;
            }

            var current = _latest;
            var currentMutable = current?.UpdatedMutable ?? current?.PreviousMutable ?? InitialMutableSettings;
            var currentExporter = current?.UpdatedExporter ?? current?.PreviousExporter ?? InitialExporterSettings;

            // we create a temporary ConfigurationTelemetry object to hold the changes to settings
            // if nothing is actually written, and nothing changes compared to the default, then we
            // don't need to report it to the provided telemetry
            var tempTelemetry = new ConfigurationTelemetry();

            var overrideErrorLog = new OverrideErrorLog();
            var newMutableSettings = MutableSettings.CreateUpdatedMutableSettings(
                dynamicConfigSource,
                manualSource,
                initialSettings,
                _tracerSettings,
                tempTelemetry,
                overrideErrorLog); // TODO: We'll later report these

            // The only exporter setting we currently _allow_ to change is the AgentUri, but if that does change,
            // it can mean that _everything_ about the exporter settings changes. To minimize the work to do, and
            // to simplify comparisons, we try to read the agent url from the manual setting. If it's missing, not
            // set, or unchanged, there's no need to update the exporter settings.
            // We only technically need to do this today if _manual_ config changes, not if remote config changes,
            // but for simplicity we don't distinguish currently.
            var newRawExporterSettings = ExporterSettings.Raw.CreateUpdatedFromManualConfig(
                currentExporter.RawSettings,
                manualSource,
                tempTelemetry,
                manualSource.UseDefaultSources);

            var isSameMutableSettings = currentMutable.Equals(newMutableSettings);
            var isSameExporterSettings = currentExporter.RawSettings.Equals(newRawExporterSettings);

            if (isSameMutableSettings && isSameExporterSettings)
            {
                Log.Debug("No changes detected in the new configuration");
                return null;
            }

            // we have changes, so we need to report them
            // First record the "default"/fallback values, then record the "new" values
            defaultTelemetry.CopyTo(telemetry);
            tempTelemetry.CopyTo(telemetry);

            Log.Information("Notifying consumers of new settings");
            var updatedMutableSettings = isSameMutableSettings ? null : newMutableSettings;
            var updatedExporterSettings = isSameExporterSettings ? null : new ExporterSettings(newRawExporterSettings, telemetry);

            return new SettingChanges(updatedMutableSettings, updatedExporterSettings, currentMutable, currentExporter);
        }

        [MemberNotNull(nameof(_noDefaultSettingsTelemetry))]
        [MemberNotNull(nameof(_noDefaultSourcesSettings))]
        private void InitialiseNoDefaultSourceSettings()
        {
            if (_noDefaultSourcesSettings is not null
             && _noDefaultSettingsTelemetry is not null)
            {
                return;
            }

            var telemetry = new ConfigurationTelemetry();
            _noDefaultSettingsTelemetry = telemetry;
            _noDefaultSourcesSettings = MutableSettings.CreateWithoutDefaultSources(_tracerSettings, telemetry);
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

        public sealed class SettingChanges(MutableSettings? updatedMutable, ExporterSettings? updatedExporter, MutableSettings previousMutable, ExporterSettings previousExporter)
        {
            /// <summary>
            /// Gets the new <see cref="MutableSettings"/>, if they have changed.
            /// If there are no changes, returns null.
            /// </summary>
            public MutableSettings? UpdatedMutable { get; } = updatedMutable;

            /// <summary>
            /// Gets the new <see cref="ExporterSettings"/>, if they have changed.
            /// If there are no changes, returns null.
            /// </summary>
            public ExporterSettings? UpdatedExporter { get; } = updatedExporter;

            /// <summary>
            /// Gets the previous <see cref="MutableSettings"/>, prior to this update.
            /// </summary>
            public MutableSettings PreviousMutable { get; } = previousMutable;

            /// <summary>
            /// Gets the previous <see cref="ExporterSettings"/>, prior to this update.
            /// </summary>
            public ExporterSettings PreviousExporter { get; } = previousExporter;
        }
    }
}

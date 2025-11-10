// <copyright file="SettingsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Configuration;

public partial record TracerSettings
{
    internal class SettingsManager(
        TracerSettings tracerSettings,
        MutableSettings initialMutable,
        ExporterSettings initialExporter)
    {
        private readonly TracerSettings _tracerSettings = tracerSettings;
        private readonly List<SettingChangeSubscription> _subscribers = [];
        private SettingChanges? _latest;

        /// <summary>
        /// Gets the initial <see cref="MutableSettings"/>. On app startup, these will be the values read from
        /// static sources. To subscribe to updates to these settings, from code or remote config, call <see cref="SubscribeToChanges"/>.
        /// </summary>
        public MutableSettings InitialMutableSettings { get; } = initialMutable;

        /// <summary>
        /// Gets the initial <see cref="ExporterSettings"/>. On app startup, these will be the values read from
        /// static sources. To subscribe to updates to these settings, from code or remote config, call <see cref="SubscribeToChanges"/>.
        /// </summary>
        public ExporterSettings InitialExporterSettings { get; } = initialExporter;

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
        /// <param name="dynamicConfigSource">An <see cref="IConfigurationSource"/> for dynamic config via remote config</param>
        /// <param name="manualSource">An <see cref="IConfigurationSource"/> for manual configuration (in code)</param>
        /// <param name="centralTelemetry">The central <see cref="IConfigurationTelemetry"/> to report config telemetry updates to</param>
        /// <returns>True if changes were detected and consumers were updated, false otherwise</returns>
        public bool UpdateSettings(
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
            IConfigurationTelemetry centralTelemetry)
        {
            var initialSettings = manualSource.UseDefaultSources
                                      ? InitialMutableSettings
                                      : MutableSettings.CreateWithoutDefaultSources(_tracerSettings);

            var current = _latest;
            var currentMutable = current?.UpdatedMutable ?? current?.PreviousMutable ?? InitialMutableSettings;
            var currentExporter = current?.UpdatedExporter ?? current?.PreviousExporter ?? InitialExporterSettings;

            var telemetry = new ConfigurationTelemetry();
            var newMutableSettings = MutableSettings.CreateUpdatedMutableSettings(
                dynamicConfigSource,
                manualSource,
                initialSettings,
                _tracerSettings,
                telemetry,
                new OverrideErrorLog()); // TODO: We'll later report these

            // The only exporter setting we currently _allow_ to change is the AgentUri, but if that does change,
            // it can mean that _everything_ about the exporter settings changes. To minimize the work to do, and
            // to simplify comparisons, we try to read the agent url from the manual setting. If it's missing, not
            // set, or unchanged, there's no need to update the exporter settings.
            // We only technically need to do this today if _manual_ config changes, not if remote config changes,
            // but for simplicity we don't distinguish currently.
            var exporterTelemetry = new ConfigurationTelemetry();
            var newRawExporterSettings = ExporterSettings.Raw.CreateUpdatedFromManualConfig(
                currentExporter.RawSettings,
                manualSource,
                exporterTelemetry,
                manualSource.UseDefaultSources);

            var isSameMutableSettings = currentMutable.Equals(newMutableSettings);
            var isSameExporterSettings = currentExporter.RawSettings.Equals(newRawExporterSettings);

            if (isSameMutableSettings && isSameExporterSettings)
            {
                Log.Debug("No changes detected in the new configuration");
                // Even though there were no "real" changes, there may be _effective_ changes in telemetry that
                // need to be recorded (e.g. the customer set the value in code, but it was already set via
                // env vars). We _should_ record exporter settings too, but that introduces a bunch of complexity
                // which we'll resolve later anyway, so just have that gap for now (it's very niche).
                // If there are changes, they're recorded automatically in ConfigureInternal
                telemetry.CopyTo(centralTelemetry);
                return null;
            }

            Log.Information("Notifying consumers of new settings");
            var updatedMutableSettings = isSameMutableSettings ? null : newMutableSettings;
            var updatedExporterSettings = isSameExporterSettings ? null : new ExporterSettings(newRawExporterSettings, exporterTelemetry);

            return new SettingChanges(updatedMutableSettings, updatedExporterSettings, currentMutable, currentExporter);
        }

        private void NotifySubscribers(SettingChanges settings)
        {
            // Strictly, for safety, we only need to lock in the subscribers list access. However,
            // there's nothing to prevent NotifySubscribers being called concurrently,
            // which could result in weird out-of-order notifications for customers. So for simplicity
            // we just lock the whole method to ensure serialized updates.

            lock (_subscribers)
            {
                Volatile.Write(ref _latest, settings);

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

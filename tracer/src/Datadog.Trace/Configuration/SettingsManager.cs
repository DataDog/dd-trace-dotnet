// <copyright file="SettingsManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Processors;
using Datadog.Trace.Serverless;

namespace Datadog.Trace.Configuration;

public sealed partial record TracerSettings
{
    internal sealed class SettingsManager
    {
        private readonly TracerSettings _tracerSettings;
        private readonly ConfigurationTelemetry _initialTelemetry;
        private readonly ServerlessCompatPipeNames _pipeNames;
        private readonly List<SettingChangeSubscription> _subscribers = [];
        private readonly object _exporterPublicationGate = new();

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

            // Build Raw once here so we can both pass it into ExporterSettings and read
            // ServerlessCompatPath to decide whether to generate pipe names. Pipe names are
            // generated at most once per process and reused on every reconfig so the compat
            // layer continues to see the same name.
            var initialRawExporterSettings = new ExporterSettings.Raw(source, initialTelemetry);
            _pipeNames = CreatePipeNames(initialRawExporterSettings.ServerlessCompatPath);
            InitialExporterSettings = new ExporterSettings(initialRawExporterSettings, initialTelemetry, _pipeNames);

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

        internal MutableSettings CurrentMutableSettings => Volatile.Read(ref _latest)?.EffectiveMutable ?? InitialMutableSettings;

        internal long CurrentRemoteConfigurationTargetGeneration => Volatile.Read(ref _latest)?.RemoteConfigurationTargetGeneration ?? 0;

        internal ExporterSettings CurrentExporterSettings
        {
            get
            {
                var latest = Volatile.Read(ref _latest);
                return latest?.EffectiveExporter ?? InitialExporterSettings;
            }
        }

        internal bool TryRunWithCurrentAgentConfiguration(ExporterSettings exporter, IDiscoveryService discoveryService, Func<AgentConfiguration, bool> apply)
        {
            lock (_exporterPublicationGate)
            {
                return ReferenceEquals(CurrentExporterSettings, exporter) && discoveryService.TryRunWithCurrentConfiguration(apply);
            }
        }

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
                if (BuildNewSettings(_dynamicConfigurationSource, manualSource) is not { } prepared)
                {
                    return false;
                }

                PublishSettings(prepared.Changes);
                CompletePublication(prepared, centralTelemetry);
                return true;
            }
        }

        /// <summary>
        /// Regenerate the application's new <see cref="MutableSettings"/> and <see cref="ExporterSettings"/>
        /// based on runtime configuration sources.
        /// </summary>
        /// <param name="dynamicConfigSource">An <see cref="IConfigurationSource"/> for dynamic config via remote config</param>
        /// <param name="centralTelemetry">The central <see cref="IConfigurationTelemetry"/> to report config telemetry updates to</param>
        /// <param name="tryCommit">An optional guard that publishes the settings while the caller still owns the route.</param>
        /// <param name="updated">Whether the effective settings changed.</param>
        /// <returns>Whether the caller could install the configuration.</returns>
        internal bool TryUpdateDynamicConfigurationSettings(
            IConfigurationSource dynamicConfigSource,
            IConfigurationTelemetry centralTelemetry,
            Func<Action, bool>? tryCommit,
            out bool updated)
        {
            lock (_subscribers)
            {
                var prepared = BuildNewSettings(dynamicConfigSource, _manualConfigurationSource);
                if (tryCommit is null)
                {
                    Publish();
                }
                else if (!tryCommit(Publish))
                {
                    updated = false;
                    return false;
                }

                updated = prepared.HasValue;
                if (prepared is { } committed)
                {
                    CompletePublication(committed, centralTelemetry);
                }

                return true;

                void Publish()
                {
                    _dynamicConfigurationSource = dynamicConfigSource;
                    if (prepared is { } settings)
                    {
                        PublishSettings(settings.Changes);
                    }
                }
            }
        }

        private static ServerlessCompatPipeNames CreatePipeNames(string? compatPathOverride)
        {
            // Named pipes are Windows-only; short-circuit on other platforms before touching AzureInfo or the disk.
            if (!FrameworkDescription.Instance.IsWindows()
                || !AzureInfo.Instance.IsAzureFunction
                || AzureInfo.Instance.IsUsingSiteExtension
                || !ServerlessCompatPipeNameHelper.IsCompatLayerAvailableWithPipeSupport(compatPathOverride))
            {
                return ServerlessCompatPipeNames.None;
            }

            var names = new ServerlessCompatPipeNames(
                TracesPipeName: $"dd_trace_{Guid.NewGuid():N}",
                MetricsPipeName: $"dd_dogstatsd_{Guid.NewGuid():N}");

            Log.Information(
                "Azure Functions environment detected with Serverless Compatibility Layer. " +
                "Generated unique pipe names — traces: {TracesPipeName}, metrics: {MetricsPipeName}",
                names.TracesPipeName,
                names.MetricsPipeName);

            return names;
        }

        private static bool HasSameRemoteConfigurationTarget(MutableSettings previous, MutableSettings updated)
        {
            if ((previous.ServiceVersion ?? string.Empty) != (updated.ServiceVersion ?? string.Empty)
             || !HaveSameTag(previous.DefaultServiceName, updated.DefaultServiceName)
             || !HaveSameTag(previous.Environment, updated.Environment))
            {
                return false;
            }

            var before = previous.ProcessTags;
            var after = updated.ProcessTags;
            if (ReferenceEquals(before, after))
            {
                return true;
            }

            IEnumerable<string> previousTags = before is null ? Array.Empty<string>() : before.TagsList;
            IEnumerable<string> updatedTags = after is null ? Array.Empty<string>() : after.TagsList;
            return previousTags.SequenceEqual(updatedTags);

            static bool HaveSameTag(string? previous, string? updated)
                => previous == updated || (TraceUtil.NormalizeTag(previous) ?? string.Empty) == (TraceUtil.NormalizeTag(updated) ?? string.Empty);
        }

        private PreparedSettings? BuildNewSettings(
            IConfigurationSource dynamicConfigSource,
            ManualInstrumentationConfigurationSourceBase manualSource)
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
            var currentMutable = current?.EffectiveMutable ?? InitialMutableSettings;
            var currentExporter = current?.EffectiveExporter ?? InitialExporterSettings;

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

            var updatedMutableSettings = isSameMutableSettings ? null : newMutableSettings;
            var updatedExporterSettings = isSameExporterSettings ? null : new ExporterSettings(newRawExporterSettings, tempTelemetry, _pipeNames);

            var targetGeneration = current?.RemoteConfigurationTargetGeneration ?? 0;
            if (updatedMutableSettings is not null && !HasSameRemoteConfigurationTarget(currentMutable, updatedMutableSettings))
            {
                targetGeneration++;
            }

            var changes = new SettingChanges(updatedMutableSettings, updatedExporterSettings, currentMutable, currentExporter)
            {
                RemoteConfigurationTargetGeneration = targetGeneration,
            };
            return new PreparedSettings(changes, defaultTelemetry, tempTelemetry);
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

        private void PublishSettings(SettingChanges settings)
        {
            lock (_exporterPublicationGate)
            {
                Volatile.Write(ref _latest, settings);
            }
        }

        private void CompletePublication(PreparedSettings prepared, IConfigurationTelemetry telemetry)
        {
            // CopyTo updates the sequence IDs of reusable fallback entries.
            prepared.DefaultTelemetry.CopyTo(telemetry);
            prepared.UpdateTelemetry.CopyTo(telemetry);

            // Subscribers can acquire the Debugger route lock. Notify them outside the publication guards.
            Log.Information("Notifying consumers of new settings");
            foreach (var subscriber in _subscribers)
            {
                try
                {
                    subscriber.Notify(prepared.Changes);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error notifying subscriber of MutableSettings change");
                }
            }
        }

        private readonly record struct PreparedSettings(
            SettingChanges Changes,
            ConfigurationTelemetry DefaultTelemetry,
            ConfigurationTelemetry UpdateTelemetry);

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

            internal long RemoteConfigurationTargetGeneration { get; init; }

            internal MutableSettings EffectiveMutable => UpdatedMutable ?? PreviousMutable;

            internal ExporterSettings EffectiveExporter => UpdatedExporter ?? PreviousExporter;
        }
    }
}

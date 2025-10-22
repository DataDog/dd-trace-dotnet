// <copyright file="DynamicConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Configurations;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Configuration
{
    internal class DynamicConfigurationManager : IDynamicConfigurationManager
    {
        internal const string ProductName = "APM_TRACING";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DynamicConfigurationManager>();

        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly Dictionary<string, RemoteConfiguration> _activeConfigurations = new();
        private ISubscription? _subscription;

        public DynamicConfigurationManager(IRcmSubscriptionManager subscriptionManager)
        {
            _subscriptionManager = subscriptionManager;
        }

        public void Start()
        {
            if (Interlocked.Exchange(ref _subscription, new Subscription(ConfigurationUpdated, ProductName)) == null)
            {
                _subscriptionManager.SubscribeToChanges(_subscription!);

                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingCustomTags, true);                          // 15
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingHttpHeaderTags, true);                      // 14
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingLogsInjection, true);                       // 13
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, true);                          // 12
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true);                      // 19
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRules, true);                         // 29
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingEnabledDynamicInstrumentation, true);       // 38
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingEnableExceptionReplay, true);               // 39
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingEnableCodeOrigin, true);                    // 40
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingEnableLiveDebugging, true);                 // 41
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingMulticonfig, true);                         // 45
            }
        }

        public void Dispose()
        {
            if (_subscription != null)
            {
                _subscriptionManager.Unsubscribe(_subscription);
            }
        }

        internal static void OnlyForTests_ApplyConfiguration(IConfigurationSource dynamicConfig)
        {
            OnConfigurationChanged(dynamicConfig);
        }

        private static void OnConfigurationChanged(IConfigurationSource dynamicConfig)
        {
            var manualConfig = GlobalConfigurationSource.ManualConfigurationSource;

            // We save this immediately, even if there's no manifest changes in the final settings
            // so that it can be picked up by other configuration updaters, e.g. config in code
            GlobalConfigurationSource.UpdateDynamicConfigConfigurationSource(dynamicConfig);

            var wasUpdated = Tracer.Instance.TracerManager.SettingsManager.UpdateSettings(dynamicConfig, manualConfig, TelemetryFactory.Config);
            if (wasUpdated)
            {
                Log.Information("Setting updates made via configuration in code were applied");
            }

            // TODO: This might not record the config in the correct order in future, but would require
            // a big refactoring of debugger settings to resolve
            var settings = new ConfigurationBuilder(dynamicConfig, TelemetryFactory.Config);
            var dynamicDebuggerSettings = new ImmutableDynamicDebuggerSettings
            {
                DynamicInstrumentationEnabled = settings.WithKeys(ConfigurationKeys.Debugger.DynamicInstrumentationEnabled).AsBool(),
                ExceptionReplayEnabled = settings.WithKeys(ConfigurationKeys.Debugger.ExceptionReplayEnabled).AsBool(),
                CodeOriginEnabled = settings.WithKeys(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled).AsBool(),
            };

            var oldDebuggerSettings = DebuggerManager.Instance.DebuggerSettings;

            if (dynamicDebuggerSettings.Equals(oldDebuggerSettings.DynamicSettings))
            {
                Log.Debug("No changes detected in the new dynamic debugger configuration");
                return;
            }

            Log.Information("Applying new dynamic debugger configuration");
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug(
                    "DynamicInstrumentationEnabled={DynamicInstrumentationEnabled}, ExceptionReplayEnabled={ExceptionReplayEnabled}, CodeOriginEnabled={CodeOriginEnabled}",
                    dynamicDebuggerSettings.DynamicInstrumentationEnabled,
                    dynamicDebuggerSettings.ExceptionReplayEnabled,
                    dynamicDebuggerSettings.CodeOriginEnabled);
            }

            var newDebuggerSettings = oldDebuggerSettings with { DynamicSettings = dynamicDebuggerSettings };

            DebuggerManager.Instance.UpdateConfiguration(Tracer.Instance.Settings, newDebuggerSettings)
                           .ContinueWith(t => Log.Error(t?.Exception, "Error updating dynamic configuration for debugger"), TaskContinuationOptions.OnlyOnFaulted);
        }

        [TestingAndPrivateOnly]
        internal static List<RemoteConfiguration> CombineApmTracingConfiguration(
            Dictionary<string, RemoteConfiguration> activeConfigurations,
            Dictionary<string, List<RemoteConfiguration>> configByProduct,
            Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct,
            List<ApplyDetails> applyDetailsResult)
        {
            // Phase 1: Handle explicit removals from removedConfigByProduct
            if (removedConfigByProduct?.TryGetValue(ProductName, out var removedConfigs) == true)
            {
                foreach (var removedConfig in removedConfigs)
                {
                    if (activeConfigurations.Remove(removedConfig.Id))
                    {
                        Log.Debug("Explicitly removed APM_TRACING configuration {ConfigId}", removedConfig.Id);
                    }
                }
            }

            // Phase 2: Handle new/updated configurations and implicit removals
            if (configByProduct.TryGetValue(ProductName, out var apmLibrary))
            {
                // if we have some config, then we will "overwrite" everything that's currently active
                if (Log.IsEnabled(LogEventLevel.Debug) && activeConfigurations.Count > 0)
                {
                    Log.Debug<int, int>("Implicitly removing {RemovedCount} APM_TRACING configurations and replacing with {AddedCount}", activeConfigurations.Count, apmLibrary.Count);
                }

                activeConfigurations.Clear();

                // Add/update configurations
                foreach (var config in apmLibrary)
                {
                    activeConfigurations[config.Path.Id] = config;
                    applyDetailsResult.Add(ApplyDetails.FromOk(config.Path.Path));
                }
            }

            return [..activeConfigurations.Values];
        }

        private ApplyDetails[] ConfigurationUpdated(
            Dictionary<string, List<RemoteConfiguration>> configByProduct,
            Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
        {
            {
                var applyDetailsResult = new List<ApplyDetails>();

                try
                {
                    // This is all non-thread safe, but we're called in a single threaded way by
                    // the RcmSubscriptionManager so that's fine
                    var valuesToApply = CombineApmTracingConfiguration(_activeConfigurations, configByProduct, removedConfigByProduct, applyDetailsResult);

                    // Phase 3: Apply merged configuration
                    ApplyMergedConfiguration(valuesToApply);

                    return [..applyDetailsResult];
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while applying dynamic configuration");
                    return [..applyDetailsResult.Select(r => ApplyDetails.FromError(r.Filename, ex.ToString()))];
                }
            }
        }

        private void ApplyMergedConfiguration(List<RemoteConfiguration> remoteConfigurations)
        {
            // Get current service/environment for filtering
            var currentSettings = Tracer.Instance.CurrentTraceSettings.Settings;

            var mergedConfigJToken = ApmTracingConfigMerger.MergeConfigurations(
                remoteConfigurations,
                serviceName: currentSettings.ServiceName,
                environment: currentSettings.Environment);

            var configurationSource = new DynamicConfigConfigurationSource(mergedConfigJToken, ConfigurationOrigins.RemoteConfig);

            OnConfigurationChanged(configurationSource);
        }
    }
}

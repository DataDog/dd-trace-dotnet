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
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Configuration
{
    internal class DynamicConfigurationManager : IDynamicConfigurationManager
    {
        internal const string ProductName = "APM_TRACING";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DynamicConfigurationManager>();

        private readonly IRcmSubscriptionManager _subscriptionManager;
        private readonly ConfigurationTelemetry _configurationTelemetry;
        private readonly Dictionary<string, RemoteConfiguration> _activeConfigurations = new();
        private readonly object _configLock = new();
        private ISubscription? _subscription;

        public DynamicConfigurationManager(IRcmSubscriptionManager subscriptionManager)
        {
            _subscriptionManager = subscriptionManager;
            _configurationTelemetry = new ConfigurationTelemetry();
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

        internal static void OnlyForTests_ApplyConfiguration(ConfigurationBuilder settings)
        {
            OnConfigurationChanged(settings);
        }

        private static void OnConfigurationChanged(ConfigurationBuilder settings)
        {
            var oldSettings = Tracer.Instance.Settings;

            var headerTags = MutableSettings.InitializeHeaderTags(settings, ConfigurationKeys.HeaderTags, headerTagsNormalizationFixEnabled: true);
            // var serviceNameMappings = TracerSettings.InitializeServiceNameMappings(settings, ConfigurationKeys.ServiceNameMappings);

            var globalTags = settings.WithKeys(ConfigurationKeys.GlobalTags).AsDictionary();

            var dynamicSettings = new ImmutableDynamicSettings
            {
                TraceEnabled = settings.WithKeys(ConfigurationKeys.TraceEnabled).AsBool(),
                // RuntimeMetricsEnabled = settings.WithKeys(ConfigurationKeys.RuntimeMetricsEnabled).AsBool(),
                // DataStreamsMonitoringEnabled = settings.WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled).AsBool(),
                // Note: Calling GetAsClass<string>() here instead of GetAsString() as we need to get the
                // "serialized JToken", which in JsonConfigurationSource is different, as it allows for non-string tokens
                SamplingRules = settings.WithKeys(ConfigurationKeys.CustomSamplingRules).GetAsClass<string>(validator: null, converter: s => s),
                GlobalSamplingRate = settings.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDouble(),
                // SpanSamplingRules = settings.WithKeys(ConfigurationKeys.SpanSamplingRules).AsString(),
                LogsInjectionEnabled = settings.WithKeys(ConfigurationKeys.LogsInjectionEnabled).AsBool(),
                HeaderTags = headerTags,
                // ServiceNameMappings = serviceNameMappings == null ? null : new ReadOnlyDictionary<string, string>(serviceNameMappings)
                GlobalTags = globalTags == null ? null : new ReadOnlyDictionary<string, string>(globalTags)
            };

            // Needs to be done before returning, to feed the value to the telemetry
            // var debugLogsEnabled = settings.WithKeys(ConfigurationKeys.DebugEnabled).AsBool();

            TracerSettings newSettings;
            if (dynamicSettings.Equals(oldSettings.DynamicSettings))
            {
                Log.Debug("No changes detected in the new dynamic configuration");
                newSettings = oldSettings;
            }
            else
            {
                Log.Information("Applying new dynamic configuration");

                newSettings = oldSettings with { DynamicSettings = dynamicSettings };

                /*
                if (debugLogsEnabled != null && debugLogsEnabled.Value != GlobalSettings.Instance.DebugEnabled)
                {
                    GlobalSettings.SetDebugEnabled(debugLogsEnabled.Value);
                    Security.Instance.SetDebugEnabled(debugLogsEnabled.Value);

                    NativeMethods.UpdateSettings(new[] { ConfigurationKeys.DebugEnabled }, new[] { debugLogsEnabled.Value ? "1" : "0" });
                }
                */

                Tracer.Configure(newSettings);
            }

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

            DebuggerManager.Instance.UpdateConfiguration(newSettings, newDebuggerSettings)
                           .ContinueWith(t => Log.Error(t?.Exception, "Error updating dynamic configuration for debugger"), TaskContinuationOptions.OnlyOnFaulted);
        }

        // Internal for testing
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
                        applyDetailsResult.Add(ApplyDetails.FromOk(removedConfig.Path));
                    }
                }
            }

            // Phase 2: Handle new/updated configurations and implicit removals
            if (configByProduct.TryGetValue(ProductName, out var apmLibrary))
            {
                var receivedConfigIds = new HashSet<string>();

                // Add/update configurations
                foreach (var config in apmLibrary)
                {
                    receivedConfigIds.Add(config.Path.Id);
                    activeConfigurations[config.Path.Id] = config;
                    applyDetailsResult.Add(ApplyDetails.FromOk(config.Path.Path));
                }

                // Remove configurations not in this update
                var configsToRemove = activeConfigurations.Keys
                                                          .Where(configId => !receivedConfigIds.Contains(configId))
                                                          .ToList();

                foreach (var configId in configsToRemove)
                {
                    activeConfigurations.Remove(configId);
                    Log.Debug("Implicitly removed APM_TRACING configuration {ConfigId} (not in update)", configId);
                }
            }

            var valuesToApply = activeConfigurations.Values.ToList();
            return valuesToApply;
        }

        private ApplyDetails[] ConfigurationUpdated(
            Dictionary<string, List<RemoteConfiguration>> configByProduct,
            Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
        {
            lock (_configLock)
            {
                var applyDetailsResult = new List<ApplyDetails>();

                try
                {
                    var valuesToApply = CombineApmTracingConfiguration(_activeConfigurations, configByProduct, removedConfigByProduct, applyDetailsResult);

                    // Phase 3: Apply merged configuration
                    ApplyMergedConfiguration(valuesToApply);

                    return applyDetailsResult.ToArray();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while applying dynamic configuration");
                    return applyDetailsResult.Select(r => ApplyDetails.FromError(r.Filename, ex.ToString())).ToArray();
                }
            }
        }

        private void ApplyMergedConfiguration(List<RemoteConfiguration> remoteConfigurations)
        {
            // Get current service/environment for filtering
            var currentSettings = Tracer.Instance.Settings;
            var serviceName = currentSettings.ServiceName;
            var environment = currentSettings.Environment ?? Tracer.Instance.DefaultServiceName;

            var mergedConfigJToken = ApmTracingConfigMerger.MergeConfigurations(
                remoteConfigurations,
                serviceName,
                environment);

            var configurationSource = new DynamicConfigConfigurationSource(mergedConfigJToken, ConfigurationOrigins.RemoteConfig);
            var configurationBuilder = new ConfigurationBuilder(configurationSource, _configurationTelemetry);

            OnConfigurationChanged(configurationBuilder);

            _configurationTelemetry.CopyTo(TelemetryFactory.Config);
            _configurationTelemetry.Clear();
        }
    }
}

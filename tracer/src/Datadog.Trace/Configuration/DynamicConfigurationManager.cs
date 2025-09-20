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
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;

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

                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingCustomTags, true);     // 15
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingHttpHeaderTags, true); // 14
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingLogsInjection, true);  // 13
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, true);     // 12
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true); // 19
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRules, true);    // 29
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingMulticonfig, true);    // 44
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

            var headerTags = TracerSettings.InitializeHeaderTags(settings, ConfigurationKeys.HeaderTags, headerTagsNormalizationFixEnabled: true);
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

            if (dynamicSettings.Equals(oldSettings.DynamicSettings))
            {
                Log.Debug("No changes detected in the new dynamic configuration");
                return;
            }

            Log.Information("Applying new dynamic configuration");

            var newSettings = oldSettings with { DynamicSettings = dynamicSettings };

            /*
            if (debugLogsEnabled != null && debugLogsEnabled.Value != GlobalSettings.Instance.DebugEnabled)
            {
                GlobalSettings.SetDebugEnabledInternal(debugLogsEnabled.Value);
                Security.Instance.SetDebugEnabled(debugLogsEnabled.Value);

                NativeMethods.UpdateSettings(new[] { ConfigurationKeys.DebugEnabled }, new[] { debugLogsEnabled.Value ? "1" : "0" });
            }
            */

            Tracer.ConfigureInternal(newSettings);
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
                    // Phase 1: Handle explicit removals from removedConfigByProduct
                    if (removedConfigByProduct?.TryGetValue(ProductName, out var removedConfigs) == true)
                    {
                        foreach (var removedConfig in removedConfigs)
                        {
                            if (_activeConfigurations.Remove(removedConfig.Id))
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
                            _activeConfigurations[config.Path.Id] = config;
                            applyDetailsResult.Add(ApplyDetails.FromOk(config.Path.Path));
                        }

                        // Remove configurations not in this update
                        var configsToRemove = _activeConfigurations.Keys
                                                                   .Where(configId => !receivedConfigIds.Contains(configId))
                                                                   .ToList();

                        foreach (var configId in configsToRemove)
                        {
                            _activeConfigurations.Remove(configId);
                            Log.Debug("Implicitly removed APM_TRACING configuration {ConfigId} (not in update)", configId);
                        }
                    }

                    // Phase 3: Apply merged configuration
                    ApplyMergedConfiguration();

                    return applyDetailsResult.ToArray();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error while applying dynamic configuration");
                    return applyDetailsResult.Select(r => ApplyDetails.FromError(r.Filename, ex.ToString())).ToArray();
                }
            }
        }

        private void ApplyMergedConfiguration()
        {
            // Get current service/environment for filtering
            var currentSettings = Tracer.Instance.Settings;
            var serviceName = currentSettings.ServiceName ?? "unknown";
            var environment = currentSettings.Environment ?? "unknown";

            var mergedConfigJson = ApmTracingConfigMerger.MergeConfigurations(
                _activeConfigurations.Values.ToList(),
                serviceName,
                environment);

            var configurationSource = new DynamicConfigConfigurationSource(mergedConfigJson, ConfigurationOrigins.RemoteConfig);
            var configurationBuilder = new ConfigurationBuilder(configurationSource, _configurationTelemetry);

            OnConfigurationChanged(configurationBuilder);

            _configurationTelemetry.CopyTo(TelemetryFactory.Config);
            _configurationTelemetry.Clear();
        }
    }
}

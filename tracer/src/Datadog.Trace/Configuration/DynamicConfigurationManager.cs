// <copyright file="DynamicConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
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

                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingCustomTags, true);     // 15
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingHttpHeaderTags, true); // 14
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingLogsInjection, true);  // 13
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, true);     // 12
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true); // 19
                _subscriptionManager.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRules, true);    // 29
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
            var tracerSettings = Tracer.Instance.Settings;
            var manualSource = GlobalConfigurationSource.ManualConfigurationSource;
            var mutableSettings = manualSource.UseDefaultSources
                                      ? tracerSettings.MutableSettings
                                      : MutableSettings.CreateWithoutDefaultSources(tracerSettings);

            OnConfigurationChanged(
                dynamicConfig,
                manualSource,
                mutableSettings,
                tracerSettings,
                // TODO: In the future this will 'live' elsewhere
                tracerSettings.MutableSettings,
                TelemetryFactory.Config,
                new OverrideErrorLog()); // TODO: We'll later report these
        }

        private static void OnConfigurationChanged(
            IConfigurationSource dynamicConfig,
            ManualInstrumentationConfigurationSourceBase manualConfig,
            MutableSettings initialSettings,
            TracerSettings tracerSettings,
            MutableSettings currentSettings,
            IConfigurationTelemetry telemetry,
            OverrideErrorLog errorLog)
        {
            var newMutableSettings = MutableSettings.CreateUpdatedMutableSettings(
                dynamicConfig,
                manualConfig,
                initialSettings,
                tracerSettings,
                telemetry,
                errorLog);

            if (currentSettings.Equals(newMutableSettings))
            {
                Log.Debug("No changes detected in the new dynamic configuration");
                return;
            }

            Log.Information("Applying new dynamic configuration");
            GlobalConfigurationSource.UpdateDynamicConfigConfigurationSource(dynamicConfig);

            var newSettings = tracerSettings with { MutableSettings = newMutableSettings };

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

        private ApplyDetails[] ConfigurationUpdated(Dictionary<string, List<RemoteConfiguration>> configByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
        {
            if (!configByProduct.TryGetValue(ProductName, out var apmLibrary))
            {
                return Array.Empty<ApplyDetails>();
            }

            if (apmLibrary.Count == 0)
            {
                return Array.Empty<ApplyDetails>();
            }

            var result = new ApplyDetails[apmLibrary.Count];

            try
            {
                IConfigurationSource configurationSource;

                if (apmLibrary.Count == 1)
                {
                    configurationSource = new DynamicConfigConfigurationSource(Encoding.UTF8.GetString(apmLibrary[0].Contents), ConfigurationOrigins.RemoteConfig);
                }
                else
                {
                    var compositeConfigurationSource = new CompositeConfigurationSource();

                    foreach (var item in apmLibrary)
                    {
                        compositeConfigurationSource.Add(new DynamicConfigConfigurationSource(Encoding.UTF8.GetString(item.Contents), ConfigurationOrigins.RemoteConfig));
                    }

                    configurationSource = compositeConfigurationSource;
                }

                OnConfigurationChanged(configurationSource);

                for (int i = 0; i < apmLibrary.Count; i++)
                {
                    result[i] = ApplyDetails.FromOk(apmLibrary[i].Path.Path);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error while applying dynamic configuration");

                for (int i = 0; i < apmLibrary.Count; i++)
                {
                    result[i] = ApplyDetails.FromError(apmLibrary[i].Path.Path, ex.ToString());
                }

                return result;
            }
        }
    }
}

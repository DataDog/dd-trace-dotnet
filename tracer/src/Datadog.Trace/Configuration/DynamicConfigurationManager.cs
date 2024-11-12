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
                HeaderTags = headerTags == null ? null : new ReadOnlyDictionary<string, string>(headerTags),
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
                    var compositeConfigurationSource = new CompositeConfigurationSourceInternal();

                    foreach (var item in apmLibrary)
                    {
                        compositeConfigurationSource.AddInternal(new DynamicConfigConfigurationSource(Encoding.UTF8.GetString(item.Contents), ConfigurationOrigins.RemoteConfig));
                    }

                    configurationSource = compositeConfigurationSource;
                }

                var configurationBuilder = new ConfigurationBuilder(configurationSource, _configurationTelemetry);

                OnConfigurationChanged(configurationBuilder);

                _configurationTelemetry.CopyTo(TelemetryFactory.Config);
                _configurationTelemetry.Clear();

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

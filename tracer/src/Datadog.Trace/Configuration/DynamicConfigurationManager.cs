// <copyright file="DynamicConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
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

            var headerTags = TracerSettings.InitializeHeaderTags(settings, ConfigurationKeys.HeaderTags, oldSettings.HeaderTagsNormalizationFixEnabled);
            var serviceNameMappings = TracerSettings.InitializeServiceNameMappings(settings, ConfigurationKeys.ServiceNameMappings);

            var dynamicSettings = new ImmutableDynamicSettings
            {
                RuntimeMetricsEnabled = settings.WithKeys(ConfigurationKeys.RuntimeMetricsEnabled).AsBool(),
                DataStreamsMonitoringEnabled = settings.WithKeys(ConfigurationKeys.DataStreamsMonitoring.Enabled).AsBool(),
                CustomSamplingRules = settings.WithKeys(ConfigurationKeys.CustomSamplingRules).AsString(),
                GlobalSamplingRate = settings.WithKeys(ConfigurationKeys.GlobalSamplingRate).AsDouble(),
                SpanSamplingRules = settings.WithKeys(ConfigurationKeys.SpanSamplingRules).AsString(),
                LogsInjectionEnabled = settings.WithKeys(ConfigurationKeys.LogsInjectionEnabled).AsBool(),
                HeaderTags = headerTags == null ? null : new ReadOnlyDictionary<string, string>(headerTags),
                ServiceNameMappings = serviceNameMappings == null ? null : new ReadOnlyDictionary<string, string>(serviceNameMappings)
            };

            // Needs to be done before returning, to feed the value to the telemetry
            var debugLogsEnabled = settings.WithKeys(ConfigurationKeys.DebugEnabled).AsBool();

            if (dynamicSettings.Equals(oldSettings.DynamicSettings))
            {
                Log.Debug("No changes detected in the new dynamic configuration");
                return;
            }

            Log.Debug("Applying new dynamic configuration");

            var newSettings = oldSettings with { DynamicSettings = dynamicSettings };

            if (debugLogsEnabled != null && debugLogsEnabled.Value != GlobalSettings.Instance.DebugEnabled)
            {
                GlobalSettings.SetDebugEnabledInternal(debugLogsEnabled.Value);
                Security.Instance.SetDebugEnabled(debugLogsEnabled.Value);

                NativeMethods.UpdateSettings(new[] { ConfigurationKeys.DebugEnabled }, new[] { debugLogsEnabled.Value ? "1" : "0" });
            }

            Tracer.ConfigureInternal(newSettings);
        }

        private IEnumerable<ApplyDetails> ConfigurationUpdated(Dictionary<string, List<RemoteConfiguration>> configByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
        {
            if (!configByProduct.TryGetValue(ProductName, out var apmLibrary))
            {
                return Enumerable.Empty<ApplyDetails>();
            }

            if (apmLibrary.Count == 0)
            {
                return Enumerable.Empty<ApplyDetails>();
            }

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

            var configurationBuilder = new ConfigurationBuilder(configurationSource, TelemetryFactory.Config);

            OnConfigurationChanged(configurationBuilder);

            var result = new ApplyDetails[apmLibrary.Count];

            for (int i = 0; i < apmLibrary.Count; i++)
            {
                result[i] = ApplyDetails.FromOk(apmLibrary[i].Path.Path);
            }

            return result;
        }
    }
}

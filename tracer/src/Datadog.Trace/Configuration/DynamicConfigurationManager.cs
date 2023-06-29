// <copyright file="DynamicConfigurationManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;

namespace Datadog.Trace.Configuration
{
    internal class DynamicConfigurationManager : IDynamicConfigurationManager
    {
        internal const string ProductName = "APM_LIBRARY";

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

        private static IEnumerable<ApplyDetails> ConfigurationUpdated(Dictionary<string, List<RemoteConfiguration>> configByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
        {
            if (!configByProduct.TryGetValue(ProductName, out var apmLibrary))
            {
                return Enumerable.Empty<ApplyDetails>();
            }

            if (apmLibrary.Count == 0)
            {
                return Enumerable.Empty<ApplyDetails>();
            }

            // TODO: To adjust when the actual path of the items will be known

            IConfigurationSource configurationSource;

            if (apmLibrary.Count == 1)
            {
                configurationSource = new JsonConfigurationSource(Encoding.UTF8.GetString(apmLibrary[0].Contents), ConfigurationOrigins.RemoteConfig);
            }
            else
            {
                var compositeConfigurationSource = new CompositeConfigurationSource();

                foreach (var item in apmLibrary)
                {
                    compositeConfigurationSource.Add(new JsonConfigurationSource(Encoding.UTF8.GetString(item.Contents), ConfigurationOrigins.RemoteConfig));
                }

                configurationSource = compositeConfigurationSource;
            }

            var configurationBuilder = new ConfigurationBuilder(configurationSource, TelemetryFactory.Config);

            OnConfigurationChanged(configurationBuilder);

            // TODO: Are we supposed to acknowledge something?
            return Enumerable.Empty<ApplyDetails>();
        }

        private static void OnConfigurationChanged(ConfigurationBuilder settings)
        {
            var oldSettings = Tracer.Instance.Settings;

            var headerTags = TracerSettings.InitializeHeaderTags(settings, "TraceHeaderTags", oldSettings.HeaderTagsNormalizationFixEnabled);
            var serviceNameMappings = TracerSettings.InitializeServiceNameMappings(settings, "TraceServiceMapping");

            var dynamicSettings = new ImmutableDynamicSettings
            {
                RuntimeMetricsEnabled = settings.WithKeys("RuntimeMetricsEnabled").AsBool(),
                DataStreamsMonitoringEnabled = settings.WithKeys("DataStreamsEnabled").AsBool(),
                CustomSamplingRules = settings.WithKeys("CustomSamplingRules").AsString(),
                GlobalSamplingRate = settings.WithKeys("TraceSampleRate").AsDouble(),
                SpanSamplingRules = settings.WithKeys("SpanSamplingRules").AsString(),
                LogsInjectionEnabled = settings.WithKeys("LogsInjectionEnabled").AsBool(),
                HeaderTags = headerTags as IReadOnlyDictionary<string, string>,
                ServiceNameMappings = serviceNameMappings
            };

            if (dynamicSettings.Equals(oldSettings.DynamicSettings))
            {
                return;
            }

            var newSettings = oldSettings with { DynamicSettings = dynamicSettings };

            var debugLogsEnabled = settings.WithKeys("DebugLogsEnabled").AsBool();

            if (debugLogsEnabled != null && debugLogsEnabled.Value != GlobalSettings.Instance.DebugEnabled)
            {
                GlobalSettings.SetDebugEnabledInternal(debugLogsEnabled.Value);
                Security.Instance.SetDebugEnabled(debugLogsEnabled.Value);

                NativeMethods.UpdateSettings(new[] { "DD_TRACE_DEBUG" }, new[] { debugLogsEnabled.Value ? "1" : "0" });
            }

            Tracer.Configure(newSettings);
        }
    }
}

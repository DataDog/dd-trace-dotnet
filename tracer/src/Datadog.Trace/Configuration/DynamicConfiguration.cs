// <copyright file="DynamicConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace.Configuration
{
    internal class DynamicConfiguration
    {
        internal const string ProductName = "APM_LIBRARY";

        public static void Initialize()
        {
            var subscription = new Subscription(ConfigurationUpdated, ProductName);

            RcmSubscriptionManager.Instance.SubscribeToChanges(subscription);
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
                configurationSource = new JsonConfigurationSource(Encoding.UTF8.GetString(apmLibrary[0].Contents));
            }
            else
            {
                var compositeConfigurationSource = new CompositeConfigurationSource();

                foreach (var item in apmLibrary)
                {
                    compositeConfigurationSource.Add(new JsonConfigurationSource(Encoding.UTF8.GetString(item.Contents)));
                }

                configurationSource = compositeConfigurationSource;
            }

            OnConfigurationChanged(configurationSource);

            // TODO: Are we supposed to acknowledge something?
            return Enumerable.Empty<ApplyDetails>();
        }

        private static void OnConfigurationChanged(IConfigurationSource settings)
        {
            var oldSettings = Tracer.Instance.Settings;

            var headerTags = TracerSettings.InitializeHeaderTags(settings, "TraceHeaderTags", oldSettings.HeaderTagsNormalizationFixEnabled);
            var serviceNameMappings = TracerSettings.InitializeServiceNameMappings(settings, "TraceServiceMapping");
            ServiceNames? serviceNames = null;

            if (serviceNameMappings != null)
            {
                serviceNames = new ServiceNames(serviceNameMappings, oldSettings.MetadataSchemaVersion);
            }

            var dynamicSettings = new ImmutableDynamicSettings
            {
                RuntimeMetricsEnabled = settings.GetBool("RuntimeMetricsEnabled"),
                DataStreamsMonitoringEnabled = settings.GetBool("DataStreamsEnabled"),
                CustomSamplingRules = settings.GetString("CustomSamplingRules"),
                GlobalSamplingRate = settings.GetDouble("TraceSampleRate"),
                SpanSamplingRules = settings.GetString("SpanSamplingRules"),
                LogsInjectionEnabled = settings.GetBool("LogsInjectionEnabled"),
                HeaderTags = headerTags as IReadOnlyDictionary<string, string>,
                ServiceNameMappings = serviceNames
            };

            var newSettings = oldSettings with { DynamicSettings = dynamicSettings };

            var debugLogsEnabled = settings.GetBool("DebugLogsEnabled");

            if (debugLogsEnabled != null && debugLogsEnabled.Value != GlobalSettings.Instance.DebugEnabled)
            {
                GlobalSettings.SetDebugEnabled(debugLogsEnabled.Value);
                Security.Instance.SetDebugEnabled(debugLogsEnabled.Value);

                NativeMethods.UpdateSettings(new[] { "DD_TRACE_DEBUG" }, new[] { debugLogsEnabled.Value ? "1" : "0" });
            }

            TracerManager.ReplaceGlobalManager(newSettings, TracerManagerFactory.Instance);
        }
    }
}

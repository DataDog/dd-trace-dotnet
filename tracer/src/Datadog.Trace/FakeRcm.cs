// <copyright file="FakeRcm.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;

namespace Datadog.Trace
{
    internal class FakeRcm
    {
        public static void Initialize()
        {
            var subscription = new Subscription(ConfigurationUpdated, "APM_LIBRARY");

            RcmSubscriptionManager.Instance.SubscribeToChanges(subscription);
        }

        private static IEnumerable<ApplyDetails> ConfigurationUpdated(Dictionary<string, List<RemoteConfiguration>> configByProduct, Dictionary<string, List<RemoteConfigurationPath>>? removedConfigByProduct)
        {
            Console.WriteLine("ConfigurationUpdated");

            if (configByProduct.TryGetValue("APM_LIBRARY", out var apmLibrary))
            {
                if (apmLibrary.Count == 1)
                {
                    var configurationSource = new JsonConfigurationSource(Encoding.UTF8.GetString(apmLibrary[0].Contents));

                    OnConfigurationChanged(configurationSource);
                }
                else
                {
                    Console.WriteLine($"Unexpected number of items in APM_LIBRARY: {apmLibrary.Count}");
                }
            }
            else
            {
                Console.WriteLine("Missing APM_LIBRARY");
            }

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

            var newSettings = oldSettings with
            {
                RuntimeMetricsEnabled = settings.GetBool("RuntimeMetricsEnabled") ?? oldSettings.RuntimeMetricsEnabled,
                IsDataStreamsMonitoringEnabled = settings.GetBool("DataStreamsEnabled") ?? oldSettings.IsDataStreamsMonitoringEnabled,
                CustomSamplingRules = settings.GetString("CustomSamplingRules") ?? oldSettings.CustomSamplingRules,
                GlobalSamplingRate = settings.GetDouble("TraceSampleRate") ?? oldSettings.GlobalSamplingRate,
                SpanSamplingRules = settings.GetString("SpanSamplingRules") ?? oldSettings.SpanSamplingRules,
                LogsInjectionEnabled = settings.GetBool("LogsInjectionEnabled") ?? oldSettings.LogsInjectionEnabled,
                HeaderTags = (headerTags as IReadOnlyDictionary<string, string>) ?? oldSettings.HeaderTags,
                ServiceNameMappings = serviceNames ?? oldSettings.ServiceNameMappings
            };

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

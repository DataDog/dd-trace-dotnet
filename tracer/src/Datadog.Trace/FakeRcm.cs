// <copyright file="FakeRcm.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace
{
    internal class FakeRcm
    {
        private static FakeRcm _instance;

        private readonly FileSystemWatcher _watcher;
        private readonly Action<IConfigurationSource> _configurationChanged;

        private int _pendingUpdate = 0;

        public FakeRcm(Action<IConfigurationSource> configurationChanged)
        {
            const string path = @"C:\temp\rcm.json";

            Console.WriteLine($"FakeRcm watching file {path}");

            _configurationChanged = configurationChanged;

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path));
            _watcher.Changed += Changed;
            _watcher.EnableRaisingEvents = true;
        }

        public static void Initialize()
        {
            _instance = new(OnConfigurationChanged);
        }

        private static void OnConfigurationChanged(IConfigurationSource settings)
        {
            var oldSettings = Tracer.Instance.Settings;

            var headerTags = TracerSettings.InitializeHeaderTags(settings, "TraceHeaderTags", oldSettings.HeaderTagsNormalizationFixEnabled);
            var serviceNameMappings = TracerSettings.InitializeServiceNames(settings, "TraceServiceMapping");

            var newSettings = oldSettings with
            {
                RuntimeMetricsEnabled = settings.GetBool("RuntimeMetricsEnabled") ?? oldSettings.RuntimeMetricsEnabled,
                IsDataStreamsMonitoringEnabled = settings.GetBool("DataStreamsEnabled") ?? oldSettings.IsDataStreamsMonitoringEnabled,
                CustomSamplingRules = settings.GetString("CustomSamplingRules") ?? oldSettings.CustomSamplingRules,
                GlobalSamplingRate = settings.GetDouble("TraceSampleRate") ?? oldSettings.GlobalSamplingRate,
                SpanSamplingRules = settings.GetString("SpanSamplingRules") ?? oldSettings.SpanSamplingRules,
                LogsInjectionEnabled = settings.GetBool("LogsInjectionEnabled") ?? oldSettings.LogsInjectionEnabled,
                HeaderTags = (headerTags as IReadOnlyDictionary<string, string>) ?? oldSettings.HeaderTags,
                ServiceNameMappings = serviceNameMappings ?? oldSettings.ServiceNameMappings
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

        private void Changed(object sender, FileSystemEventArgs e)
        {
            Task.Run(() =>
            {
                if (Interlocked.CompareExchange(ref _pendingUpdate, 1, 0) != 0)
                {
                    return;
                }

                Thread.Sleep(500);

                var fileContent = File.ReadAllText(e.FullPath);

                Console.WriteLine($"Applying new configuration: {fileContent}");

                _configurationChanged(new JsonConfigurationSource(fileContent));

                Volatile.Write(ref _pendingUpdate, 0);
            });
        }
    }
}

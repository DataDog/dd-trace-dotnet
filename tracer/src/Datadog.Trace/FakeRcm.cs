// <copyright file="FakeRcm.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace
{
    internal class FakeRcm
    {
        private static FakeRcm _instance;

        private readonly FileSystemWatcher _watcher;
        private readonly Action<RcmSettings> _configurationChanged;

        private int _pendingUpdate = 0;

        public FakeRcm(Action<RcmSettings> configurationChanged)
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

        private static void OnConfigurationChanged(RcmSettings settings)
        {
            Console.WriteLine($"Applying new configuration: {settings}");

            var oldSettings = Tracer.Instance.Settings;

            var newSettings = oldSettings with
            {
                RuntimeMetricsEnabled = settings.RuntimeMetricsEnabled ?? oldSettings.RuntimeMetricsEnabled,
                IsDataStreamsMonitoringEnabled = settings.DataStreamsEnabled ?? oldSettings.IsDataStreamsMonitoringEnabled
            };

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
                var settings = JsonConvert.DeserializeObject<RcmSettings>(fileContent);
                _configurationChanged(settings);

                Volatile.Write(ref _pendingUpdate, 0);
            });
        }
    }
}

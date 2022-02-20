// <copyright file="SnapshotUploader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Debugger
{
    internal class SnapshotUploader
    {
        private const int TimerInterval = 1000;
        private const int BatchSize = 100;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SnapshotUploader));
        private static object _globalInstanceLock = new();
        private static bool _globalInstanceInitialized;
        private static SnapshotUploader _instance;

        private readonly ImmutableDebuggerSettings _debuggerSettings;
        private readonly DatadogHttpClient _probeClient;
        private readonly List<string> _snapshots;
        private readonly Timer _timer;

        public SnapshotUploader()
        {
            _probeClient = new DatadogHttpClient();
            _snapshots = new List<string>();
            _debuggerSettings = ImmutableDebuggerSettings.Create(DebuggerSettings.FromDefaultSources());
            _timer = new Timer(async _ => await Send().ConfigureAwait(false));
            Start();
        }

        public static SnapshotUploader Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock);
            }
        }

        public void Start()
        {
            _timer.Change(TimerInterval, TimerInterval);
        }

        public void Stop()
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Post(string json)
        {
            try
            {
                _snapshots.Add(json);
                if (_snapshots.Count >= BatchSize)
                {
                    Task.Run(() => Send().ConfigureAwait(false));
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error posting snapshot probe");
            }
        }

        private async Task Send()
        {
            try
            {
                Stop();
                var debuggerSnapshots = _snapshots
                                       .Select(JsonConvert.DeserializeObject<ProbeSnapshot>)
                                       .Select(snapshotProbe =>
                                                   new DebuggerSnapshot
                                                   {
                                                       ProbeSnapshot = snapshotProbe,
                                                       Service = Tracer.Instance.TracerManager.DefaultServiceName,
                                                       Host = _debuggerSettings.HostId,
                                                       Tags = default,
                                                       Id = Guid.NewGuid().ToString(),
                                                       Message = string.Empty,
                                                       Timestamp = DateTimeOffset.UtcNow.ToString()
                                                   })
                                       .ToList();
                _snapshots.Clear();
                await _probeClient.SendAsync(null, null, null).ConfigureAwait(false);
                Start();
            }
            catch (Exception e)
            {
                Log.Error(e, $"{nameof(SnapshotUploader)}.{nameof(Send)}: Error sending snapshots");
            }
        }
    }
}

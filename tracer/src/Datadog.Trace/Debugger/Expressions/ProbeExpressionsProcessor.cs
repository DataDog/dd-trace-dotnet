// <copyright file="ProbeExpressionsProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Threading;
using Datadog.Trace.Debugger.Configurations.Models;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Expressions
{
    internal class ProbeExpressionsProcessor
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProbeExpressionsProcessor));

        private static object _globalInstanceLock = new();

        private static bool _globalInstanceInitialized;

        private static ProbeExpressionsProcessor _instance;

        private readonly ConcurrentDictionary<string, ProbeProcessor> _processors = new();

        internal static ProbeExpressionsProcessor Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _globalInstanceInitialized,
                    ref _globalInstanceLock);
            }
        }

        internal ProbeInfo? GetProbeInfo(string probeId)
        {
            return Get(probeId)?.ProbeInfo;
        }

        private ProbeProcessor Get(string probeId)
        {
            _processors.TryGetValue(probeId, out var probeProcessor);
            return probeProcessor;
        }

        public bool Process<T>(string probeId, ref CaptureInfo<T> info, DebuggerSnapshotCreator snapshotCreator)
        {
            var probeProcessor = Get(probeId);
            if (probeProcessor == null)
            {
                Log.Error("Probe processor has not found. Probe Id: " + probeId);
                return false;
            }

            return probeProcessor.Process(ref info, snapshotCreator);
        }

        public void AddProbeProcessor(ProbeDefinition probe)
        {
            try
            {
                _processors.TryAdd(probe.Id, new ProbeProcessor(probe));
            }
            catch (Exception e)
            {
                Log.Error(e, $"Failed to add probe expressions for probe {probe.Id}");
            }
        }

        public void Remove(string probeId)
        {
            _processors.TryRemove(probeId, out _);
        }
    }
}

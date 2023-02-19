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

        internal void AddProbeProcessor(ProbeDefinition probe)
        {
            try
            {
                _processors.AddOrUpdate(
                    probe.Id,
                    new ProbeProcessor(probe),
                    (s, processor) => processor.UpdateProbeProcessor(probe));
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to create probe processor for probe: {Id}", probe.Id);
            }
        }

        internal void Remove(string probeId)
        {
            _processors.TryRemove(probeId, out _);
        }

        internal ProbeProcessor Get(string probeId)
        {
            _processors.TryGetValue(probeId, out var probeProcessor);
            return probeProcessor;
        }
    }
}

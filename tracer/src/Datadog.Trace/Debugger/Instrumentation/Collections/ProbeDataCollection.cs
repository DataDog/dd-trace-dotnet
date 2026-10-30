// <copyright file="ProbeDataCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.RateLimiting;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// Acts as a registry of indexed <see cref="ProbeData"/>.
    /// Each instrumented probe is given an index (hard-coded into the instrumented bytecode),
    /// and this index is used to perform O(1) lookup for the corresponding ProbeData instance.
    /// The reason the index is incremented and dictated by the native side is to support multi-AppDomain scenarios.
    /// In these scenario, there will be multiple <see cref="EverGrowingCollection{TPayload}.Items"/> arrays, one for each AppDomain.
    /// In order for us to grab the same ProbeData across all of them, we need to dereference the same index,
    /// because the same instrumented bytecode could execute in different AppDomains, and static fields are not shared across AppDomains.
    internal sealed class ProbeDataCollection : EverGrowingCollection<ProbeData>
    {
        private static ProbeDataCollection _instance;
        private static object _instanceLock = new();
        private static bool _instanceInitialized;

        internal static ProbeDataCollection Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(
                    ref _instance,
                    ref _instanceInitialized,
                    ref _instanceLock);
            }
        }

        /// <summary>
        /// Tries to create a new <see cref="ProbeData"/> at <paramref name="index"/> and return it.
        /// </summary>
        /// <remarks>
        /// Returns a copy, so <see cref="RemoveProbes"/> clearing the entry concurrently can't change it after the caller checked it.
        /// </remarks>
        /// <param name="index">The index of the probe inside <see cref="EverGrowingCollection{TPayload}.Items"/></param>
        /// <param name="probeId">The id of the probe/></param>
        /// <returns>The probe data (either existed before or just created), or an empty one (<see cref="ProbeData.IsEmpty"/>) if it fails to create</returns>
        public ProbeData TryCreateProbeDataIfNotExists(int index, string probeId)
        {
            var probeData = TryGetProbeData(index, probeId);

            if (!probeData.IsEmpty())
            {
                return probeData;
            }

            // Create a new one at the given index
            lock (ItemsLocker)
            {
                probeData = TryGetProbeData(index, probeId);

                if (!probeData.IsEmpty())
                {
                    return probeData;
                }

                EnlargeCapacity(index);

                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug<int, int>(nameof(ProbeDataCollection) + "." + nameof(TryCreateProbeDataIfNotExists) + " Creating a new probe metadata info for index = {Index}, Items.Length = {Length}", index, Items.Length);
                }

                var processor = ProbeExpressionsProcessor.Instance.Get(probeId);

                if (processor == null)
                {
                    return ProbeData.Empty;
                }

                var sampler = ProbeRateLimiter.Instance.GerOrAddSampler(probeId);

                probeData = new ProbeData(probeId, sampler, processor);
                Items[index] = probeData;

                return probeData;
            }
        }

        /// <summary>
        /// Clears the entries of the removed probes, so a probe added again with the same id gets a new <see cref="ProbeData"/>
        /// with the processor and sampler registered at that time. The native side reuses the indices of a removed probe first,
        /// so without this, a probe added again would usually find its old entry, with a removed processor and a disposed sampler.
        /// </summary>
        /// <remarks>
        /// The native index of a probe isn't known here, and a method probe gets one index per instrumented method (e.g. overloads),
        /// so every entry is checked. Each AppDomain has its own collection, cleared by the Dynamic Instrumentation instance of that AppDomain.
        /// </remarks>
        /// <param name="probeIds">The ids of the removed probes</param>
        public void RemoveProbes(string[] probeIds)
        {
            if (probeIds.Length == 0)
            {
                return;
            }

            var removedProbeIds = new HashSet<string>(probeIds, StringComparer.Ordinal);
            var clearedCount = 0;

            lock (ItemsLocker)
            {
                var items = Items;
                for (var i = 0; i < items.Length; i++)
                {
                    var probeId = items[i].ProbeId;
                    if (probeId is not null && removedProbeIds.Contains(probeId))
                    {
                        items[i] = default;
                        clearedCount++;
                    }
                }
            }

            if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
            {
                Log.Debug<int, int>(nameof(ProbeDataCollection) + "." + nameof(RemoveProbes) + " Cleared {ClearedCount} probe metadata entries of {ProbeCount} removed probes", clearedCount, probeIds.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool IsEmpty(ref ProbeData payload)
        {
            return payload == default;
        }

        /// <summary>
        /// In the native side we attempt to reuse indices; when a probe gets removed, its associated probe data index
        /// will be reused when a new probe later gets add. To make sure the index embedded in the instrumentation is not stale,
        /// we compare the ProbeId at that index with the one we were expecting.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ProbeData TryGetProbeData(int index, string probeId)
        {
            var items = Items;
            if (index >= items.Length)
            {
                return ProbeData.Empty;
            }

            // Read the entry once. RemoveProbes can clear it at the same time, and a copy torn by that write
            // either has another ProbeId or misses its processor or sampler, which ProbeData.IsEmpty catches.
            var probeData = items[index];
            if (probeData.ProbeId == probeId)
            {
                return probeData;
            }

            return ProbeData.Empty;
        }
    }
}

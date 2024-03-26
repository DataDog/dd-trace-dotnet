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
    internal class ProbeDataCollection : EverGrowingCollection<ProbeData>
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
        /// <param name="index">The index of the probe inside <see cref="EverGrowingCollection{TPayload}.Items"/></param>
        /// <param name="probeId">The id of the probe/></param>
        /// <returns>true if succeeded (either existed before or just created), false if fails to create</returns>
        public ref ProbeData TryCreateProbeDataIfNotExists(int index, string probeId)
        {
            ref var probeData = ref TryGetProbeDataIndex(index, probeId);

            if (!probeData.IsEmpty())
            {
                return ref probeData;
            }

            // Create a new one at the given index
            lock (ItemsLocker)
            {
                probeData = ref TryGetProbeDataIndex(index, probeId);

                if (!probeData.IsEmpty())
                {
                    return ref probeData;
                }

                EnlargeCapacity(index);

                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug<int, int>(nameof(ProbeDataCollection) + "." + nameof(TryCreateProbeDataIfNotExists) + " Creating a new probe metadata info for index = {Index}, Items.Length = {Length}", index, Items.Length);
                }

                var processor = ProbeExpressionsProcessor.Instance.Get(probeId);

                if (processor == null)
                {
                    return ref ProbeData.Empty;
                }

                var sampler = ProbeRateLimiter.Instance.GerOrAddSampler(probeId);

                Items[index] = new ProbeData(probeId, sampler, processor);

                return ref Items[index];
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
        private ref ProbeData TryGetProbeDataIndex(int index, string probeId)
        {
            if (!IndexExists(index))
            {
                return ref ProbeData.Empty;
            }

            ref var probeData = ref Items[index];
            if (probeData.ProbeId == probeId)
            {
                return ref probeData;
            }

            return ref ProbeData.Empty;
        }
    }
}

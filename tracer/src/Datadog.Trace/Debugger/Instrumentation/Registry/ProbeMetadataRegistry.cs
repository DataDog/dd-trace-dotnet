// <copyright file="ProbeMetadataRegistry.cs" company="Datadog">
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

namespace Datadog.Trace.Debugger.Instrumentation.Registry
{
    /// Acts as a registry of indexed <see cref="ProbeMetadataInfo"/>.
    /// Each instrumented probe is given an index (hard-coded into the instrumented bytecode),
    /// and this index is used to perform O(1) lookup for the corresponding ProbeMetadataInfo instance.
    /// The reason the index is incremented and dictated by the native side is to support multi-AppDomain scenarios.
    /// In these scenario, there will be multiple <see cref="EverGrowingRegistry{TPayload}.Items"/> arrays, one for each AppDomain.
    /// In order for us to grab the same ProbeMetadataInfo across all of them, we need to dereference the same index,
    /// because the same instrumented bytecode could execute in different AppDomains, and static fields are not shared across AppDomains.
    internal class ProbeMetadataRegistry : EverGrowingRegistry<ProbeMetadataInfo>
    {
        private static ProbeMetadataRegistry _instance;
        private static object _instanceLock = new();
        private static bool _instanceInitialized;

        internal static ProbeMetadataRegistry Instance
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
        /// Tries to create a new <see cref="ProbeMetadataInfo"/> at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The index of the probe inside <see cref="EverGrowingRegistry{TPayload}.Items"/></param>
        /// <param name="probeId">The id of the probe/></param>
        /// <returns>true if succeeded (either existed before or just created), false if fails to create</returns>
        public bool TryCreateProbeMetadataIfNotExists(int index, string probeId)
        {
            if (IsIndexExists(index))
            {
                return true;
            }

            // Create a new one at the given index
            lock (ItemsLocker)
            {
                if (IsIndexExists(index))
                {
                    return true;
                }

                EnlargeCapacity(index);

                if (Log.IsEnabled(Vendors.Serilog.Events.LogEventLevel.Debug))
                {
                    Log.Debug($"{nameof(ProbeMetadataRegistry)}.{nameof(TryCreateProbeMetadataIfNotExists)}: Creating a new probe metadata info for index = {index}, Items.Length = {Items.Length}");
                }

                var processor = ProbeExpressionsProcessor.Instance.Get(probeId);

                if (processor == null)
                {
                    return false;
                }

                var sampler = ProbeRateLimiter.Instance.GerOrAddSampler(probeId);

                Items[index] = new ProbeMetadataInfo(probeId, sampler, processor);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override bool IsEmpty(ref ProbeMetadataInfo payload)
        {
            return payload == default;
        }
    }
}

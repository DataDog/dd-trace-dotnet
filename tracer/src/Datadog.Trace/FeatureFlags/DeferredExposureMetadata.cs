// <copyright file="DeferredExposureMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.FeatureFlags.Exposure.Model;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary>
    /// Metadata of an evaluation whose exposure waits for the commit. The provider hands this instance
    /// back to commit, so the exposure travels with it: no side table, and the commit still finds it
    /// after <c>Tracer.Configure</c> replaced the module.
    /// </summary>
    internal sealed class DeferredExposureMetadata : Dictionary<string, string>
    {
        private const int Pending = 1;
        private const int Taken = 2;

        private ExposureEvent _exposure;
        private int _state; // 0 until an exposure is pending

        internal bool IsPending => Volatile.Read(ref _state) == Pending;

        internal void SetPendingExposure(in ExposureEvent exposure)
        {
            _exposure = exposure;
            Volatile.Write(ref _state, Pending);
        }

        /// <summary>Takes the pending exposure, at most once.</summary>
        internal bool TryTakePendingExposure(out ExposureEvent exposure)
        {
            if (Interlocked.CompareExchange(ref _state, Taken, Pending) != Pending)
            {
                exposure = default;
                return false;
            }

            exposure = _exposure;
            // Releases the subject's context attributes with the commit, not with the metadata.
            _exposure = default;
            return true;
        }
    }
}

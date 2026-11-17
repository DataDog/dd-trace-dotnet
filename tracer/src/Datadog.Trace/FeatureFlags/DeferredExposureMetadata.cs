// <copyright file="DeferredExposureMetadata.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.FeatureFlags.Exposure;
using Datadog.Trace.FeatureFlags.Exposure.Model;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary>
    /// Evaluation metadata that carries a pending exposure until the provider commits it. No side table,
    /// so the commit still works after <c>Tracer.Configure</c> replaces the module.
    /// </summary>
    internal sealed class DeferredExposureMetadata : Dictionary<string, string>
    {
        private const int Pending = 1;
        private const int Taken = 2;

        private ExposureEvent _exposure;
        private PendingExposureBatch? _batch;
        private IReadOnlyList<IDictionary<string, object?>>? _dependencyTelemetry;
        private int _state; // 0 until an exposure is pending

        internal DeferredExposureMetadata()
        {
        }

        internal DeferredExposureMetadata(IDictionary<string, string> metadata)
            : base(metadata)
        {
        }

        internal bool IsPending => Volatile.Read(ref _state) == Pending;

        internal IReadOnlyList<IDictionary<string, object?>>? DependencyTelemetry => _dependencyTelemetry;

        internal void SetDependencyTelemetry(IReadOnlyList<IDictionary<string, object?>> dependencyTelemetry)
        {
            _dependencyTelemetry = dependencyTelemetry;
        }

        internal void RecordDependencyExposureResult(string flagKey, ExposureRecordResult result)
        {
            if (_dependencyTelemetry is not { } telemetry)
            {
                return;
            }

            for (var i = 0; i < telemetry.Count; i++)
            {
                var record = telemetry[i];
                if (record.TryGetValue("flagKey", out var key)
                 && string.Equals(key as string, flagKey, System.StringComparison.Ordinal)
                 && record.TryGetValue("exposureEligible", out var eligible)
                 && eligible is true)
                {
                    record["exposureCommitted"] = result != ExposureRecordResult.NotRecorded;
                    if (result != ExposureRecordResult.NotRecorded)
                    {
                        record["exposureCacheHit"] = result == ExposureRecordResult.Repeat;
                    }

                    return;
                }
            }
        }

        internal void SetPendingExposure(in ExposureEvent exposure)
        {
            _exposure = exposure;
            Volatile.Write(ref _state, Pending);
        }

        internal void SetPendingExposureBatch(string rootFlagKey, IReadOnlyList<ExposureEvent> exposures)
        {
            _batch = new PendingExposureBatch(rootFlagKey, exposures);
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
            // Free the context attributes now, not with the metadata.
            _exposure = default;
            return true;
        }

        internal bool TryTakePendingExposureBatch(out PendingExposureBatch batch)
        {
            if (Interlocked.CompareExchange(ref _state, Taken, Pending) != Pending)
            {
                batch = null!;
                return false;
            }

            batch = _batch ?? new PendingExposureBatch(_exposure.Flag.Key, in _exposure);
            _batch = null;
            _exposure = default;
            return true;
        }
    }
}

// <copyright file="EverGrowingCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Debugger.Instrumentation.Collections
{
    /// <summary>
    /// A very fast, lock free, ordered collection to which items can be added, but never removed.
    /// It utilizes a generic type <typeparamref name="TPayload"/> to define the type of items stored in the registry.
    /// </summary>
    internal abstract class EverGrowingCollection<TPayload>
    {
        private static readonly int[] CapacityRiskThresholds = [1024, 4096, 16384, 65536];

        private int _highestReportedRiskThreshold = -1;

        protected IDatadogLogger Log { get; } = DatadogLogging.GetLoggerFor(typeof(EverGrowingCollection<TPayload>));

        protected object ItemsLocker { get; } = new object();

        protected TPayload[] Items { get; private set; } = new TPayload[16];

        private int Capacity
        {
            set
            {
                if (value != Items.Length)
                {
                    var newItems = new TPayload[value];

                    // ReSharper disable once InconsistentlySynchronizedField
                    Array.Copy(Items, 0, newItems, 0, Items.Length);

                    // ReSharper disable once InconsistentlySynchronizedField
                    Items = newItems;
                }
            }
        }

        private void EnsureCapacity(int min)
        {
            // Enlarge the _items array as needed. Note that there is an implicit by-design memory-leak here,
            // in the sense that this unbounded collection can only grow and is never shrunk.
            // After removing the default 100 probes cap, a long-running process with high probe churn can
            // retain large backing arrays. We keep this implementation for now and emit warning logs as
            // capacity crosses risk thresholds so we can monitor and decide if additional safeguards are needed.
            if (Items.Length < min)
            {
                var previousCapacity = Items.Length;
                var newCapacity = Items.Length * 2;
                if (newCapacity < min)
                {
                    newCapacity = min + 1;
                }

                Capacity = newCapacity;
                ReportCapacityRiskIfNeeded(previousCapacity, newCapacity);
            }
        }

        private void ReportCapacityRiskIfNeeded(int previousCapacity, int newCapacity)
        {
            for (var i = _highestReportedRiskThreshold + 1; i < CapacityRiskThresholds.Length; i++)
            {
                var threshold = CapacityRiskThresholds[i];
                if (newCapacity >= threshold)
                {
                    Log.Warning<int, int, int>(
                        "Collection capacity increased from {PreviousCapacity} to {NewCapacity} and reached risk threshold {Threshold} for retained memory.",
                        previousCapacity,
                        newCapacity,
                        threshold);
                    _highestReportedRiskThreshold = i;
                }
                else
                {
                    break;
                }
            }
        }

        public void Remove(int index)
        {
            lock (ItemsLocker)
            {
                Items[index] = default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void EnlargeCapacity(int index)
        {
            if (index == Items.Length)
            {
                EnsureCapacity(index + 1);
            }
            else if (index > Items.Length)
            {
                EnsureCapacity(index);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IndexExists(int index)
        {
            // Check if there's a `payload` associated with the given index
            if (index < Items.Length)
            {
                // ReSharper disable once InconsistentlySynchronizedField
                ref var payload = ref Items[index];

                if (!IsEmpty(ref payload))
                {
                    return true;
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref TPayload Get(int index)
        {
            return ref Items[index];
        }

        protected abstract bool IsEmpty(ref TPayload payload);
    }
}

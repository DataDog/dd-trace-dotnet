// <copyright file="FakeOtelThreadContextSlotProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Datadog.Trace.OtelThreadContext;

namespace Datadog.Trace.Tests.OtelThreadContext
{
    /// <summary>
    /// Stands in for the native <c>otel_thread_ctx_v1</c> thread-local slot, so the publisher can be
    /// exercised on any platform without the native tracer. Hands out one distinct pointer-sized slot per
    /// calling thread, exactly as ELF thread-local storage would.
    /// </summary>
    internal sealed unsafe class FakeOtelThreadContextSlotProvider : IOtelThreadContextSlotProvider, IDisposable
    {
        private readonly object _lock = new();
        private readonly Dictionary<int, IntPtr> _slotsByThread = new();
        private readonly List<IntPtr> _allocations = new();
        private readonly bool _returnNull;
        private int _callCount;

        public FakeOtelThreadContextSlotProvider(bool returnNull = false)
        {
            _returnNull = returnNull;
        }

        /// <summary>
        /// Gets the number of times a slot has been requested. The design guarantees this is once per
        /// thread, no matter how many spans are activated.
        /// </summary>
        public int CallCount => Volatile.Read(ref _callCount);

        public IntPtr GetSlot()
        {
            Interlocked.Increment(ref _callCount);

            if (_returnNull)
            {
                return IntPtr.Zero;
            }

            lock (_lock)
            {
                var slot = Marshal.AllocHGlobal(IntPtr.Size);
                *(IntPtr*)slot = IntPtr.Zero;
                _allocations.Add(slot);
                _slotsByThread[Environment.CurrentManagedThreadId] = slot;
                return slot;
            }
        }

        /// <summary>
        /// Gets the address of the record the current thread published, or zero if it published none.
        /// </summary>
        public IntPtr GetPublishedRecord()
        {
            lock (_lock)
            {
                return _slotsByThread.TryGetValue(Environment.CurrentManagedThreadId, out var slot)
                           ? *(IntPtr*)slot
                           : IntPtr.Zero;
            }
        }

        /// <summary>
        /// Copies the record the current thread published. Fails if nothing has been published.
        /// </summary>
        public byte[] ReadPublishedRecord()
        {
            var record = GetPublishedRecord();

            if (record == IntPtr.Zero)
            {
                throw new InvalidOperationException("No thread context record has been published on this thread.");
            }

            var buffer = new byte[OtelThreadContextRecord.Size];
            Marshal.Copy(record, buffer, 0, buffer.Length);
            return buffer;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var allocation in _allocations)
                {
                    Marshal.FreeHGlobal(allocation);
                }

                _allocations.Clear();
                _slotsByThread.Clear();
            }
        }
    }
}

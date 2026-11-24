// <copyright file="FakeOtelThreadContextRecordProvider.cs" company="Datadog">
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
    /// Stands in for the native owner of OTEP 4947 records, so the publisher can be exercised on any
    /// platform without the native tracer. Hands out one stable, zeroed record per calling thread.
    /// </summary>
    internal sealed class FakeOtelThreadContextRecordProvider : IOtelThreadContextRecordProvider, IDisposable
    {
        private static readonly byte[] EmptyRecord = new byte[OtelThreadContextRecord.Size];

        private readonly object _lock = new();
        private readonly Dictionary<int, IntPtr> _recordsByThread = new();
        private readonly List<IntPtr> _allocations = new();
        private readonly bool _returnNull;
        private int _callCount;

        public FakeOtelThreadContextRecordProvider(bool returnNull = false)
        {
            _returnNull = returnNull;
        }

        /// <summary>
        /// Gets the number of times a record has been requested. The publisher requests it once per
        /// thread, no matter how many spans are activated.
        /// </summary>
        public int CallCount => Volatile.Read(ref _callCount);

        public IntPtr GetRecord()
        {
            Interlocked.Increment(ref _callCount);

            if (_returnNull)
            {
                return IntPtr.Zero;
            }

            lock (_lock)
            {
                var threadId = Environment.CurrentManagedThreadId;
                if (_recordsByThread.TryGetValue(threadId, out var existing))
                {
                    return existing;
                }

                var record = Marshal.AllocHGlobal(OtelThreadContextRecord.Size);
                Marshal.Copy(EmptyRecord, 0, record, EmptyRecord.Length);
                _allocations.Add(record);
                _recordsByThread[threadId] = record;
                return record;
            }
        }

        /// <summary>
        /// Gets the current thread's record, or zero if it has not acquired one.
        /// </summary>
        public IntPtr GetPublishedRecord()
        {
            lock (_lock)
            {
                return _recordsByThread.TryGetValue(Environment.CurrentManagedThreadId, out var record)
                           ? record
                           : IntPtr.Zero;
            }
        }

        /// <summary>
        /// Copies the current thread's record. Fails if nothing has been published.
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
                _recordsByThread.Clear();
            }
        }
    }
}

// <copyright file="StackSnapshotsBufferSegment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Threading;

using Datadog.Util;

namespace Datadog.Profiler
{
    internal sealed class StackSnapshotsBufferSegment : IDisposable
    {
        private IntPtr _nativeSegmentObjectPtr;

        public StackSnapshotsBufferSegment(
                IntPtr nativeSegmentObjectPtr,
                IntPtr segmentBufferStartAddress,
                uint segmentByteCount,
                uint snapshotsCount,
                ulong segmentUnixTimeUtcRangeStart,
                ulong segmentUnixTimeUtcRangeEnd)
            : this(
                  nativeSegmentObjectPtr,
                  segmentBufferStartAddress,
                  segmentByteCount,
                  snapshotsCount,
                  UnixTimeToDTO(segmentUnixTimeUtcRangeStart, nameof(segmentUnixTimeUtcRangeStart)),
                  UnixTimeToDTO(segmentUnixTimeUtcRangeEnd, nameof(segmentUnixTimeUtcRangeEnd)))
        {
        }

        public StackSnapshotsBufferSegment(
                IntPtr nativeSegmentObjectPtr,
                IntPtr segmentBufferStartAddress,
                uint segmentByteCount,
                uint snapshotsCount,
                DateTimeOffset timeRangeStart,
                DateTimeOffset timeRangeEnd)
        {
            if (nativeSegmentObjectPtr == IntPtr.Zero)
            {
                throw new ArgumentNullException($"{nameof(nativeSegmentObjectPtr)}");
            }

            if (segmentBufferStartAddress == IntPtr.Zero)
            {
                throw new ArgumentNullException($"{nameof(segmentBufferStartAddress)}");
            }

            if (timeRangeStart > timeRangeEnd)
            {
                throw new ArgumentException($"Time range start"
                                          + $" ({nameof(timeRangeStart)}={Format.AsReadablePreciseUnconverted(timeRangeStart)})"
                                          + $" may not come after the time range end"
                                          + $" ({nameof(timeRangeEnd)}={Format.AsReadablePreciseUnconverted(timeRangeEnd)}).");
            }

            _nativeSegmentObjectPtr = nativeSegmentObjectPtr;
            SegmentBufferStartAddress = segmentBufferStartAddress;
            SegmentByteCount = segmentByteCount;
            SnapshotsCount = snapshotsCount;
            TimeRangeStart = timeRangeStart;
            TimeRangeEnd = timeRangeEnd;
        }

        ~StackSnapshotsBufferSegment()
        {
            ReleaseNativeSegmentObject();
        }

        public IntPtr NativeSegmentObjectPtr
        {
            get { return _nativeSegmentObjectPtr; }
        }

        public IntPtr SegmentBufferStartAddress { get; }

        public uint SegmentByteCount { get; }

        public uint SnapshotsCount { get; }

        public DateTimeOffset TimeRangeStart { get; }

        public DateTimeOffset TimeRangeEnd { get; }

        public bool IsDisposed()
        {
            return (_nativeSegmentObjectPtr == IntPtr.Zero);
        }

        public void Dispose()
        {
            if (ReleaseNativeSegmentObject())
            {
                GC.SuppressFinalize(this);
            }
        }

        public SnapshotEnumerator EnumerateSnapshots()
        {
            return new SnapshotEnumerator(this);
        }

        internal void DisposeWithoutNativeRelease()
        {
            Interlocked.Exchange(ref _nativeSegmentObjectPtr, IntPtr.Zero);
            GC.SuppressFinalize(this);
        }

        private static DateTimeOffset UnixTimeToDTO(ulong unixTimeUtc, string varNameForValidation)
        {
            if (unixTimeUtc >= long.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                            varNameForValidation,
                            $"Time must be within the Int64 range, but specified value is {unixTimeUtc}.");
            }

            DateTimeOffset dto = Converter.UnixTimeSeconds.ToDateTimeOffset((long)unixTimeUtc);
            return dto;
        }

        private bool ReleaseNativeSegmentObject()
        {
            IntPtr nativeSegmentObjectPtr = Interlocked.Exchange(ref _nativeSegmentObjectPtr, IntPtr.Zero);
            if (nativeSegmentObjectPtr == IntPtr.Zero)
            {
                return true;
            }

            try
            {
                bool isReleased = NativeInterop.TryMakeSegmentAvailableForWrite(nativeSegmentObjectPtr);
                if (!isReleased)
                {
                    _nativeSegmentObjectPtr = nativeSegmentObjectPtr;
                }

                return isReleased;
            }
            catch (ClrShutdownException ex)
            {
                Log.Info(nameof(StackSnapshotsBufferSegment), ex.Message);
                _nativeSegmentObjectPtr = nativeSegmentObjectPtr;
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(nameof(StackSnapshotsBufferSegment), ex);
                _nativeSegmentObjectPtr = nativeSegmentObjectPtr;
                return false;
            }
        }

        public struct SnapshotEnumerator
        {
            private readonly StackSnapshotsBufferSegment _ownerSegment;
            private readonly uint _snapshotsCount;
            private IntPtr _nextSnapshotDataPtr;
            private uint _currentSnapshotIndex;

            public SnapshotEnumerator(StackSnapshotsBufferSegment ownerSegment)
            {
                _ownerSegment = ownerSegment;
                _snapshotsCount = ownerSegment.SnapshotsCount;
                _currentSnapshotIndex = 0;
                _nextSnapshotDataPtr = (_snapshotsCount < 1)
                                        ? IntPtr.Zero
                                        : _ownerSegment.SegmentBufferStartAddress;
            }

            public uint CurrentSnapshotIndex
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    return _currentSnapshotIndex;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                _currentSnapshotIndex++;

                if (_currentSnapshotIndex < _snapshotsCount)
                {
                    _nextSnapshotDataPtr = StackSnapshotResult.GetNextSnapshotMemoryPointerUnsafe(_nextSnapshotDataPtr);
                    return true;
                }
                else
                {
                    _nextSnapshotDataPtr = IntPtr.Zero;
                    return false;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public StackSnapshotResult GetCurrent()
            {
                return new StackSnapshotResult(_nextSnapshotDataPtr);
            }
        }
    }
}
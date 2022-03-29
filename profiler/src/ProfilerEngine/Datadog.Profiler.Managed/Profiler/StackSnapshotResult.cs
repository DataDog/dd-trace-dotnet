// <copyright file="StackSnapshotResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#pragma warning disable SA1407

namespace Datadog.Profiler
{
    /// <summary>
    ///  —–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—
    /// | See memory layout map in the "StackSnapshotResult.h" header file. |
    ///  —–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—–—
    /// </summary>
    internal readonly ref struct StackSnapshotResult
    {
        private readonly IntPtr _dataPtr;

        public StackSnapshotResult(IntPtr dataPtr)
        {
            _dataPtr = dataPtr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StackSnapshotResult CreateNewInvalid()
        {
            return new StackSnapshotResult(IntPtr.Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetNextSnapshotMemoryPointerUnsafe(IntPtr snapshotDataPtr)
        {
            return snapshotDataPtr + (26 + 9 * Marshal.ReadInt16(ptr: snapshotDataPtr, ofs: 24));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsValid()
        {
            return (_dataPtr != IntPtr.Zero);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt64 GetRepresentedDurationNanoseconds()
        {
            return IsValid() ? GetRepresentedDurationNanosecondsUnsafe() : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt64 GetRepresentedDurationNanosecondsUnsafe()
        {
            return (UInt64)Marshal.ReadInt64(ptr: _dataPtr, ofs: 0);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt32 GetProfilerThreadInfoId()
        {
            return IsValid() ? GetProfilerThreadInfoIdUnsafe() : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt32 GetProfilerThreadInfoIdUnsafe()
        {
            return (UInt32)Marshal.ReadInt32(ptr: _dataPtr, ofs: 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt64 GetProfilerAppDomainId()
        {
            return IsValid() ? GetProfilerAppDomainIdUnsafe() : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt64 GetProfilerAppDomainIdUnsafe()
        {
            return (UInt64)Marshal.ReadInt64(ptr: _dataPtr, ofs: 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 GetFramesCount()
        {
            return IsValid() ? GetFramesCountUnsafe() : (UInt16)0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UInt16 GetFramesCountUnsafe()
        {
            return (UInt16)Marshal.ReadInt16(ptr: _dataPtr, ofs: 24);
        }

        // frameInfoCode is the ClrFunctionId OR the NativeIP, depending on codeKind
        public bool TryGetFrameAtIndex(UInt16 index, out StackFrameCodeKind codeKind, out UInt64 frameInfoCode)
        {
            if (IsValid() && 0 <= index && index < GetFramesCountUnsafe())
            {
                GetFrameAtIndexUnsafe(index, out codeKind, out frameInfoCode);
                return true;
            }
            else
            {
                codeKind = StackFrameCodeKind.Unknown;
                frameInfoCode = 0;
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetFrameAtIndexUnsafe(UInt16 index, out StackFrameCodeKind codeKind, out UInt64 frameInfoCode)
        {
            codeKind = (StackFrameCodeKind)Marshal.ReadByte(ptr: _dataPtr, ofs: 26 + index * 9);
            frameInfoCode = (UInt64)Marshal.ReadInt64(ptr: _dataPtr, ofs: 27 + index * 9);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int32 GetUsedBytesCount()
        {
            return IsValid() ? GetUsedBytesCountUnsafe() : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Int32 GetUsedBytesCountUnsafe()
        {
            return 26 + 9 * (Int32)GetFramesCountUnsafe();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr GetNextSnapshotMemoryPointer()
        {
            return IsValid() ? GetNextSnapshotMemoryPointerUnsafe() : IntPtr.Zero;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IntPtr GetNextSnapshotMemoryPointerUnsafe()
        {
            return StackSnapshotResult.GetNextSnapshotMemoryPointerUnsafe(_dataPtr);
        }
    }
}
#pragma warning restore SA1407


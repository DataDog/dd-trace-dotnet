// <copyright file="UnmanagedMemorySegment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler;

internal static class UnmanagedMemorySegment
{
    private const int SizeOfMemorySegment = 1 * 1024 * 1024;
    private static readonly List<IntPtr> Segments = new(5);
    private static IntPtr _currentSegment = IntPtr.Zero;
    private static int _segmentOffset;

    public static unsafe IntPtr Allocate(int bytesCount)
    {
        if (_currentSegment == IntPtr.Zero || SizeOfMemorySegment - _segmentOffset < bytesCount)
        {
            _currentSegment = Marshal.AllocHGlobal(SizeOfMemorySegment);
            _segmentOffset = 0;
            Segments.Add(_currentSegment);
        }

        var allocation = (IntPtr)((byte*)_currentSegment + _segmentOffset);
        _segmentOffset += bytesCount;
        return allocation;
    }

    public static void Free()
    {
        for (var i = 0; i < Segments.Count; i++)
        {
            Marshal.FreeHGlobal(Segments[i]);
        }

        _currentSegment = IntPtr.Zero;
        _segmentOffset = 0;
        Segments.Clear();
    }

    public static unsafe IntPtr AllocateAndWriteUtf16String(string value)
    {
        var stringPtrSize = value.Length * 2;
        var stringPtr = Allocate(stringPtrSize + 2);
        fixed (char* sPointer = value)
        {
            Buffer.MemoryCopy(sPointer, (void*)stringPtr, stringPtrSize, stringPtrSize);
            ((char*)stringPtr)[value.Length] = '\0';
        }

        return stringPtr;
    }
}

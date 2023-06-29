// <copyright file="UnmanagedMemorySegment.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Datadog.Trace.ClrProfiler;

internal static class UnmanagedMemorySegment
{
    private const int SizeOfMemorySegment = 1 * 1024 * 1024;
    private static readonly List<IntPtr> Segments;
    private static IntPtr _currentSegment;
    private static int _segmentOffset;

    public static readonly int SizeOfPointer = Marshal.SizeOf(typeof(IntPtr));

    static UnmanagedMemorySegment()
    {
        Segments = new(5);
        CreateSegment();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateSegment()
    {
        _currentSegment = Marshal.AllocCoTaskMem(SizeOfMemorySegment);
        _segmentOffset = 0;
        Segments.Add(_currentSegment);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr Allocate(int bytesCount)
    {
        var offset = _segmentOffset;
        if (SizeOfMemorySegment - offset < bytesCount)
        {
            CreateSegment();
        }

        _segmentOffset = offset + bytesCount;
        return _currentSegment + offset;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Free()
    {
        for (var i = 0; i < Segments.Count; i++)
        {
            Marshal.FreeCoTaskMem(Segments[i]);
        }

        _currentSegment = IntPtr.Zero;
        _segmentOffset = 0;
        Segments.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

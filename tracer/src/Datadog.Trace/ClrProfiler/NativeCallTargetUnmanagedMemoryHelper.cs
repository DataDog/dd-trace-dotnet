// <copyright file="NativeCallTargetUnmanagedMemoryHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler;

internal static class NativeCallTargetUnmanagedMemoryHelper
{
    private const int SizeOfMemorySegment = 750 * 1024; // 750Kb
    private static readonly int SizeOfPointer = Marshal.SizeOf(typeof(IntPtr));
    private static readonly List<IntPtr> Segments;
    private static IntPtr _currentSegment;
    private static int _segmentOffset;

    static NativeCallTargetUnmanagedMemoryHelper()
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
            if (bytesCount > SizeOfMemorySegment)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException("The number of bytes is bigger than the max size of the memory segment.");
            }

            CreateSegment();
            offset = 0;
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
        if (value is null)
        {
            return IntPtr.Zero;
        }

        var stringPtrSize = value.Length * 2;
        var stringPtr = Allocate(stringPtrSize + 2);
        fixed (char* sPointer = value)
        {
            Buffer.MemoryCopy(sPointer, (void*)stringPtr, stringPtrSize, stringPtrSize);
            ((char*)stringPtr)[value.Length] = '\0';
        }

        return stringPtr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(string[] array)
    {
        if (array is null || array.Length == 0)
        {
            return IntPtr.Zero;
        }

        var unmanagedArray = Allocate(array.Length * SizeOfPointer);
        for (var i = 0; i < array.Length; i++)
        {
            Marshal.WriteIntPtr(unmanagedArray, i * SizeOfPointer, AllocateAndWriteUtf16String(array[i]));
        }

        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(string arrayItem1)
    {
        var unmanagedArray = Allocate(SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2)
    {
        var unmanagedArray = Allocate(2 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3)
    {
        var unmanagedArray = Allocate(3 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3,
        string arrayItem4)
    {
        var unmanagedArray = Allocate(4 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        Marshal.WriteIntPtr(unmanagedArray, 3 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem4));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3,
        string arrayItem4,
        string arrayItem5)
    {
        var unmanagedArray = Allocate(5 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        Marshal.WriteIntPtr(unmanagedArray, 3 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem4));
        Marshal.WriteIntPtr(unmanagedArray, 4 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem5));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3,
        string arrayItem4,
        string arrayItem5,
        string arrayItem6)
    {
        var unmanagedArray = Allocate(6 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        Marshal.WriteIntPtr(unmanagedArray, 3 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem4));
        Marshal.WriteIntPtr(unmanagedArray, 4 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem5));
        Marshal.WriteIntPtr(unmanagedArray, 5 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem6));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3,
        string arrayItem4,
        string arrayItem5,
        string arrayItem6,
        string arrayItem7)
    {
        var unmanagedArray = Allocate(7 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        Marshal.WriteIntPtr(unmanagedArray, 3 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem4));
        Marshal.WriteIntPtr(unmanagedArray, 4 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem5));
        Marshal.WriteIntPtr(unmanagedArray, 5 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem6));
        Marshal.WriteIntPtr(unmanagedArray, 6 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem7));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3,
        string arrayItem4,
        string arrayItem5,
        string arrayItem6,
        string arrayItem7,
        string arrayItem8)
    {
        var unmanagedArray = Allocate(8 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        Marshal.WriteIntPtr(unmanagedArray, 3 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem4));
        Marshal.WriteIntPtr(unmanagedArray, 4 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem5));
        Marshal.WriteIntPtr(unmanagedArray, 5 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem6));
        Marshal.WriteIntPtr(unmanagedArray, 6 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem7));
        Marshal.WriteIntPtr(unmanagedArray, 7 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem8));
        return unmanagedArray;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr AllocateAndWriteUtf16StringArray(
        string arrayItem1,
        string arrayItem2,
        string arrayItem3,
        string arrayItem4,
        string arrayItem5,
        string arrayItem6,
        string arrayItem7,
        string arrayItem8,
        string arrayItem9)
    {
        var unmanagedArray = Allocate(9 * SizeOfPointer);
        Marshal.WriteIntPtr(unmanagedArray, 0, AllocateAndWriteUtf16String(arrayItem1));
        Marshal.WriteIntPtr(unmanagedArray, 1 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem2));
        Marshal.WriteIntPtr(unmanagedArray, 2 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem3));
        Marshal.WriteIntPtr(unmanagedArray, 3 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem4));
        Marshal.WriteIntPtr(unmanagedArray, 4 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem5));
        Marshal.WriteIntPtr(unmanagedArray, 5 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem6));
        Marshal.WriteIntPtr(unmanagedArray, 6 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem7));
        Marshal.WriteIntPtr(unmanagedArray, 7 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem8));
        Marshal.WriteIntPtr(unmanagedArray, 8 * SizeOfPointer, AllocateAndWriteUtf16String(arrayItem9));
        return unmanagedArray;
    }
}

// <copyright file="FileBitmap.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NETCOREAPP3_0_OR_GREATER
using System.Runtime.Intrinsics;
#endif
using System.Text;
using Unsafe = Datadog.Trace.VendoredMicrosoftCode.System.Runtime.CompilerServices.Unsafe.Unsafe;

namespace Datadog.Trace.Ci.Coverage.Util;

internal readonly unsafe ref struct FileBitmap
{
    private readonly int _size;
    private readonly bool _disposable;
    private readonly StrongBox<GCHandle>? _handle;
    private readonly byte* _bitmap;

    public FileBitmap(byte* buffer, int size)
    {
        _handle = null;
        _size = size;
        _bitmap = buffer;
        for (var i = 0; i < _size; i++)
        {
            _bitmap[i] = 0;
        }

        _disposable = false;
    }

    public FileBitmap(byte[] bitmapArray)
    {
        _size = bitmapArray.Length;
        _handle = new StrongBox<GCHandle>(GCHandle.Alloc(bitmapArray, GCHandleType.Pinned));
        _bitmap = (byte*)Unsafe.AsPointer(ref bitmapArray.FastGetReference(0));
        _disposable = true;
    }

    public int Size => _size;

    public static FileBitmap operator |(FileBitmap fileBitmapA, FileBitmap fileBitmapB)
    {
        if (fileBitmapA._size != fileBitmapB._size)
        {
            return default;
        }

        var resBitmap = new FileBitmap(new byte[fileBitmapA._size]);
        var index = 0;

#if NET8_0_OR_GREATER
        for (; fileBitmapA._size - index >= 64; index += 64)
        {
            var a = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }
#endif

#if NET7_0_OR_GREATER
        for (; fileBitmapA._size - index >= 32; index += 32)
        {
            var a = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }

        for (; fileBitmapA._size - index >= 16; index += 16)
        {
            var a = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }
#endif

        for (; fileBitmapA._size - index >= 8; index += 8)
        {
            var a = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }

        for (; fileBitmapA._size - index >= 4; index += 4)
        {
            var a = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a | b);
        }

        for (; fileBitmapA._size - index >= 2; index += 2)
        {
            var a = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, (ushort)(a | b));
        }

        for (; index < fileBitmapA._size; index++)
        {
            resBitmap._bitmap[index] = (byte)(fileBitmapA._bitmap[index] | fileBitmapB._bitmap[index]);
        }

        return resBitmap;
    }

    public static FileBitmap operator &(FileBitmap fileBitmapA, FileBitmap fileBitmapB)
    {
        if (fileBitmapA._size != fileBitmapB._size)
        {
            return default;
        }

        var resBitmap = new FileBitmap(new byte[fileBitmapA._size]);
        var index = 0;

#if NET8_0_OR_GREATER
        for (; fileBitmapA._size - index >= 64; index += 64)
        {
            var a = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }
#endif

#if NET7_0_OR_GREATER
        for (; fileBitmapA._size - index >= 32; index += 32)
        {
            var a = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }

        for (; fileBitmapA._size - index >= 16; index += 16)
        {
            var a = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }
#endif

        for (; fileBitmapA._size - index >= 8; index += 8)
        {
            var a = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }

        for (; fileBitmapA._size - index >= 4; index += 4)
        {
            var a = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, a & b);
        }

        for (; fileBitmapA._size - index >= 2; index += 2)
        {
            var a = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapA._bitmap + index));
            var b = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(fileBitmapB._bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, (ushort)(a & b));
        }

        for (; index < fileBitmapA._size; index++)
        {
            resBitmap._bitmap[index] = (byte)(fileBitmapA._bitmap[index] & fileBitmapB._bitmap[index]);
        }

        return resBitmap;
    }

    public static FileBitmap operator ~(FileBitmap fileBitmap)
    {
        var resBitmap = new FileBitmap(new byte[fileBitmap._size]);
        var index = 0;
        var size = fileBitmap._size;
        var bitmap = fileBitmap._bitmap;

#if NET8_0_OR_GREATER
        for (; size - index >= 64; index += 64)
        {
            var a = Unsafe.ReadUnaligned<Vector512<byte>>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }
#endif

#if NET7_0_OR_GREATER
        for (; size - index >= 32; index += 32)
        {
            var a = Unsafe.ReadUnaligned<Vector256<byte>>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }

        for (; size - index >= 16; index += 16)
        {
            var a = Unsafe.ReadUnaligned<Vector128<byte>>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }
#endif

        for (; size - index >= 8; index += 8)
        {
            var a = Unsafe.ReadUnaligned<ulong>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }

        for (; size - index >= 4; index += 4)
        {
            var a = Unsafe.ReadUnaligned<uint>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, ~a);
        }

        for (; size - index >= 2; index += 2)
        {
            var a = Unsafe.ReadUnaligned<ushort>(ref Unsafe.AsRef<byte>(bitmap + index));
            Unsafe.WriteUnaligned(resBitmap._bitmap + index, (ushort)~a);
        }

        for (; index < size; index++)
        {
            resBitmap._bitmap[index] = (byte)~bitmap[index];
        }

        return resBitmap;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSize(int numOfLines) => (numOfLines + 7) / 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int line)
    {
        /*
            L0000: dec edx
            L0001: mov eax, edx
            L0003: shr eax, 3
            L0006: add eax, [ecx+0xc]
            L0009: mov ecx, edx
            L000b: and ecx, 7
            L000e: mov edx, 0x80
            L0013: sar edx, cl
            L0015: movzx edx, dl
            L0018: or [eax], dl
            L001a: ret
         */
        var idx = (uint)line - 1;
        _bitmap[idx >> 3] |= (byte)(128 >> (int)(idx & 7));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Get(int line)
    {
        /*
            L0000: dec edx
            L0001: mov ecx, [ecx+0xc]
            L0004: mov eax, edx
            L0006: shr eax, 3
            L0009: movzx eax, byte ptr [ecx+eax]
            L000d: mov ecx, edx
            L000f: and ecx, 7
            L0012: mov edx, 0x80
            L0017: sar edx, cl
            L0019: movzx edx, dl
            L001c: test edx, eax
            L001e: setne al
            L0021: movzx eax, al
            L0024: ret
         */
        var idx = (uint)line - 1;
        return (_bitmap[idx >> 3] & (byte)(128 >> (int)(idx & 7))) != 0;
    }

    public int CountActiveBits()
    {
        int count = 0;
        for (var i = 0; i < _size; i++)
        {
            // let's try to count 8 bytes at a time (reinterpret byte as ulong)
            if (i + 7 < _size)
            {
#if NETCOREAPP3_0_OR_GREATER
                // SIMD algorithm (popcnt)
                count += BitOperations.PopCount(*(ulong*)(_bitmap + i));
#else
                // Hamming weight algorithm
                count += SlowLongPopCount(*(ulong*)(_bitmap + i));
#endif
                i += 7;
                continue;
            }

            // let's try to count 4 bytes at a time (reinterpret byte as uint)
            if (i + 3 < _size)
            {
#if NETCOREAPP3_0_OR_GREATER
                // SIMD algorithm (popcnt)
                count += BitOperations.PopCount(*(uint*)(_bitmap + i));
#else
                // Hamming weight algorithm
                count += SlowIntPopCount(*(uint*)(_bitmap + i));
#endif
                i += 3;
                continue;
            }

            // let's count byte per byte
#if NETCOREAPP3_0_OR_GREATER
            // SIMD algorithm (popcnt)
            count += BitOperations.PopCount(_bitmap[i]);
#else
            // Hamming weight algorithm
            count += SlowIntPopCount(_bitmap[i]);
#endif
        }

        return count;

#if !NETCOREAPP3_0_OR_GREATER
        // Hamming weight algorithm for ulong
        static int SlowLongPopCount(ulong value)
        {
            const ulong c1 = 0x_55555555_55555555ul;
            const ulong c2 = 0x_33333333_33333333ul;
            const ulong c3 = 0x_0F0F0F0F_0F0F0F0Ful;
            const ulong c4 = 0x_01010101_01010101ul;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (((value + (value >> 4)) & c3) * c4) >> 56;

            return (int)value;
        }

        // Hamming weight algorithm for uint
        static int SlowIntPopCount(uint value)
        {
            const uint c1 = 0x_55555555u;
            const uint c2 = 0x_33333333u;
            const uint c3 = 0x_0F0F0F0Fu;
            const uint c4 = 0x_01010101u;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (((value + (value >> 4)) & c3) * c4) >> 24;

            return (int)value;
        }
#endif
    }

    public void Dispose()
    {
        if (_disposable && _handle is not null)
        {
            if (_handle.Value.IsAllocated)
            {
                _handle.Value.Free();
            }
        }
    }

    public void Write(byte[] array)
    {
        fixed (byte* arrayPtr = array)
        {
            Buffer.MemoryCopy(_bitmap, arrayPtr, array.Length, _size);
        }
    }

    public byte[] ToArray()
    {
        var array = new byte[_size];
        Write(array);
        return array;
    }

    internal byte[]? GetInternalArrayOrToArray()
    {
        if (_handle?.Value.IsAllocated == true)
        {
            return _handle.Value.Target as byte[] ?? ToArray();
        }

        return ToArray();
    }

    public override string ToString()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < _size; i++)
        {
            sb.Append(Convert.ToString(_bitmap[i], 2).PadLeft(8, '0'));
        }

        return sb.ToString();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(_bitmap, _size);

    public struct Enumerator(byte* bitMap, int size) : IEnumerator<byte>
    {
        private int _index = -1;

        public byte Current => bitMap[_index];

        object IEnumerator.Current => Current;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_index < size;

        public void Reset()
        {
            _index = 0;
        }

        public void Dispose()
        {
            bitMap = null;
            size = 0;
            _index = 0;
        }
    }
}

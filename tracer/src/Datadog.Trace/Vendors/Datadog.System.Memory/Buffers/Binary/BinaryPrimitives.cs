﻿// Decompiled with JetBrains decompiler
// Type: System.Buffers.Binary.BinaryPrimitives
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Runtime.CompilerServices;
using Datadog.System.Runtime.InteropServices;

namespace Datadog.System.Buffers.Binary
{
    public static class BinaryPrimitives
  {
    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static sbyte ReverseEndianness(sbyte value) => value;

    [MethodImpl((MethodImplOptions) 256)]
    public static short ReverseEndianness(short value) => (short) (((int) value & (int) byte.MaxValue) << 8 | ((int) value & 65280) >> 8);

    [MethodImpl((MethodImplOptions) 256)]
    public static int ReverseEndianness(int value) => (int) BinaryPrimitives.ReverseEndianness((uint) value);

    [MethodImpl((MethodImplOptions) 256)]
    public static long ReverseEndianness(long value) => (long) BinaryPrimitives.ReverseEndianness((ulong) value);

    [MethodImpl((MethodImplOptions) 256)]
    public static byte ReverseEndianness(byte value) => value;

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static ushort ReverseEndianness(ushort value) => (ushort) (((int) value >> 8) + ((int) value << 8));

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static uint ReverseEndianness(uint value)
    {
      uint num1 = value & 16711935U;
      uint num2 = value & 4278255360U;
      return (uint) (((int) (num1 >> 8) | (int) num1 << 24) + ((int) num2 << 8 | (int) (num2 >> 24)));
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static ulong ReverseEndianness(ulong value) => ((ulong) BinaryPrimitives.ReverseEndianness((uint) value) << 32) + (ulong) BinaryPrimitives.ReverseEndianness((uint) (value >> 32));

    [MethodImpl((MethodImplOptions) 256)]
    public static short ReadInt16BigEndian(ReadOnlySpan<byte> source)
    {
      short num = MemoryMarshal.Read<short>(source);
      if (BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int ReadInt32BigEndian(ReadOnlySpan<byte> source)
    {
      int num = MemoryMarshal.Read<int>(source);
      if (BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static long ReadInt64BigEndian(ReadOnlySpan<byte> source)
    {
      long num = MemoryMarshal.Read<long>(source);
      if (BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static ushort ReadUInt16BigEndian(ReadOnlySpan<byte> source)
    {
      ushort num = MemoryMarshal.Read<ushort>(source);
      if (BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static uint ReadUInt32BigEndian(ReadOnlySpan<byte> source)
    {
      uint num = MemoryMarshal.Read<uint>(source);
      if (BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> source)
    {
      ulong num = MemoryMarshal.Read<ulong>(source);
      if (BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadInt16BigEndian(ReadOnlySpan<byte> source, out short value)
    {
      bool flag = MemoryMarshal.TryRead<short>(source, out value);
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadInt32BigEndian(ReadOnlySpan<byte> source, out int value)
    {
      bool flag = MemoryMarshal.TryRead<int>(source, out value);
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadInt64BigEndian(ReadOnlySpan<byte> source, out long value)
    {
      bool flag = MemoryMarshal.TryRead<long>(source, out value);
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadUInt16BigEndian(ReadOnlySpan<byte> source, out ushort value)
    {
      bool flag = MemoryMarshal.TryRead<ushort>(source, out value);
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadUInt32BigEndian(ReadOnlySpan<byte> source, out uint value)
    {
      bool flag = MemoryMarshal.TryRead<uint>(source, out value);
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadUInt64BigEndian(ReadOnlySpan<byte> source, out ulong value)
    {
      bool flag = MemoryMarshal.TryRead<ulong>(source, out value);
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static short ReadInt16LittleEndian(ReadOnlySpan<byte> source)
    {
      short num = MemoryMarshal.Read<short>(source);
      if (!BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int ReadInt32LittleEndian(ReadOnlySpan<byte> source)
    {
      int num = MemoryMarshal.Read<int>(source);
      if (!BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static long ReadInt64LittleEndian(ReadOnlySpan<byte> source)
    {
      long num = MemoryMarshal.Read<long>(source);
      if (!BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> source)
    {
      ushort num = MemoryMarshal.Read<ushort>(source);
      if (!BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> source)
    {
      uint num = MemoryMarshal.Read<uint>(source);
      if (!BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> source)
    {
      ulong num = MemoryMarshal.Read<ulong>(source);
      if (!BitConverter.IsLittleEndian)
        num = BinaryPrimitives.ReverseEndianness(num);
      return num;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadInt16LittleEndian(ReadOnlySpan<byte> source, out short value)
    {
      bool flag = MemoryMarshal.TryRead<short>(source, out value);
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadInt32LittleEndian(ReadOnlySpan<byte> source, out int value)
    {
      bool flag = MemoryMarshal.TryRead<int>(source, out value);
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadInt64LittleEndian(ReadOnlySpan<byte> source, out long value)
    {
      bool flag = MemoryMarshal.TryRead<long>(source, out value);
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadUInt16LittleEndian(ReadOnlySpan<byte> source, out ushort value)
    {
      bool flag = MemoryMarshal.TryRead<ushort>(source, out value);
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadUInt32LittleEndian(ReadOnlySpan<byte> source, out uint value)
    {
      bool flag = MemoryMarshal.TryRead<uint>(source, out value);
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryReadUInt64LittleEndian(ReadOnlySpan<byte> source, out ulong value)
    {
      bool flag = MemoryMarshal.TryRead<ulong>(source, out value);
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return flag;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteInt16BigEndian(Span<byte> destination, short value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<short>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteInt32BigEndian(Span<byte> destination, int value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<int>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteInt64BigEndian(Span<byte> destination, long value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<long>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteUInt16BigEndian(Span<byte> destination, ushort value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<ushort>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteUInt32BigEndian(Span<byte> destination, uint value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<uint>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteUInt64BigEndian(Span<byte> destination, ulong value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<ulong>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteInt16BigEndian(Span<byte> destination, short value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<short>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteInt32BigEndian(Span<byte> destination, int value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<int>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteInt64BigEndian(Span<byte> destination, long value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<long>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteUInt16BigEndian(Span<byte> destination, ushort value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<ushort>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteUInt32BigEndian(Span<byte> destination, uint value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<uint>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteUInt64BigEndian(Span<byte> destination, ulong value)
    {
      if (BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<ulong>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteInt16LittleEndian(Span<byte> destination, short value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<short>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteInt32LittleEndian(Span<byte> destination, int value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<int>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteInt64LittleEndian(Span<byte> destination, long value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<long>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteUInt16LittleEndian(Span<byte> destination, ushort value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<ushort>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<uint>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static void WriteUInt64LittleEndian(Span<byte> destination, ulong value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      MemoryMarshal.Write<ulong>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteInt16LittleEndian(Span<byte> destination, short value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<short>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteInt32LittleEndian(Span<byte> destination, int value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<int>(destination, ref value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteInt64LittleEndian(Span<byte> destination, long value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<long>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteUInt16LittleEndian(Span<byte> destination, ushort value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<ushort>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteUInt32LittleEndian(Span<byte> destination, uint value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<uint>(destination, ref value);
    }

    [CLSCompliant(false)]
    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWriteUInt64LittleEndian(Span<byte> destination, ulong value)
    {
      if (!BitConverter.IsLittleEndian)
        value = BinaryPrimitives.ReverseEndianness(value);
      return MemoryMarshal.TryWrite<ulong>(destination, ref value);
    }
  }
}

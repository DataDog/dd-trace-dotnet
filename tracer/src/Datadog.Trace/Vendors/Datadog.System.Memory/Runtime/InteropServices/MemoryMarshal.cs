﻿// Decompiled with JetBrains decompiler
// Type: System.Runtime.InteropServices.MemoryMarshal
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.System.Buffers;
using Datadog.System.Runtime.CompilerServices.Unsafe;

namespace Datadog.System.Runtime.InteropServices
{
    public static class MemoryMarshal
  {
    public static bool TryGetArray<T>(ReadOnlyMemory<T> memory, out ArraySegment<T> segment)
    {
      int start;
      int length;
      object objectStartLength = memory.GetObjectStartLength(out start, out length);
      if (start < 0)
      {
        ArraySegment<T> segment1;
        if (((MemoryManager<T>) objectStartLength).TryGetArray(out segment1))
        {
          segment = new ArraySegment<T>(segment1.Array, segment1.Offset + (start & int.MaxValue), length);
          return true;
        }
      }
      else if (objectStartLength is T[] array)
      {
        segment = new ArraySegment<T>(array, start, length & int.MaxValue);
        return true;
      }
      if ((length & int.MaxValue) == 0)
      {
        segment = new ArraySegment<T>(SpanHelpers.PerTypeValues<T>.EmptyArray);
        return true;
      }
      segment = new ArraySegment<T>();
      return false;
    }

    public static bool TryGetMemoryManager<T, TManager>(
      ReadOnlyMemory<T> memory,
      out TManager manager)
      where TManager : MemoryManager<T>
    {
      TManager objectStartLength;
      manager = objectStartLength = memory.GetObjectStartLength(out int _, out int _) as TManager;
      return (object) manager != null;
    }

    public static bool TryGetMemoryManager<T, TManager>(
      ReadOnlyMemory<T> memory,
      out TManager manager,
      out int start,
      out int length)
      where TManager : MemoryManager<T>
    {
      TManager objectStartLength;
      manager = objectStartLength = memory.GetObjectStartLength(out start, out length) as TManager;
      start &= int.MaxValue;
      if ((object) manager != null)
        return true;
      start = 0;
      length = 0;
      return false;
    }

    public static IEnumerable<T> ToEnumerable<T>(ReadOnlyMemory<T> memory)
    {
      for (int i = 0; i < memory.Length; ++i)
        yield return memory.Span[i];
    }

    public static bool TryGetString(
      ReadOnlyMemory<char> memory,
      out string text,
      out int start,
      out int length)
    {
      int start1;
      int length1;
      if (memory.GetObjectStartLength(out start1, out length1) is string objectStartLength)
      {
        text = objectStartLength;
        start = start1;
        length = length1;
        return true;
      }
      text = (string) null;
      start = 0;
      length = 0;
      return false;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static T Read<T>(ReadOnlySpan<byte> source) where T : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      if (Unsafe.SizeOf<T>() > source.Length)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
      return Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference<byte>(source));
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryRead<T>(ReadOnlySpan<byte> source, out T value) where T : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      if ((long) Unsafe.SizeOf<T>() > (long) (uint) source.Length)
      {
        value = default (T);
        return false;
      }
      value = Unsafe.ReadUnaligned<T>(ref MemoryMarshal.GetReference<byte>(source));
      return true;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void Write<T>(Span<byte> destination, ref T value) where T : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      if ((uint) Unsafe.SizeOf<T>() > (uint) destination.Length)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
      Unsafe.WriteUnaligned<T>(ref MemoryMarshal.GetReference<byte>(destination), value);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool TryWrite<T>(Span<byte> destination, ref T value) where T : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      if ((long) Unsafe.SizeOf<T>() > (long) (uint) destination.Length)
        return false;
      Unsafe.WriteUnaligned<T>(ref MemoryMarshal.GetReference<byte>(destination), value);
      return true;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static Memory<T> CreateFromPinnedArray<T>(T[] array, int start, int length)
    {
      if (array == null)
      {
        if (start != 0 || length != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException();
        return new Memory<T>();
      }
      if ((object) default (T) == null && array.GetType() != typeof (T[]))
        ThrowHelper.ThrowArrayTypeMismatchException();
      if ((uint) start > (uint) array.Length || (uint) length > (uint) (array.Length - start))
        ThrowHelper.ThrowArgumentOutOfRangeException();
      return new Memory<T>((object) array, start, length | int.MinValue);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static Span<byte> AsBytes<T>(Span<T> span) where T : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      int length = checked (span.Length * Unsafe.SizeOf<T>());
      return new Span<byte>(Unsafe.As<Pinnable<byte>>((object) span.Pinnable), span.ByteOffset, length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static ReadOnlySpan<byte> AsBytes<T>(ReadOnlySpan<T> span) where T : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<T>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (T));
      int length = checked (span.Length * Unsafe.SizeOf<T>());
      return new ReadOnlySpan<byte>(Unsafe.As<Pinnable<byte>>((object) span.Pinnable), span.ByteOffset, length);
    }

    public static Memory<T> AsMemory<T>(ReadOnlyMemory<T> memory) => Unsafe.As<ReadOnlyMemory<T>, Memory<T>>(ref memory);

    public static unsafe ref T GetReference<T>(Span<T> span) => ref (span.Pinnable == null ? ref Unsafe.AsRef<T>(span.ByteOffset.ToPointer()) : ref Unsafe.AddByteOffset<T>(ref span.Pinnable.Data, span.ByteOffset));

    public static unsafe ref T GetReference<T>(ReadOnlySpan<T> span) => ref (span.Pinnable == null ? ref Unsafe.AsRef<T>(span.ByteOffset.ToPointer()) : ref Unsafe.AddByteOffset<T>(ref span.Pinnable.Data, span.ByteOffset));

    public static Span<TTo> Cast<TFrom, TTo>(Span<TFrom> span)
      where TFrom : struct
      where TTo : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<TFrom>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (TFrom));
      if (SpanHelpers.IsReferenceOrContainsReferences<TTo>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (TTo));
      int length = checked ((int) unchecked (checked ((long) span.Length * (long) Unsafe.SizeOf<TFrom>()) / (long) Unsafe.SizeOf<TTo>()));
      return new Span<TTo>(Unsafe.As<Pinnable<TTo>>((object) span.Pinnable), span.ByteOffset, length);
    }

    public static ReadOnlySpan<TTo> Cast<TFrom, TTo>(ReadOnlySpan<TFrom> span)
      where TFrom : struct
      where TTo : struct
    {
      if (SpanHelpers.IsReferenceOrContainsReferences<TFrom>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (TFrom));
      if (SpanHelpers.IsReferenceOrContainsReferences<TTo>())
        ThrowHelper.ThrowArgumentException_InvalidTypeWithPointersNotSupported(typeof (TTo));
      int length = checked ((int) unchecked (checked ((long) span.Length * (long) Unsafe.SizeOf<TFrom>()) / (long) Unsafe.SizeOf<TTo>()));
      return new ReadOnlySpan<TTo>(Unsafe.As<Pinnable<TTo>>((object) span.Pinnable), span.ByteOffset, length);
    }
  }
}

﻿// Decompiled with JetBrains decompiler
// Type: System.MemoryExtensions
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Datadog.System.Runtime.CompilerServices.Unsafe;
using Datadog.System.Runtime.InteropServices;

namespace Datadog.System
{
    public static class MemoryExtensions
  {
    internal static readonly IntPtr StringAdjustment = MemoryExtensions.MeasureStringAdjustment();

    public static ReadOnlySpan<char> Trim(this ReadOnlySpan<char> span) => span.TrimStart().TrimEnd();

    public static ReadOnlySpan<char> TrimStart(this ReadOnlySpan<char> span)
    {
      int num = 0;
      while (num < span.Length && char.IsWhiteSpace(span[num]))
        ++num;
      return span.Slice(num);
    }

    public static ReadOnlySpan<char> TrimEnd(this ReadOnlySpan<char> span)
    {
      int index = span.Length - 1;
      while (index >= 0 && char.IsWhiteSpace(span[index]))
        --index;
      return span.Slice(0, index + 1);
    }

    public static ReadOnlySpan<char> Trim(this ReadOnlySpan<char> span, char trimChar) => span.TrimStart(trimChar).TrimEnd(trimChar);

    public static ReadOnlySpan<char> TrimStart(this ReadOnlySpan<char> span, char trimChar)
    {
      int num = 0;
      while (num < span.Length && (int) span[num] == (int) trimChar)
        ++num;
      return span.Slice(num);
    }

    public static ReadOnlySpan<char> TrimEnd(this ReadOnlySpan<char> span, char trimChar)
    {
      int index = span.Length - 1;
      while (index >= 0 && (int) span[index] == (int) trimChar)
        --index;
      return span.Slice(0, index + 1);
    }

    public static ReadOnlySpan<char> Trim(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> trimChars)
    {
      return span.TrimStart(trimChars).TrimEnd(trimChars);
    }

    public static ReadOnlySpan<char> TrimStart(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> trimChars)
    {
      if (trimChars.IsEmpty)
        return span.TrimStart();
label_8:
      int num;
      for (num = 0; num < span.Length; ++num)
      {
        for (int index = 0; index < trimChars.Length; ++index)
        {
          if ((int) span[num] == (int) trimChars[index])
            goto label_8;
        }
        break;
      }
      return span.Slice(num);
    }

    public static ReadOnlySpan<char> TrimEnd(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> trimChars)
    {
      if (trimChars.IsEmpty)
        return span.TrimEnd();
label_8:
      int index1;
      for (index1 = span.Length - 1; index1 >= 0; --index1)
      {
        for (int index2 = 0; index2 < trimChars.Length; ++index2)
        {
          if ((int) span[index1] == (int) trimChars[index2])
            goto label_8;
        }
        break;
      }
      return span.Slice(0, index1 + 1);
    }

    public static bool IsWhiteSpace(this ReadOnlySpan<char> span)
    {
      for (int index = 0; index < span.Length; ++index)
      {
        if (!char.IsWhiteSpace(span[index]))
          return false;
      }
      return true;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOf<T>(this Span<T> span, T value) where T : IEquatable<T>
    {
      if (typeof (T) == typeof (byte))
        return SpanHelpers.IndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value), span.Length);
      return typeof (T) == typeof (char) ? SpanHelpers.IndexOf(ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, char>(ref value), span.Length) : SpanHelpers.IndexOf<T>(ref MemoryMarshal.GetReference<T>(span), value, span.Length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOf<T>(this Span<T> span, ReadOnlySpan<T> value) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), value.Length) : SpanHelpers.IndexOf<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(value), value.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOf<T>(this Span<T> span, T value) where T : IEquatable<T>
    {
      if (typeof (T) == typeof (byte))
        return SpanHelpers.LastIndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value), span.Length);
      return typeof (T) == typeof (char) ? SpanHelpers.LastIndexOf(ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, char>(ref value), span.Length) : SpanHelpers.LastIndexOf<T>(ref MemoryMarshal.GetReference<T>(span), value, span.Length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOf<T>(this Span<T> span, ReadOnlySpan<T> value) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), value.Length) : SpanHelpers.LastIndexOf<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(value), value.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static bool SequenceEqual<T>(this Span<T> span, ReadOnlySpan<T> other) where T : IEquatable<T>
    {
      int length = span.Length;
      NUInt size;
      return (object) default (T) != null && MemoryExtensions.IsTypeComparableAsBytes<T>(out size) ? length == other.Length && SpanHelpers.SequenceEqual(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(other)), (int)(object)((NUInt) length * size)) : length == other.Length && SpanHelpers.SequenceEqual<T>(ref MemoryMarshal.GetReference<T>(span), ref MemoryMarshal.GetReference<T>(other), length);
    }

    public static int SequenceCompareTo<T>(this Span<T> span, ReadOnlySpan<T> other) where T : IComparable<T>
    {
      if (typeof (T) == typeof (byte))
        return SpanHelpers.SequenceCompareTo(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(other)), other.Length);
      return typeof (T) == typeof (char) ? SpanHelpers.SequenceCompareTo(ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(other)), other.Length) : SpanHelpers.SequenceCompareTo<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(other), other.Length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOf<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
    {
      if (typeof (T) == typeof (byte))
        return SpanHelpers.IndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value), span.Length);
      return typeof (T) == typeof (char) ? SpanHelpers.IndexOf(ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, char>(ref value), span.Length) : SpanHelpers.IndexOf<T>(ref MemoryMarshal.GetReference<T>(span), value, span.Length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOf<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), value.Length) : SpanHelpers.IndexOf<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(value), value.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOf<T>(this ReadOnlySpan<T> span, T value) where T : IEquatable<T>
    {
      if (typeof (T) == typeof (byte))
        return SpanHelpers.LastIndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value), span.Length);
      return typeof (T) == typeof (char) ? SpanHelpers.LastIndexOf(ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, char>(ref value), span.Length) : SpanHelpers.LastIndexOf<T>(ref MemoryMarshal.GetReference<T>(span), value, span.Length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOf<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOf(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), value.Length) : SpanHelpers.LastIndexOf<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(value), value.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOfAny<T>(this Span<T> span, T value0, T value1) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), span.Length) : SpanHelpers.IndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOfAny<T>(this Span<T> span, T value0, T value1, T value2) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), Unsafe.As<T, byte>(ref value2), span.Length) : SpanHelpers.IndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, value2, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOfAny<T>(this Span<T> span, ReadOnlySpan<T> values) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(values)), values.Length) : SpanHelpers.IndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(values), values.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOfAny<T>(this ReadOnlySpan<T> span, T value0, T value1) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), span.Length) : SpanHelpers.IndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOfAny<T>(this ReadOnlySpan<T> span, T value0, T value1, T value2) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), Unsafe.As<T, byte>(ref value2), span.Length) : SpanHelpers.IndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, value2, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int IndexOfAny<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> values) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.IndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(values)), values.Length) : SpanHelpers.IndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(values), values.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOfAny<T>(this Span<T> span, T value0, T value1) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), span.Length) : SpanHelpers.LastIndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOfAny<T>(this Span<T> span, T value0, T value1, T value2) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), Unsafe.As<T, byte>(ref value2), span.Length) : SpanHelpers.LastIndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, value2, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOfAny<T>(this Span<T> span, ReadOnlySpan<T> values) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(values)), values.Length) : SpanHelpers.LastIndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(values), values.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOfAny<T>(this ReadOnlySpan<T> span, T value0, T value1) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), span.Length) : SpanHelpers.LastIndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOfAny<T>(this ReadOnlySpan<T> span, T value0, T value1, T value2) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), Unsafe.As<T, byte>(ref value0), Unsafe.As<T, byte>(ref value1), Unsafe.As<T, byte>(ref value2), span.Length) : SpanHelpers.LastIndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), value0, value1, value2, span.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static int LastIndexOfAny<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> values) where T : IEquatable<T> => typeof (T) == typeof (byte) ? SpanHelpers.LastIndexOfAny(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(values)), values.Length) : SpanHelpers.LastIndexOfAny<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(values), values.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static bool SequenceEqual<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> other) where T : IEquatable<T>
    {
      int length = span.Length;
      NUInt size;
      return (object) default (T) != null && MemoryExtensions.IsTypeComparableAsBytes<T>(out size) ? length == other.Length && SpanHelpers.SequenceEqual(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(other)), (int)(object)((NUInt) length * size)) : length == other.Length && SpanHelpers.SequenceEqual<T>(ref MemoryMarshal.GetReference<T>(span), ref MemoryMarshal.GetReference<T>(other), length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int SequenceCompareTo<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> other) where T : IComparable<T>
    {
      if (typeof (T) == typeof (byte))
        return SpanHelpers.SequenceCompareTo(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(other)), other.Length);
      return typeof (T) == typeof (char) ? SpanHelpers.SequenceCompareTo(ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(span)), span.Length, ref Unsafe.As<T, char>(ref MemoryMarshal.GetReference<T>(other)), other.Length) : SpanHelpers.SequenceCompareTo<T>(ref MemoryMarshal.GetReference<T>(span), span.Length, ref MemoryMarshal.GetReference<T>(other), other.Length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool StartsWith<T>(this Span<T> span, ReadOnlySpan<T> value) where T : IEquatable<T>
    {
      int length = value.Length;
      NUInt size;
      return (object) default (T) != null && MemoryExtensions.IsTypeComparableAsBytes<T>(out size) ? length <= span.Length && SpanHelpers.SequenceEqual(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), (int)(object)((NUInt) length * size)) : length <= span.Length && SpanHelpers.SequenceEqual<T>(ref MemoryMarshal.GetReference<T>(span), ref MemoryMarshal.GetReference<T>(value), length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool StartsWith<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value) where T : IEquatable<T>
    {
      int length = value.Length;
      NUInt size;
      return (object) default (T) != null && MemoryExtensions.IsTypeComparableAsBytes<T>(out size) ? length <= span.Length && SpanHelpers.SequenceEqual(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(span)), ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), (int)(object)((NUInt) length * size)) : length <= span.Length && SpanHelpers.SequenceEqual<T>(ref MemoryMarshal.GetReference<T>(span), ref MemoryMarshal.GetReference<T>(value), length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool EndsWith<T>(this Span<T> span, ReadOnlySpan<T> value) where T : IEquatable<T>
    {
      int length1 = span.Length;
      int length2 = value.Length;
      NUInt size;
      return (object) default (T) != null && MemoryExtensions.IsTypeComparableAsBytes<T>(out size) ? length2 <= length1 && SpanHelpers.SequenceEqual(ref Unsafe.As<T, byte>(ref Unsafe.Add<T>(ref MemoryMarshal.GetReference<T>(span), length1 - length2)), ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), (int)(object)((NUInt) length2 * size)) : length2 <= length1 && SpanHelpers.SequenceEqual<T>(ref Unsafe.Add<T>(ref MemoryMarshal.GetReference<T>(span), length1 - length2), ref MemoryMarshal.GetReference<T>(value), length2);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static bool EndsWith<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> value) where T : IEquatable<T>
    {
      int length1 = span.Length;
      int length2 = value.Length;
      NUInt size;
      return (object) default (T) != null && MemoryExtensions.IsTypeComparableAsBytes<T>(out size) ? length2 <= length1 && SpanHelpers.SequenceEqual(ref Unsafe.As<T, byte>(ref Unsafe.Add<T>(ref MemoryMarshal.GetReference<T>(span), length1 - length2)), ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference<T>(value)), (int)(object)((NUInt) length2 * size)) : length2 <= length1 && SpanHelpers.SequenceEqual<T>(ref Unsafe.Add<T>(ref MemoryMarshal.GetReference<T>(span), length1 - length2), ref MemoryMarshal.GetReference<T>(value), length2);
    }

    public static void Reverse<T>(this Span<T> span)
    {
      ref T local = ref MemoryMarshal.GetReference<T>(span);
      int elementOffset1 = 0;
      for (int elementOffset2 = span.Length - 1; elementOffset1 < elementOffset2; --elementOffset2)
      {
        T obj = Unsafe.Add<T>(ref local, elementOffset1);
        Unsafe.Add<T>(ref local, elementOffset1) = Unsafe.Add<T>(ref local, elementOffset2);
        Unsafe.Add<T>(ref local, elementOffset2) = obj;
        ++elementOffset1;
      }
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static Span<T> AsSpan<T>(this T[] array) => new Span<T>(array);

    [MethodImpl((MethodImplOptions) 256)]
    public static Span<T> AsSpan<T>(this T[] array, int start, int length) => new Span<T>(array, start, length);

    [MethodImpl((MethodImplOptions) 256)]
    public static Span<T> AsSpan<T>(this ArraySegment<T> segment) => new Span<T>(segment.Array, segment.Offset, segment.Count);

    [MethodImpl((MethodImplOptions) 256)]
    public static Span<T> AsSpan<T>(this ArraySegment<T> segment, int start)
    {
      if ((long) (uint) start > (long) segment.Count)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new Span<T>(segment.Array, segment.Offset + start, segment.Count - start);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static Span<T> AsSpan<T>(this ArraySegment<T> segment, int start, int length)
    {
      if ((long) (uint) start > (long) segment.Count)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      if ((long) (uint) length > (long) (segment.Count - start))
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
      return new Span<T>(segment.Array, segment.Offset + start, length);
    }

    public static Memory<T> AsMemory<T>(this T[] array) => new Memory<T>(array);

    public static Memory<T> AsMemory<T>(this T[] array, int start) => new Memory<T>(array, start);

    public static Memory<T> AsMemory<T>(this T[] array, int start, int length) => new Memory<T>(array, start, length);

    public static Memory<T> AsMemory<T>(this ArraySegment<T> segment) => new Memory<T>(segment.Array, segment.Offset, segment.Count);

    public static Memory<T> AsMemory<T>(this ArraySegment<T> segment, int start)
    {
      if ((long) (uint) start > (long) segment.Count)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new Memory<T>(segment.Array, segment.Offset + start, segment.Count - start);
    }

    public static Memory<T> AsMemory<T>(this ArraySegment<T> segment, int start, int length)
    {
      if ((long) (uint) start > (long) segment.Count)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      if ((long) (uint) length > (long) (segment.Count - start))
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
      return new Memory<T>(segment.Array, segment.Offset + start, length);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void CopyTo<T>(this T[] source, Span<T> destination) => new ReadOnlySpan<T>(source).CopyTo(destination);

    [MethodImpl((MethodImplOptions) 256)]
    public static void CopyTo<T>(this T[] source, Memory<T> destination) => source.CopyTo<T>(destination.Span);

    [MethodImpl((MethodImplOptions) 256)]
    public static bool Overlaps<T>(this Span<T> span, ReadOnlySpan<T> other) => ((ReadOnlySpan<T>) span).Overlaps<T>(other);

    [MethodImpl((MethodImplOptions) 256)]
    public static bool Overlaps<T>(this Span<T> span, ReadOnlySpan<T> other, out int elementOffset) => ((ReadOnlySpan<T>) span).Overlaps<T>(other, out elementOffset);

    public static bool Overlaps<T>(this ReadOnlySpan<T> span, ReadOnlySpan<T> other)
    {
      if (span.IsEmpty || other.IsEmpty)
        return false;
      IntPtr num = Unsafe.ByteOffset<T>(ref MemoryMarshal.GetReference<T>(span), ref MemoryMarshal.GetReference<T>(other));
      return Unsafe.SizeOf<IntPtr>() == 4 ? (uint) (int) num < (uint) (span.Length * Unsafe.SizeOf<T>()) || (uint) (int) num > (uint) -(other.Length * Unsafe.SizeOf<T>()) : (ulong) (long) num < (ulong) span.Length * (ulong) Unsafe.SizeOf<T>() || (ulong) (long) num > (ulong) -((long) other.Length * (long) Unsafe.SizeOf<T>());
    }

    public static bool Overlaps<T>(
      this ReadOnlySpan<T> span,
      ReadOnlySpan<T> other,
      out int elementOffset)
    {
      if (span.IsEmpty || other.IsEmpty)
      {
        elementOffset = 0;
        return false;
      }
      IntPtr num = Unsafe.ByteOffset<T>(ref MemoryMarshal.GetReference<T>(span), ref MemoryMarshal.GetReference<T>(other));
      if (Unsafe.SizeOf<IntPtr>() == 4)
      {
        if ((uint) (int) num < (uint) (span.Length * Unsafe.SizeOf<T>()) || (uint) (int) num > (uint) -(other.Length * Unsafe.SizeOf<T>()))
        {
          if ((int) num % Unsafe.SizeOf<T>() != 0)
            ThrowHelper.ThrowArgumentException_OverlapAlignmentMismatch();
          elementOffset = (int) num / Unsafe.SizeOf<T>();
          return true;
        }
        elementOffset = 0;
        return false;
      }
      if ((ulong) (long) num < (ulong) span.Length * (ulong) Unsafe.SizeOf<T>() || (ulong) (long) num > (ulong) -((long) other.Length * (long) Unsafe.SizeOf<T>()))
      {
        if ((long) num % (long) Unsafe.SizeOf<T>() != 0L)
          ThrowHelper.ThrowArgumentException_OverlapAlignmentMismatch();
        elementOffset = (int) ((long) num / (long) Unsafe.SizeOf<T>());
        return true;
      }
      elementOffset = 0;
      return false;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int BinarySearch<T>(this Span<T> span, IComparable<T> comparable) => span.BinarySearch<T, IComparable<T>>(comparable);

    [MethodImpl((MethodImplOptions) 256)]
    public static int BinarySearch<T, TComparable>(this Span<T> span, TComparable comparable) where TComparable : IComparable<T> => MemoryExtensions.BinarySearch<T, TComparable>((ReadOnlySpan<T>) span, comparable);

    [MethodImpl((MethodImplOptions) 256)]
    public static int BinarySearch<T, TComparer>(this Span<T> span, T value, TComparer comparer) where TComparer : IComparer<T> => ((ReadOnlySpan<T>) span).BinarySearch<T, TComparer>(value, comparer);

    [MethodImpl((MethodImplOptions) 256)]
    public static int BinarySearch<T>(this ReadOnlySpan<T> span, IComparable<T> comparable) => MemoryExtensions.BinarySearch<T, IComparable<T>>(span, comparable);

    [MethodImpl((MethodImplOptions) 256)]
    public static int BinarySearch<T, TComparable>(
      this ReadOnlySpan<T> span,
      TComparable comparable)
      where TComparable : IComparable<T>
    {
      return SpanHelpers.BinarySearch<T, TComparable>(span, comparable);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static int BinarySearch<T, TComparer>(
      this ReadOnlySpan<T> span,
      T value,
      TComparer comparer)
      where TComparer : IComparer<T>
    {
      if ((object) comparer == null)
        ThrowHelper.ThrowArgumentNullException(ExceptionArgument.comparer);
      SpanHelpers.ComparerComparable<T, TComparer> comparable = new SpanHelpers.ComparerComparable<T, TComparer>(value, comparer);
      return MemoryExtensions.BinarySearch<T, SpanHelpers.ComparerComparable<T, TComparer>>(span, comparable);
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static bool IsTypeComparableAsBytes<T>(out NUInt size)
    {
      if (typeof (T) == typeof (byte) || typeof (T) == typeof (sbyte))
      {
        size = (NUInt) 1;
        return true;
      }
      if (typeof (T) == typeof (char) || typeof (T) == typeof (short) || typeof (T) == typeof (ushort))
      {
        size = (NUInt) 2;
        return true;
      }
      if (typeof (T) == typeof (int) || typeof (T) == typeof (uint))
      {
        size = (NUInt) 4;
        return true;
      }
      if (typeof (T) == typeof (long) || typeof (T) == typeof (ulong))
      {
        size = (NUInt) 8;
        return true;
      }
      size = new NUInt();
      return false;
    }

    public static Span<T> AsSpan<T>(this T[] array, int start) => Span<T>.Create(array, start);

    public static bool Contains(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> value,
      StringComparison comparisonType)
    {
      return span.IndexOf(value, comparisonType) >= 0;
    }

    public static bool Equals(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> other,
      StringComparison comparisonType)
    {
      switch (comparisonType)
      {
        case StringComparison.Ordinal:
          return span.SequenceEqual<char>(other);
        case StringComparison.OrdinalIgnoreCase:
          return span.Length == other.Length && MemoryExtensions.EqualsOrdinalIgnoreCase(span, other);
        default:
          return span.ToString().Equals(other.ToString(), comparisonType);
      }
    }

    [MethodImpl((MethodImplOptions) 256)]
    private static bool EqualsOrdinalIgnoreCase(ReadOnlySpan<char> span, ReadOnlySpan<char> other) => other.Length == 0 || MemoryExtensions.CompareToOrdinalIgnoreCase(span, other) == 0;

    public static int CompareTo(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> other,
      StringComparison comparisonType)
    {
      if (comparisonType == StringComparison.Ordinal)
        return span.SequenceCompareTo<char>(other);
      return comparisonType == StringComparison.OrdinalIgnoreCase ? MemoryExtensions.CompareToOrdinalIgnoreCase(span, other) : string.Compare(span.ToString(), other.ToString(), comparisonType);
    }

    private static unsafe int CompareToOrdinalIgnoreCase(
      ReadOnlySpan<char> strA,
      ReadOnlySpan<char> strB)
    {
      int num1 = Math.Min(strA.Length, strB.Length);
      int num2 = num1;
      fixed (char* chPtr1 = &MemoryMarshal.GetReference<char>(strA))
        fixed (char* chPtr2 = &MemoryMarshal.GetReference<char>(strB))
        {
          char* chPtr3 = chPtr1;
          char* chPtr4 = chPtr2;
          while (num1 != 0 && *chPtr3 <= '\u007F' && *chPtr4 <= '\u007F')
          {
            int num3 = (int) *chPtr3;
            int num4 = (int) *chPtr4;
            if (num3 == num4)
            {
              ++chPtr3;
              ++chPtr4;
              --num1;
            }
            else
            {
              if ((uint) (num3 - 97) <= 25U)
                num3 -= 32;
              if ((uint) (num4 - 97) <= 25U)
                num4 -= 32;
              if (num3 != num4)
                return num3 - num4;
              ++chPtr3;
              ++chPtr4;
              --num1;
            }
          }
          if (num1 == 0)
            return strA.Length - strB.Length;
          int start = num2 - num1;
          return string.Compare(strA.Slice(start).ToString(), strB.Slice(start).ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public static int IndexOf(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> value,
      StringComparison comparisonType)
    {
      return comparisonType == StringComparison.Ordinal ? span.IndexOf<char>(value) : span.ToString().IndexOf(value.ToString(), comparisonType);
    }

    public static int ToLower(
      this ReadOnlySpan<char> source,
      Span<char> destination,
      CultureInfo culture)
    {
      if (culture == null)
        ThrowHelper.ThrowArgumentNullException(ExceptionArgument.culture);
      if (destination.Length < source.Length)
        return -1;
      source.ToString().ToLower(culture).AsSpan().CopyTo(destination);
      return source.Length;
    }

    public static int ToLowerInvariant(this ReadOnlySpan<char> source, Span<char> destination) => source.ToLower(destination, CultureInfo.InvariantCulture);

    public static int ToUpper(
      this ReadOnlySpan<char> source,
      Span<char> destination,
      CultureInfo culture)
    {
      if (culture == null)
        ThrowHelper.ThrowArgumentNullException(ExceptionArgument.culture);
      if (destination.Length < source.Length)
        return -1;
      source.ToString().ToUpper(culture).AsSpan().CopyTo(destination);
      return source.Length;
    }

    public static int ToUpperInvariant(this ReadOnlySpan<char> source, Span<char> destination) => source.ToUpper(destination, CultureInfo.InvariantCulture);

    public static bool EndsWith(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> value,
      StringComparison comparisonType)
    {
      switch (comparisonType)
      {
        case StringComparison.Ordinal:
          return span.EndsWith<char>(value);
        case StringComparison.OrdinalIgnoreCase:
          return value.Length <= span.Length && MemoryExtensions.EqualsOrdinalIgnoreCase(span.Slice(span.Length - value.Length), value);
        default:
          return span.ToString().EndsWith(value.ToString(), comparisonType);
      }
    }

    public static bool StartsWith(
      this ReadOnlySpan<char> span,
      ReadOnlySpan<char> value,
      StringComparison comparisonType)
    {
      switch (comparisonType)
      {
        case StringComparison.Ordinal:
          return span.StartsWith<char>(value);
        case StringComparison.OrdinalIgnoreCase:
          return value.Length <= span.Length && MemoryExtensions.EqualsOrdinalIgnoreCase(span.Slice(0, value.Length), value);
        default:
          return span.ToString().StartsWith(value.ToString(), comparisonType);
      }
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static ReadOnlySpan<char> AsSpan(this string text) => text == null ? new ReadOnlySpan<char>() : new ReadOnlySpan<char>(Unsafe.As<Pinnable<char>>((object) text), MemoryExtensions.StringAdjustment, text.Length);

    [MethodImpl((MethodImplOptions) 256)]
    public static ReadOnlySpan<char> AsSpan(this string text, int start)
    {
      if (text == null)
      {
        if (start != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
        return new ReadOnlySpan<char>();
      }
      if ((uint) start > (uint) text.Length)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlySpan<char>(Unsafe.As<Pinnable<char>>((object) text), MemoryExtensions.StringAdjustment + start * 2, text.Length - start);
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static ReadOnlySpan<char> AsSpan(this string text, int start, int length)
    {
      if (text == null)
      {
        if (start != 0 || length != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
        return new ReadOnlySpan<char>();
      }
      if ((uint) start > (uint) text.Length || (uint) length > (uint) (text.Length - start))
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlySpan<char>(Unsafe.As<Pinnable<char>>((object) text), MemoryExtensions.StringAdjustment + start * 2, length);
    }

    public static ReadOnlyMemory<char> AsMemory(this string text) => text == null ? new ReadOnlyMemory<char>() : new ReadOnlyMemory<char>((object) text, 0, text.Length);

    public static ReadOnlyMemory<char> AsMemory(this string text, int start)
    {
      if (text == null)
      {
        if (start != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
        return new ReadOnlyMemory<char>();
      }
      if ((uint) start > (uint) text.Length)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlyMemory<char>((object) text, start, text.Length - start);
    }

    public static ReadOnlyMemory<char> AsMemory(this string text, int start, int length)
    {
      if (text == null)
      {
        if (start != 0 || length != 0)
          ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
        return new ReadOnlyMemory<char>();
      }
      if ((uint) start > (uint) text.Length || (uint) length > (uint) (text.Length - start))
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
      return new ReadOnlyMemory<char>((object) text, start, length);
    }

    private static unsafe IntPtr MeasureStringAdjustment()
    {
      string o = "a";
      fixed (char* source = o)
        return Unsafe.ByteOffset<char>(ref Unsafe.As<Pinnable<char>>((object) o).Data, ref Unsafe.AsRef<char>((void*) source));
    }
  }
}

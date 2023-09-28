// Decompiled with JetBrains decompiler
// Type: System.Buffers.BuffersExtensions
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Runtime.CompilerServices;

namespace Datadog.System.Buffers
{
    public static class BuffersExtensions
  {
    [MethodImpl((MethodImplOptions) 256)]
    public static SequencePosition? PositionOf<T>(in this ReadOnlySequence<T> source, T value) where T : IEquatable<T>
    {
      if (!source.IsSingleSegment)
        return BuffersExtensions.PositionOfMultiSegment<T>(in source, value);
      int offset = source.First.Span.IndexOf<T>(value);
      return offset != -1 ? new SequencePosition?(source.GetPosition((long) offset)) : new SequencePosition?();
    }

    private static SequencePosition? PositionOfMultiSegment<T>(
      in ReadOnlySequence<T> source,
      T value)
      where T : IEquatable<T>
    {
      SequencePosition start = source.Start;
      SequencePosition origin = start;
      ReadOnlyMemory<T> memory;
      while (source.TryGet(ref start, out memory))
      {
        int offset = memory.Span.IndexOf<T>(value);
        if (offset != -1)
          return new SequencePosition?(source.GetPosition((long) offset, origin));
        if (start.GetObject() != null)
          origin = start;
        else
          break;
      }
      return new SequencePosition?();
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void CopyTo<T>(in this ReadOnlySequence<T> source, Span<T> destination)
    {
      if (source.Length > (long) destination.Length)
        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.destination);
      if (source.IsSingleSegment)
        source.First.Span.CopyTo(destination);
      else
        BuffersExtensions.CopyToMultiSegment<T>(in source, destination);
    }

    private static void CopyToMultiSegment<T>(in ReadOnlySequence<T> sequence, Span<T> destination)
    {
      SequencePosition start = sequence.Start;
      ReadOnlyMemory<T> memory;
      while (sequence.TryGet(ref start, out memory))
      {
        ReadOnlySpan<T> span = memory.Span;
        span.CopyTo(destination);
        if (start.GetObject() == null)
          break;
        destination = destination.Slice(span.Length);
      }
    }

    public static T[] ToArray<T>(in this ReadOnlySequence<T> sequence)
    {
      T[] destination = new T[sequence.Length];
      sequence.CopyTo<T>((Span<T>) destination);
      return destination;
    }

    [MethodImpl((MethodImplOptions) 256)]
    public static void Write<T>(this IBufferWriter<T> writer, ReadOnlySpan<T> value)
    {
      Span<T> span = writer.GetSpan();
      if (value.Length <= span.Length)
      {
        value.CopyTo(span);
        writer.Advance(value.Length);
      }
      else
        BuffersExtensions.WriteMultiSegment<T>(writer, in value, span);
    }

    private static void WriteMultiSegment<T>(
      IBufferWriter<T> writer,
      in ReadOnlySpan<T> source,
      Span<T> destination)
    {
      ReadOnlySpan<T> readOnlySpan = source;
      while (true)
      {
        int num = Math.Min(destination.Length, readOnlySpan.Length);
        readOnlySpan.Slice(0, num).CopyTo(destination);
        writer.Advance(num);
        readOnlySpan = readOnlySpan.Slice(num);
        if (readOnlySpan.Length > 0)
          destination = writer.GetSpan(readOnlySpan.Length);
        else
          break;
      }
    }
  }
}

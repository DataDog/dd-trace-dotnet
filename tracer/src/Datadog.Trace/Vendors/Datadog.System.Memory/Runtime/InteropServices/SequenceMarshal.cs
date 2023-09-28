// Decompiled with JetBrains decompiler
// Type: System.Runtime.InteropServices.SequenceMarshal
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using Datadog.System.Buffers;

namespace Datadog.System.Runtime.InteropServices
{
  public static class SequenceMarshal
  {
    public static bool TryGetReadOnlySequenceSegment<T>(
      ReadOnlySequence<T> sequence,
      out ReadOnlySequenceSegment<T> startSegment,
      out int startIndex,
      out ReadOnlySequenceSegment<T> endSegment,
      out int endIndex)
    {
      return sequence.TryGetReadOnlySequenceSegment(out startSegment, out startIndex, out endSegment, out endIndex);
    }

    public static bool TryGetArray<T>(ReadOnlySequence<T> sequence, out ArraySegment<T> segment) => sequence.TryGetArray(out segment);

    public static bool TryGetReadOnlyMemory<T>(
      ReadOnlySequence<T> sequence,
      out ReadOnlyMemory<T> memory)
    {
      if (!sequence.IsSingleSegment)
      {
        memory = new ReadOnlyMemory<T>();
        return false;
      }
      memory = sequence.First;
      return true;
    }

    internal static bool TryGetString(
      ReadOnlySequence<char> sequence,
      out string text,
      out int start,
      out int length)
    {
      return sequence.TryGetString(out text, out start, out length);
    }
  }
}

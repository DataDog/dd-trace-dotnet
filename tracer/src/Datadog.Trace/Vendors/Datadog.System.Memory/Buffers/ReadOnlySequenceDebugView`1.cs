// Decompiled with JetBrains decompiler
// Type: System.Buffers.ReadOnlySequenceDebugView`1
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System.Diagnostics;

namespace Datadog.System.Buffers
{
    internal sealed class ReadOnlySequenceDebugView<T>
  {
    private readonly T[] _array;
    private readonly ReadOnlySequenceDebugView<T>.ReadOnlySequenceDebugViewSegments _segments;

    public ReadOnlySequenceDebugView(ReadOnlySequence<T> sequence)
    {
      this._array = sequence.ToArray<T>();
      int length = 0;
      foreach (ReadOnlyMemory<T> readOnlyMemory in sequence)
        ++length;
      ReadOnlyMemory<T>[] readOnlyMemoryArray = new ReadOnlyMemory<T>[length];
      int index = 0;
      foreach (ReadOnlyMemory<T> readOnlyMemory in sequence)
      {
        readOnlyMemoryArray[index] = readOnlyMemory;
        ++index;
      }
      this._segments = new ReadOnlySequenceDebugView<T>.ReadOnlySequenceDebugViewSegments()
      {
        Segments = readOnlyMemoryArray
      };
    }

    public ReadOnlySequenceDebugView<T>.ReadOnlySequenceDebugViewSegments BufferSegments => this._segments;

    [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
    public T[] Items => this._array;

    [DebuggerDisplay("Count: {Segments.Length}", Name = "Segments")]
    public struct ReadOnlySequenceDebugViewSegments
    {
      [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
      public ReadOnlyMemory<T>[] Segments { get; set; }
    }
  }
}

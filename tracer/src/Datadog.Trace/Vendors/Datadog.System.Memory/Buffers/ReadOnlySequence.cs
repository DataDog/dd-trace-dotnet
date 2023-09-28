// Decompiled with JetBrains decompiler
// Type: System.Buffers.ReadOnlySequence
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System.Runtime.CompilerServices;

namespace Datadog.System.Buffers
{
    internal static class ReadOnlySequence
  {
    public const int FlagBitMask = -2147483648;
    public const int IndexBitMask = 2147483647;
    public const int SegmentStartMask = 0;
    public const int SegmentEndMask = 0;
    public const int ArrayStartMask = 0;
    public const int ArrayEndMask = -2147483648;
    public const int MemoryManagerStartMask = -2147483648;
    public const int MemoryManagerEndMask = 0;
    public const int StringStartMask = -2147483648;
    public const int StringEndMask = -2147483648;

    [MethodImpl((MethodImplOptions) 256)]
    public static int SegmentToSequenceStart(int startIndex) => startIndex | 0;

    [MethodImpl((MethodImplOptions) 256)]
    public static int SegmentToSequenceEnd(int endIndex) => endIndex | 0;

    [MethodImpl((MethodImplOptions) 256)]
    public static int ArrayToSequenceStart(int startIndex) => startIndex | 0;

    [MethodImpl((MethodImplOptions) 256)]
    public static int ArrayToSequenceEnd(int endIndex) => endIndex | int.MinValue;

    [MethodImpl((MethodImplOptions) 256)]
    public static int MemoryManagerToSequenceStart(int startIndex) => startIndex | int.MinValue;

    [MethodImpl((MethodImplOptions) 256)]
    public static int MemoryManagerToSequenceEnd(int endIndex) => endIndex | 0;

    [MethodImpl((MethodImplOptions) 256)]
    public static int StringToSequenceStart(int startIndex) => startIndex | int.MinValue;

    [MethodImpl((MethodImplOptions) 256)]
    public static int StringToSequenceEnd(int endIndex) => endIndex | int.MinValue;
  }
}

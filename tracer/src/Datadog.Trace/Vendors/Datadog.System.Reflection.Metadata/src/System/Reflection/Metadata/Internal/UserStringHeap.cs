// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.UserStringHeap
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct UserStringHeap
  {
    internal readonly MemoryBlock Block;

    public UserStringHeap(MemoryBlock block) => this.Block = block;

    internal string GetString(UserStringHandle handle)
    {
      int offset;
      int size;
      return !this.Block.PeekHeapValueOffsetAndSize(handle.GetHeapOffset(), out offset, out size) ? string.Empty : this.Block.PeekUtf16(offset, size & -2);
    }

    internal UserStringHandle GetNextHandle(UserStringHandle handle)
    {
      int offset;
      int size;
      if (!this.Block.PeekHeapValueOffsetAndSize(handle.GetHeapOffset(), out offset, out size))
        return new UserStringHandle();
      int heapOffset = offset + size;
      return heapOffset >= this.Block.Length ? new UserStringHandle() : UserStringHandle.FromOffset(heapOffset);
    }
  }
}

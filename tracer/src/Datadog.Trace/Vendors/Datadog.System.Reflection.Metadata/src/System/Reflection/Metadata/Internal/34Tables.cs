// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.AssemblyOSTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct AssemblyOSTableReader
  {
    internal readonly int NumberOfRows;
    private readonly int _OSPlatformIdOffset;
    private readonly int _OSMajorVersionIdOffset;
    private readonly int _OSMinorVersionIdOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal AssemblyOSTableReader(
      int numberOfRows,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._OSPlatformIdOffset = 0;
      this._OSMajorVersionIdOffset = this._OSPlatformIdOffset + 4;
      this._OSMinorVersionIdOffset = this._OSMajorVersionIdOffset + 4;
      this.RowSize = this._OSMinorVersionIdOffset + 4;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }
  }
}

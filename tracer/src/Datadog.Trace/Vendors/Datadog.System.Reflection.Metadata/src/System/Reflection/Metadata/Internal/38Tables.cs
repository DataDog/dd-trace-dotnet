// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.FileTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct FileTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _NameOffset;
    private readonly int _HashValueOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal FileTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._NameOffset = this._FlagsOffset + 4;
      this._HashValueOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._HashValueOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal BlobHandle GetHashValue(AssemblyFileHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._HashValueOffset, this._IsBlobHeapRefSizeSmall));

    internal uint GetFlags(AssemblyFileHandle handle) => this.Block.PeekUInt32((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal StringHandle GetName(AssemblyFileHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));
  }
}

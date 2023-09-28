// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodSpecTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct MethodSpecTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsMethodDefOrRefRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _MethodOffset;
    private readonly int _InstantiationOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal MethodSpecTableReader(
      int numberOfRows,
      int methodDefOrRefRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsMethodDefOrRefRefSizeSmall = methodDefOrRefRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._MethodOffset = 0;
      this._InstantiationOffset = this._MethodOffset + methodDefOrRefRefSize;
      this.RowSize = this._InstantiationOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal EntityHandle GetMethod(MethodSpecificationHandle handle) => MethodDefOrRefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._MethodOffset, this._IsMethodDefOrRefRefSizeSmall));

    internal BlobHandle GetInstantiation(MethodSpecificationHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._InstantiationOffset, this._IsBlobHeapRefSizeSmall));
  }
}

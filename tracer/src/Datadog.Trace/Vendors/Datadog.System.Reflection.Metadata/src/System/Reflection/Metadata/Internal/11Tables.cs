// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ConstantTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ConstantTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsHasConstantRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _TypeOffset;
    private readonly int _ParentOffset;
    private readonly int _ValueOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ConstantTableReader(
      int numberOfRows,
      bool declaredSorted,
      int hasConstantRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsHasConstantRefSizeSmall = hasConstantRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._TypeOffset = 0;
      this._ParentOffset = this._TypeOffset + 1 + 1;
      this._ValueOffset = this._ParentOffset + hasConstantRefSize;
      this.RowSize = this._ValueOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.Constant);
    }

    internal ConstantTypeCode GetType(ConstantHandle handle) => (ConstantTypeCode) this.Block.PeekByte((handle.RowId - 1) * this.RowSize + this._TypeOffset);

    internal BlobHandle GetValue(ConstantHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._ValueOffset, this._IsBlobHeapRefSizeSmall));

    internal EntityHandle GetParent(ConstantHandle handle) => HasConstantTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ParentOffset, this._IsHasConstantRefSizeSmall));

    internal ConstantHandle FindConstant(EntityHandle parentHandle) => ConstantHandle.FromRowId(this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._ParentOffset, HasConstantTag.ConvertToTag(parentHandle), this._IsHasConstantRefSizeSmall) + 1);

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._ParentOffset, this._IsHasConstantRefSizeSmall);
  }
}

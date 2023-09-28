// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.FieldMarshalTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct FieldMarshalTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsHasFieldMarshalRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _ParentOffset;
    private readonly int _NativeTypeOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal FieldMarshalTableReader(
      int numberOfRows,
      bool declaredSorted,
      int hasFieldMarshalRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsHasFieldMarshalRefSizeSmall = hasFieldMarshalRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._ParentOffset = 0;
      this._NativeTypeOffset = this._ParentOffset + hasFieldMarshalRefSize;
      this.RowSize = this._NativeTypeOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.FieldMarshal);
    }

    internal EntityHandle GetParent(int rowId) => HasFieldMarshalTag.ConvertToHandle(this.Block.PeekTaggedReference((rowId - 1) * this.RowSize + this._ParentOffset, this._IsHasFieldMarshalRefSizeSmall));

    internal BlobHandle GetNativeType(int rowId) => BlobHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._NativeTypeOffset, this._IsBlobHeapRefSizeSmall));

    internal int FindFieldMarshalRowId(EntityHandle handle) => this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._ParentOffset, HasFieldMarshalTag.ConvertToTag(handle), this._IsHasFieldMarshalRefSizeSmall) + 1;

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._ParentOffset, this._IsHasFieldMarshalRefSizeSmall);
  }
}

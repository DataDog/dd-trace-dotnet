// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CustomAttributeTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct CustomAttributeTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsHasCustomAttributeRefSizeSmall;
    private readonly bool _IsCustomAttributeTypeRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _ParentOffset;
    private readonly int _TypeOffset;
    private readonly int _ValueOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;
    internal readonly int[]? PtrTable;

    internal CustomAttributeTableReader(
      int numberOfRows,
      bool declaredSorted,
      int hasCustomAttributeRefSize,
      int customAttributeTypeRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsHasCustomAttributeRefSizeSmall = hasCustomAttributeRefSize == 2;
      this._IsCustomAttributeTypeRefSizeSmall = customAttributeTypeRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._ParentOffset = 0;
      this._TypeOffset = this._ParentOffset + hasCustomAttributeRefSize;
      this._ValueOffset = this._TypeOffset + customAttributeTypeRefSize;
      this.RowSize = this._ValueOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      this.PtrTable = (int[]) null;
      if (declaredSorted || this.CheckSorted())
        return;
      this.PtrTable = this.Block.BuildPtrTable(numberOfRows, this.RowSize, this._ParentOffset, this._IsHasCustomAttributeRefSizeSmall);
    }

    internal EntityHandle GetParent(CustomAttributeHandle handle) => HasCustomAttributeTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ParentOffset, this._IsHasCustomAttributeRefSizeSmall));

    internal EntityHandle GetConstructor(CustomAttributeHandle handle) => CustomAttributeTypeTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._TypeOffset, this._IsCustomAttributeTypeRefSizeSmall));

    internal BlobHandle GetValue(CustomAttributeHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._ValueOffset, this._IsBlobHeapRefSizeSmall));

    internal void GetAttributeRange(
      EntityHandle parentHandle,
      out int firstImplRowId,
      out int lastImplRowId)
    {
      int startRowNumber;
      int endRowNumber;
      if (this.PtrTable != null)
        this.Block.BinarySearchReferenceRange(this.PtrTable, this.RowSize, this._ParentOffset, HasCustomAttributeTag.ConvertToTag(parentHandle), this._IsHasCustomAttributeRefSizeSmall, out startRowNumber, out endRowNumber);
      else
        this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, this._ParentOffset, HasCustomAttributeTag.ConvertToTag(parentHandle), this._IsHasCustomAttributeRefSizeSmall, out startRowNumber, out endRowNumber);
      if (startRowNumber == -1)
      {
        firstImplRowId = 1;
        lastImplRowId = 0;
      }
      else
      {
        firstImplRowId = startRowNumber + 1;
        lastImplRowId = endRowNumber + 1;
      }
    }

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._ParentOffset, this._IsHasCustomAttributeRefSizeSmall);
  }
}

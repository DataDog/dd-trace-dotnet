// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.DeclSecurityTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct DeclSecurityTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsHasDeclSecurityRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _ActionOffset;
    private readonly int _ParentOffset;
    private readonly int _PermissionSetOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal DeclSecurityTableReader(
      int numberOfRows,
      bool declaredSorted,
      int hasDeclSecurityRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsHasDeclSecurityRefSizeSmall = hasDeclSecurityRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._ActionOffset = 0;
      this._ParentOffset = this._ActionOffset + 2;
      this._PermissionSetOffset = this._ParentOffset + hasDeclSecurityRefSize;
      this.RowSize = this._PermissionSetOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.DeclSecurity);
    }

    internal DeclarativeSecurityAction GetAction(int rowId) => (DeclarativeSecurityAction) this.Block.PeekUInt16((rowId - 1) * this.RowSize + this._ActionOffset);

    internal EntityHandle GetParent(int rowId) => HasDeclSecurityTag.ConvertToHandle(this.Block.PeekTaggedReference((rowId - 1) * this.RowSize + this._ParentOffset, this._IsHasDeclSecurityRefSizeSmall));

    internal BlobHandle GetPermissionSet(int rowId) => BlobHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._PermissionSetOffset, this._IsBlobHeapRefSizeSmall));

    internal void GetAttributeRange(
      EntityHandle parentToken,
      out int firstImplRowId,
      out int lastImplRowId)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, this._ParentOffset, HasDeclSecurityTag.ConvertToTag(parentToken), this._IsHasDeclSecurityRefSizeSmall, out startRowNumber, out endRowNumber);
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

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._ParentOffset, this._IsHasDeclSecurityRefSizeSmall);
  }
}

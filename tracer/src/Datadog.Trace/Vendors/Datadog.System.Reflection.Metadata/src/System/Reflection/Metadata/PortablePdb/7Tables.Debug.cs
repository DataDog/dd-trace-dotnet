// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CustomDebugInformationTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct CustomDebugInformationTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _isHasCustomDebugInformationRefSizeSmall;
    private readonly bool _isGuidHeapRefSizeSmall;
    private readonly bool _isBlobHeapRefSizeSmall;
    private const int ParentOffset = 0;
    private readonly int _kindOffset;
    private readonly int _valueOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal CustomDebugInformationTableReader(
      int numberOfRows,
      bool declaredSorted,
      int hasCustomDebugInformationRefSize,
      int guidHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._isHasCustomDebugInformationRefSizeSmall = hasCustomDebugInformationRefSize == 2;
      this._isGuidHeapRefSizeSmall = guidHeapRefSize == 2;
      this._isBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._kindOffset = hasCustomDebugInformationRefSize;
      this._valueOffset = this._kindOffset + guidHeapRefSize;
      this.RowSize = this._valueOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (numberOfRows <= 0 || declaredSorted)
        return;
      Throw.TableNotSorted(TableIndex.CustomDebugInformation);
    }

    internal EntityHandle GetParent(CustomDebugInformationHandle handle) => HasCustomDebugInformationTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize, this._isHasCustomDebugInformationRefSizeSmall));

    internal GuidHandle GetKind(CustomDebugInformationHandle handle) => GuidHandle.FromIndex(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._kindOffset, this._isGuidHeapRefSizeSmall));

    internal BlobHandle GetValue(CustomDebugInformationHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._valueOffset, this._isBlobHeapRefSizeSmall));

    internal void GetRange(
      EntityHandle parentHandle,
      out int firstImplRowId,
      out int lastImplRowId)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, 0, HasCustomDebugInformationTag.ConvertToTag(parentHandle), this._isHasCustomDebugInformationRefSizeSmall, out startRowNumber, out endRowNumber);
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
  }
}

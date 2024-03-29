//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
#nullable enable
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ConstantTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal;

namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335
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

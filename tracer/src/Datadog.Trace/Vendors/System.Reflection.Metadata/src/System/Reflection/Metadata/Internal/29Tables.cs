//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
#nullable enable
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.FieldRVATableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal;

namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335
{
  internal readonly struct FieldRVATableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsFieldTableRowRefSizeSmall;
    private readonly int _RvaOffset;
    private readonly int _FieldOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal FieldRVATableReader(
      int numberOfRows,
      bool declaredSorted,
      int fieldTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsFieldTableRowRefSizeSmall = fieldTableRowRefSize == 2;
      this._RvaOffset = 0;
      this._FieldOffset = this._RvaOffset + 4;
      this.RowSize = this._FieldOffset + fieldTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.FieldRva);
    }

    internal int GetRva(int rowId) => this.Block.PeekInt32((rowId - 1) * this.RowSize + this._RvaOffset);

    internal int FindFieldRvaRowId(int fieldDefRowId) => this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._FieldOffset, (uint) fieldDefRowId, this._IsFieldTableRowRefSizeSmall) + 1;

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._FieldOffset, this._IsFieldTableRowRefSizeSmall);
  }
}

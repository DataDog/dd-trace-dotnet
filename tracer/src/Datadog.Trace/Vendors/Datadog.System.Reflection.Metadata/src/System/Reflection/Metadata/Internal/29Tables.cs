// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.FieldRVATableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
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

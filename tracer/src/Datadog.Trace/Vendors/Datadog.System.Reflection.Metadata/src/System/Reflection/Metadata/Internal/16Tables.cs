// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.FieldLayoutTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct FieldLayoutTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsFieldTableRowRefSizeSmall;
    private readonly int _OffsetOffset;
    private readonly int _FieldOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal FieldLayoutTableReader(
      int numberOfRows,
      bool declaredSorted,
      int fieldTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsFieldTableRowRefSizeSmall = fieldTableRowRefSize == 2;
      this._OffsetOffset = 0;
      this._FieldOffset = this._OffsetOffset + 4;
      this.RowSize = this._FieldOffset + fieldTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.FieldLayout);
    }

    /// <summary>
    /// Returns field offset for given field RowId, or -1 if not available.
    /// </summary>
    internal int FindFieldLayoutRowId(FieldDefinitionHandle handle) => this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._FieldOffset, (uint) handle.RowId, this._IsFieldTableRowRefSizeSmall) + 1;

    internal uint GetOffset(int rowId) => this.Block.PeekUInt32((rowId - 1) * this.RowSize + this._OffsetOffset);

    internal FieldDefinitionHandle GetField(int rowId) => FieldDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._FieldOffset, this._IsFieldTableRowRefSizeSmall));

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._FieldOffset, this._IsFieldTableRowRefSizeSmall);
  }
}

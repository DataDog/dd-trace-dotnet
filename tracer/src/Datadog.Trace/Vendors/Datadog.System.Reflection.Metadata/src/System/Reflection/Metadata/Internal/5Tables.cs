// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodPtrTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct MethodPtrTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsMethodTableRowRefSizeSmall;
    private readonly int _MethodOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal MethodPtrTableReader(
      int numberOfRows,
      int methodTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsMethodTableRowRefSizeSmall = methodTableRowRefSize == 2;
      this._MethodOffset = 0;
      this.RowSize = this._MethodOffset + methodTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal MethodDefinitionHandle GetMethodFor(int rowId) => MethodDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._MethodOffset, this._IsMethodTableRowRefSizeSmall));

    internal int GetRowIdForMethodDefRow(int methodDefRowId) => this.Block.LinearSearchReference(this.RowSize, this._MethodOffset, (uint) methodDefRowId, this._IsMethodTableRowRefSizeSmall) + 1;
  }
}

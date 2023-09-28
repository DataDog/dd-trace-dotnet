// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.EventPtrTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct EventPtrTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsEventTableRowRefSizeSmall;
    private readonly int _EventOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal EventPtrTableReader(
      int numberOfRows,
      int eventTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsEventTableRowRefSizeSmall = eventTableRowRefSize == 2;
      this._EventOffset = 0;
      this.RowSize = this._EventOffset + eventTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal EventDefinitionHandle GetEventFor(int rowId) => EventDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._EventOffset, this._IsEventTableRowRefSizeSmall));
  }
}

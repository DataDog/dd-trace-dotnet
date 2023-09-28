// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.PropertyPtrTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct PropertyPtrTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsPropertyTableRowRefSizeSmall;
    private readonly int _PropertyOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal PropertyPtrTableReader(
      int numberOfRows,
      int propertyTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsPropertyTableRowRefSizeSmall = propertyTableRowRefSize == 2;
      this._PropertyOffset = 0;
      this.RowSize = this._PropertyOffset + propertyTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal PropertyDefinitionHandle GetPropertyFor(int rowId) => PropertyDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._PropertyOffset, this._IsPropertyTableRowRefSizeSmall));
  }
}

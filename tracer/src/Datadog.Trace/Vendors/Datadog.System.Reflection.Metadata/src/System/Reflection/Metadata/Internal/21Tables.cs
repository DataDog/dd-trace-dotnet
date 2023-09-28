// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.PropertyMapTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct PropertyMapTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsTypeDefTableRowRefSizeSmall;
    private readonly bool _IsPropertyRefSizeSmall;
    private readonly int _ParentOffset;
    private readonly int _PropertyListOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal PropertyMapTableReader(
      int numberOfRows,
      int typeDefTableRowRefSize,
      int propertyRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsTypeDefTableRowRefSizeSmall = typeDefTableRowRefSize == 2;
      this._IsPropertyRefSizeSmall = propertyRefSize == 2;
      this._ParentOffset = 0;
      this._PropertyListOffset = this._ParentOffset + typeDefTableRowRefSize;
      this.RowSize = this._PropertyListOffset + propertyRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal int FindPropertyMapRowIdFor(TypeDefinitionHandle typeDef) => this.Block.LinearSearchReference(this.RowSize, this._ParentOffset, (uint) typeDef.RowId, this._IsTypeDefTableRowRefSizeSmall) + 1;

    internal TypeDefinitionHandle GetParentType(int rowId) => TypeDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._ParentOffset, this._IsTypeDefTableRowRefSizeSmall));

    internal int GetPropertyListStartFor(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._PropertyListOffset, this._IsPropertyRefSizeSmall);
  }
}

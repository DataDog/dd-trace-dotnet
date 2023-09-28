// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ClassLayoutTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal struct ClassLayoutTableReader
  {
    internal int NumberOfRows;
    private readonly bool _IsTypeDefTableRowRefSizeSmall;
    private readonly int _PackagingSizeOffset;
    private readonly int _ClassSizeOffset;
    private readonly int _ParentOffset;
    internal readonly int RowSize;
    internal MemoryBlock Block;

    internal ClassLayoutTableReader(
      int numberOfRows,
      bool declaredSorted,
      int typeDefTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsTypeDefTableRowRefSizeSmall = typeDefTableRowRefSize == 2;
      this._PackagingSizeOffset = 0;
      this._ClassSizeOffset = this._PackagingSizeOffset + 2;
      this._ParentOffset = this._ClassSizeOffset + 4;
      this.RowSize = this._ParentOffset + typeDefTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.ClassLayout);
    }

    internal TypeDefinitionHandle GetParent(int rowId) => TypeDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._ParentOffset, this._IsTypeDefTableRowRefSizeSmall));

    internal ushort GetPackingSize(int rowId) => this.Block.PeekUInt16((rowId - 1) * this.RowSize + this._PackagingSizeOffset);

    internal uint GetClassSize(int rowId) => this.Block.PeekUInt32((rowId - 1) * this.RowSize + this._ClassSizeOffset);

    internal int FindRow(TypeDefinitionHandle typeDef) => 1 + this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._ParentOffset, (uint) typeDef.RowId, this._IsTypeDefTableRowRefSizeSmall);

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._ParentOffset, this._IsTypeDefTableRowRefSizeSmall);
  }
}

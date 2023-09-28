// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.NestedClassTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct NestedClassTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsTypeDefTableRowRefSizeSmall;
    private readonly int _NestedClassOffset;
    private readonly int _EnclosingClassOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal NestedClassTableReader(
      int numberOfRows,
      bool declaredSorted,
      int typeDefTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsTypeDefTableRowRefSizeSmall = typeDefTableRowRefSize == 2;
      this._NestedClassOffset = 0;
      this._EnclosingClassOffset = this._NestedClassOffset + typeDefTableRowRefSize;
      this.RowSize = this._EnclosingClassOffset + typeDefTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.NestedClass);
    }

    internal TypeDefinitionHandle GetNestedClass(int rowId) => TypeDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._NestedClassOffset, this._IsTypeDefTableRowRefSizeSmall));

    internal TypeDefinitionHandle GetEnclosingClass(int rowId) => TypeDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._EnclosingClassOffset, this._IsTypeDefTableRowRefSizeSmall));

    internal TypeDefinitionHandle FindEnclosingType(TypeDefinitionHandle nestedTypeDef)
    {
      int num = this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._NestedClassOffset, (uint) nestedTypeDef.RowId, this._IsTypeDefTableRowRefSizeSmall);
      return num == -1 ? new TypeDefinitionHandle() : TypeDefinitionHandle.FromRowId(this.Block.PeekReference(num * this.RowSize + this._EnclosingClassOffset, this._IsTypeDefTableRowRefSizeSmall));
    }

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._NestedClassOffset, this._IsTypeDefTableRowRefSizeSmall);
  }
}

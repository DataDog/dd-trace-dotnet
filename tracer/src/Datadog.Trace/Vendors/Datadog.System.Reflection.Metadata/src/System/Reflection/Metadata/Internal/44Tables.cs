// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.GenericParamConstraintTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct GenericParamConstraintTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsGenericParamTableRowRefSizeSmall;
    private readonly bool _IsTypeDefOrRefRefSizeSmall;
    private readonly int _OwnerOffset;
    private readonly int _ConstraintOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal GenericParamConstraintTableReader(
      int numberOfRows,
      bool declaredSorted,
      int genericParamTableRowRefSize,
      int typeDefOrRefRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsGenericParamTableRowRefSizeSmall = genericParamTableRowRefSize == 2;
      this._IsTypeDefOrRefRefSizeSmall = typeDefOrRefRefSize == 2;
      this._OwnerOffset = 0;
      this._ConstraintOffset = this._OwnerOffset + genericParamTableRowRefSize;
      this.RowSize = this._ConstraintOffset + typeDefOrRefRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.GenericParamConstraint);
    }

    internal GenericParameterConstraintHandleCollection FindConstraintsForGenericParam(
      GenericParameterHandle genericParameter)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, this._OwnerOffset, (uint) genericParameter.RowId, this._IsGenericParamTableRowRefSizeSmall, out startRowNumber, out endRowNumber);
      return startRowNumber == -1 ? new GenericParameterConstraintHandleCollection() : new GenericParameterConstraintHandleCollection(startRowNumber + 1, (ushort) (endRowNumber - startRowNumber + 1));
    }

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._OwnerOffset, this._IsGenericParamTableRowRefSizeSmall);

    internal EntityHandle GetConstraint(GenericParameterConstraintHandle handle) => TypeDefOrRefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ConstraintOffset, this._IsTypeDefOrRefRefSizeSmall));

    internal GenericParameterHandle GetOwner(GenericParameterConstraintHandle handle) => GenericParameterHandle.FromRowId(this.Block.PeekReference((handle.RowId - 1) * this.RowSize + this._OwnerOffset, this._IsGenericParamTableRowRefSizeSmall));
  }
}

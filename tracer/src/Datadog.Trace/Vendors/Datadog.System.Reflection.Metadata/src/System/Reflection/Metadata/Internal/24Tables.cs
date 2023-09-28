// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodSemanticsTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct MethodSemanticsTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsMethodTableRowRefSizeSmall;
    private readonly bool _IsHasSemanticRefSizeSmall;
    private readonly int _SemanticsFlagOffset;
    private readonly int _MethodOffset;
    private readonly int _AssociationOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal MethodSemanticsTableReader(
      int numberOfRows,
      bool declaredSorted,
      int methodTableRowRefSize,
      int hasSemanticRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsMethodTableRowRefSizeSmall = methodTableRowRefSize == 2;
      this._IsHasSemanticRefSizeSmall = hasSemanticRefSize == 2;
      this._SemanticsFlagOffset = 0;
      this._MethodOffset = this._SemanticsFlagOffset + 2;
      this._AssociationOffset = this._MethodOffset + methodTableRowRefSize;
      this.RowSize = this._AssociationOffset + hasSemanticRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.MethodSemantics);
    }

    internal MethodDefinitionHandle GetMethod(int rowId) => MethodDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._MethodOffset, this._IsMethodTableRowRefSizeSmall));

    internal MethodSemanticsAttributes GetSemantics(int rowId) => (MethodSemanticsAttributes) this.Block.PeekUInt16((rowId - 1) * this.RowSize + this._SemanticsFlagOffset);

    internal EntityHandle GetAssociation(int rowId) => HasSemanticsTag.ConvertToHandle(this.Block.PeekTaggedReference((rowId - 1) * this.RowSize + this._AssociationOffset, this._IsHasSemanticRefSizeSmall));

    internal int FindSemanticMethodsForEvent(EventDefinitionHandle eventDef, out ushort methodCount)
    {
      methodCount = (ushort) 0;
      return this.BinarySearchTag(HasSemanticsTag.ConvertEventHandleToTag(eventDef), ref methodCount);
    }

    internal int FindSemanticMethodsForProperty(
      PropertyDefinitionHandle propertyDef,
      out ushort methodCount)
    {
      methodCount = (ushort) 0;
      return this.BinarySearchTag(HasSemanticsTag.ConvertPropertyHandleToTag(propertyDef), ref methodCount);
    }

    private int BinarySearchTag(uint searchCodedTag, ref ushort methodCount)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, this._AssociationOffset, searchCodedTag, this._IsHasSemanticRefSizeSmall, out startRowNumber, out endRowNumber);
      if (startRowNumber == -1)
      {
        methodCount = (ushort) 0;
        return 0;
      }
      methodCount = (ushort) (endRowNumber - startRowNumber + 1);
      return startRowNumber + 1;
    }

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._AssociationOffset, this._IsHasSemanticRefSizeSmall);
  }
}

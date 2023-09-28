// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodImplTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct MethodImplTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsTypeDefTableRowRefSizeSmall;
    private readonly bool _IsMethodDefOrRefRefSizeSmall;
    private readonly int _ClassOffset;
    private readonly int _MethodBodyOffset;
    private readonly int _MethodDeclarationOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal MethodImplTableReader(
      int numberOfRows,
      bool declaredSorted,
      int typeDefTableRowRefSize,
      int methodDefOrRefRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsTypeDefTableRowRefSizeSmall = typeDefTableRowRefSize == 2;
      this._IsMethodDefOrRefRefSizeSmall = methodDefOrRefRefSize == 2;
      this._ClassOffset = 0;
      this._MethodBodyOffset = this._ClassOffset + typeDefTableRowRefSize;
      this._MethodDeclarationOffset = this._MethodBodyOffset + methodDefOrRefRefSize;
      this.RowSize = this._MethodDeclarationOffset + methodDefOrRefRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.MethodImpl);
    }

    internal TypeDefinitionHandle GetClass(MethodImplementationHandle handle) => TypeDefinitionHandle.FromRowId(this.Block.PeekReference((handle.RowId - 1) * this.RowSize + this._ClassOffset, this._IsTypeDefTableRowRefSizeSmall));

    internal EntityHandle GetMethodBody(MethodImplementationHandle handle) => MethodDefOrRefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._MethodBodyOffset, this._IsMethodDefOrRefRefSizeSmall));

    internal EntityHandle GetMethodDeclaration(MethodImplementationHandle handle) => MethodDefOrRefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._MethodDeclarationOffset, this._IsMethodDefOrRefRefSizeSmall));

    internal void GetMethodImplRange(
      TypeDefinitionHandle typeDef,
      out int firstImplRowId,
      out int lastImplRowId)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, this._ClassOffset, (uint) typeDef.RowId, this._IsTypeDefTableRowRefSizeSmall, out startRowNumber, out endRowNumber);
      if (startRowNumber == -1)
      {
        firstImplRowId = 1;
        lastImplRowId = 0;
      }
      else
      {
        firstImplRowId = startRowNumber + 1;
        lastImplRowId = endRowNumber + 1;
      }
    }

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._ClassOffset, this._IsTypeDefTableRowRefSizeSmall);
  }
}

﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.GenericParamTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct GenericParamTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsTypeOrMethodDefRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _NumberOffset;
    private readonly int _FlagsOffset;
    private readonly int _OwnerOffset;
    private readonly int _NameOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal GenericParamTableReader(
      int numberOfRows,
      bool declaredSorted,
      int typeOrMethodDefRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsTypeOrMethodDefRefSizeSmall = typeOrMethodDefRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._NumberOffset = 0;
      this._FlagsOffset = this._NumberOffset + 2;
      this._OwnerOffset = this._FlagsOffset + 2;
      this._NameOffset = this._OwnerOffset + typeOrMethodDefRefSize;
      this.RowSize = this._NameOffset + stringHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.GenericParam);
    }

    internal ushort GetNumber(GenericParameterHandle handle) => this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._NumberOffset);

    internal GenericParameterAttributes GetFlags(GenericParameterHandle handle) => (GenericParameterAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal StringHandle GetName(GenericParameterHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal EntityHandle GetOwner(GenericParameterHandle handle) => TypeOrMethodDefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._OwnerOffset, this._IsTypeOrMethodDefRefSizeSmall));

    internal GenericParameterHandleCollection FindGenericParametersForType(
      TypeDefinitionHandle typeDef)
    {
      ushort genericParamCount = 0;
      return new GenericParameterHandleCollection(this.BinarySearchTag(TypeOrMethodDefTag.ConvertTypeDefRowIdToTag(typeDef), ref genericParamCount), genericParamCount);
    }

    internal GenericParameterHandleCollection FindGenericParametersForMethod(
      MethodDefinitionHandle methodDef)
    {
      ushort genericParamCount = 0;
      return new GenericParameterHandleCollection(this.BinarySearchTag(TypeOrMethodDefTag.ConvertMethodDefToTag(methodDef), ref genericParamCount), genericParamCount);
    }

    private int BinarySearchTag(uint searchCodedTag, ref ushort genericParamCount)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, this._OwnerOffset, searchCodedTag, this._IsTypeOrMethodDefRefSizeSmall, out startRowNumber, out endRowNumber);
      if (startRowNumber == -1)
      {
        genericParamCount = (ushort) 0;
        return 0;
      }
      genericParamCount = (ushort) (endRowNumber - startRowNumber + 1);
      return startRowNumber + 1;
    }

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._OwnerOffset, this._IsTypeOrMethodDefRefSizeSmall);
  }
}

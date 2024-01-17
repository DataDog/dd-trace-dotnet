﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.TypeDefTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal struct TypeDefTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsFieldRefSizeSmall;
    private readonly bool _IsMethodRefSizeSmall;
    private readonly bool _IsTypeDefOrRefRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _NameOffset;
    private readonly int _NamespaceOffset;
    private readonly int _ExtendsOffset;
    private readonly int _FieldListOffset;
    private readonly int _MethodListOffset;
    internal readonly int RowSize;
    internal MemoryBlock Block;

    internal TypeDefTableReader(
      int numberOfRows,
      int fieldRefSize,
      int methodRefSize,
      int typeDefOrRefRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsFieldRefSizeSmall = fieldRefSize == 2;
      this._IsMethodRefSizeSmall = methodRefSize == 2;
      this._IsTypeDefOrRefRefSizeSmall = typeDefOrRefRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._NameOffset = this._FlagsOffset + 4;
      this._NamespaceOffset = this._NameOffset + stringHeapRefSize;
      this._ExtendsOffset = this._NamespaceOffset + stringHeapRefSize;
      this._FieldListOffset = this._ExtendsOffset + typeDefOrRefRefSize;
      this._MethodListOffset = this._FieldListOffset + fieldRefSize;
      this.RowSize = this._MethodListOffset + methodRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal TypeAttributes GetFlags(TypeDefinitionHandle handle) => (TypeAttributes) this.Block.PeekUInt32((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal NamespaceDefinitionHandle GetNamespaceDefinition(TypeDefinitionHandle handle) => NamespaceDefinitionHandle.FromFullNameOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NamespaceOffset, this._IsStringHeapRefSizeSmall));

    internal StringHandle GetNamespace(TypeDefinitionHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NamespaceOffset, this._IsStringHeapRefSizeSmall));

    internal StringHandle GetName(TypeDefinitionHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal EntityHandle GetExtends(TypeDefinitionHandle handle) => TypeDefOrRefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ExtendsOffset, this._IsTypeDefOrRefRefSizeSmall));

    internal int GetFieldStart(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._FieldListOffset, this._IsFieldRefSizeSmall);

    internal int GetMethodStart(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._MethodListOffset, this._IsMethodRefSizeSmall);

    internal TypeDefinitionHandle FindTypeContainingMethod(
      int methodDefOrPtrRowId,
      int numberOfMethods)
    {
      int numberOfRows = this.NumberOfRows;
      int rowId1 = this.Block.BinarySearchForSlot(numberOfRows, this.RowSize, this._MethodListOffset, (uint) methodDefOrPtrRowId, this._IsMethodRefSizeSmall) + 1;
      if (rowId1 == 0)
        return new TypeDefinitionHandle();
      if (rowId1 > numberOfRows)
        return methodDefOrPtrRowId <= numberOfMethods ? TypeDefinitionHandle.FromRowId(numberOfRows) : new TypeDefinitionHandle();
      int rowId2;
      if (this.GetMethodStart(rowId1) == methodDefOrPtrRowId)
      {
        for (; rowId1 < numberOfRows; rowId1 = rowId2)
        {
          rowId2 = rowId1 + 1;
          if (this.GetMethodStart(rowId2) != methodDefOrPtrRowId)
            break;
        }
      }
      return TypeDefinitionHandle.FromRowId(rowId1);
    }

    internal TypeDefinitionHandle FindTypeContainingField(
      int fieldDefOrPtrRowId,
      int numberOfFields)
    {
      int numberOfRows = this.NumberOfRows;
      int rowId1 = this.Block.BinarySearchForSlot(numberOfRows, this.RowSize, this._FieldListOffset, (uint) fieldDefOrPtrRowId, this._IsFieldRefSizeSmall) + 1;
      if (rowId1 == 0)
        return new TypeDefinitionHandle();
      if (rowId1 > numberOfRows)
        return fieldDefOrPtrRowId <= numberOfFields ? TypeDefinitionHandle.FromRowId(numberOfRows) : new TypeDefinitionHandle();
      int rowId2;
      if (this.GetFieldStart(rowId1) == fieldDefOrPtrRowId)
      {
        for (; rowId1 < numberOfRows; rowId1 = rowId2)
        {
          rowId2 = rowId1 + 1;
          if (this.GetFieldStart(rowId2) != fieldDefOrPtrRowId)
            break;
        }
      }
      return TypeDefinitionHandle.FromRowId(rowId1);
    }
  }
}

﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.LocalScopeTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct LocalScopeTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _isMethodRefSmall;
    private readonly bool _isImportScopeRefSmall;
    private readonly bool _isLocalConstantRefSmall;
    private readonly bool _isLocalVariableRefSmall;
    private const int MethodOffset = 0;
    private readonly int _importScopeOffset;
    private readonly int _variableListOffset;
    private readonly int _constantListOffset;
    private readonly int _startOffsetOffset;
    private readonly int _lengthOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal LocalScopeTableReader(
      int numberOfRows,
      bool declaredSorted,
      int methodRefSize,
      int importScopeRefSize,
      int localVariableRefSize,
      int localConstantRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._isMethodRefSmall = methodRefSize == 2;
      this._isImportScopeRefSmall = importScopeRefSize == 2;
      this._isLocalVariableRefSmall = localVariableRefSize == 2;
      this._isLocalConstantRefSmall = localConstantRefSize == 2;
      this._importScopeOffset = methodRefSize;
      this._variableListOffset = this._importScopeOffset + importScopeRefSize;
      this._constantListOffset = this._variableListOffset + localVariableRefSize;
      this._startOffsetOffset = this._constantListOffset + localConstantRefSize;
      this._lengthOffset = this._startOffsetOffset + 4;
      this.RowSize = this._lengthOffset + 4;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (numberOfRows <= 0 || declaredSorted)
        return;
      Throw.TableNotSorted(TableIndex.LocalScope);
    }

    internal MethodDefinitionHandle GetMethod(int rowId) => MethodDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize, this._isMethodRefSmall));

    internal ImportScopeHandle GetImportScope(LocalScopeHandle handle) => ImportScopeHandle.FromRowId(this.Block.PeekReference((handle.RowId - 1) * this.RowSize + this._importScopeOffset, this._isImportScopeRefSmall));

    internal int GetVariableStart(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._variableListOffset, this._isLocalVariableRefSmall);

    internal int GetConstantStart(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._constantListOffset, this._isLocalConstantRefSmall);

    internal int GetStartOffset(int rowId) => this.Block.PeekInt32((rowId - 1) * this.RowSize + this._startOffsetOffset);

    internal int GetLength(int rowId) => this.Block.PeekInt32((rowId - 1) * this.RowSize + this._lengthOffset);

    internal int GetEndOffset(int rowId)
    {
      int num = (rowId - 1) * this.RowSize;
      long endOffset = (long) (this.Block.PeekUInt32(num + this._startOffsetOffset) + this.Block.PeekUInt32(num + this._lengthOffset));
      if ((long) (int) endOffset != endOffset)
        Throw.ValueOverflow();
      return (int) endOffset;
    }

    internal void GetLocalScopeRange(
      int methodDefRid,
      out int firstScopeRowId,
      out int lastScopeRowId)
    {
      int startRowNumber;
      int endRowNumber;
      this.Block.BinarySearchReferenceRange(this.NumberOfRows, this.RowSize, 0, (uint) methodDefRid, this._isMethodRefSmall, out startRowNumber, out endRowNumber);
      if (startRowNumber == -1)
      {
        firstScopeRowId = 1;
        lastScopeRowId = 0;
      }
      else
      {
        firstScopeRowId = startRowNumber + 1;
        lastScopeRowId = endRowNumber + 1;
      }
    }
  }
}

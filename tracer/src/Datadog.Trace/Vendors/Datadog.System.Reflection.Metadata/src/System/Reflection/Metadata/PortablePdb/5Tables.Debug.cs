﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.StateMachineMethodTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct StateMachineMethodTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _isMethodRefSizeSmall;
    private const int MoveNextMethodOffset = 0;
    private readonly int _kickoffMethodOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal StateMachineMethodTableReader(
      int numberOfRows,
      bool declaredSorted,
      int methodRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._isMethodRefSizeSmall = methodRefSize == 2;
      this._kickoffMethodOffset = methodRefSize;
      this.RowSize = this._kickoffMethodOffset + methodRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (numberOfRows <= 0 || declaredSorted)
        return;
      Throw.TableNotSorted(TableIndex.StateMachineMethod);
    }

    internal MethodDefinitionHandle FindKickoffMethod(int moveNextMethodRowId)
    {
      int num = this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, 0, (uint) moveNextMethodRowId, this._isMethodRefSizeSmall);
      return num < 0 ? new MethodDefinitionHandle() : this.GetKickoffMethod(num + 1);
    }

    private MethodDefinitionHandle GetKickoffMethod(int rowId) => MethodDefinitionHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._kickoffMethodOffset, this._isMethodRefSizeSmall));
  }
}

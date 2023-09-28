// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ParamPtrTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ParamPtrTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsParamTableRowRefSizeSmall;
    private readonly int _ParamOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ParamPtrTableReader(
      int numberOfRows,
      int paramTableRowRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsParamTableRowRefSizeSmall = paramTableRowRefSize == 2;
      this._ParamOffset = 0;
      this.RowSize = this._ParamOffset + paramTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal ParameterHandle GetParamFor(int rowId) => ParameterHandle.FromRowId(this.Block.PeekReference((rowId - 1) * this.RowSize + this._ParamOffset, this._IsParamTableRowRefSizeSmall));
  }
}

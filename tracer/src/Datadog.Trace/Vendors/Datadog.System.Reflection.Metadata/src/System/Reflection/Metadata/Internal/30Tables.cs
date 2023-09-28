// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.EnCLogTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct EnCLogTableReader
  {
    internal readonly int NumberOfRows;
    private readonly int _TokenOffset;
    private readonly int _FuncCodeOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal EnCLogTableReader(
      int numberOfRows,
      MemoryBlock containingBlock,
      int containingBlockOffset,
      MetadataStreamKind metadataStreamKind)
    {
      this.NumberOfRows = metadataStreamKind == MetadataStreamKind.Compressed ? 0 : numberOfRows;
      this._TokenOffset = 0;
      this._FuncCodeOffset = this._TokenOffset + 4;
      this.RowSize = this._FuncCodeOffset + 4;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal uint GetToken(int rowId) => this.Block.PeekUInt32((rowId - 1) * this.RowSize + this._TokenOffset);

    internal EditAndContinueOperation GetFuncCode(int rowId) => (EditAndContinueOperation) this.Block.PeekUInt32((rowId - 1) * this.RowSize + this._FuncCodeOffset);
  }
}

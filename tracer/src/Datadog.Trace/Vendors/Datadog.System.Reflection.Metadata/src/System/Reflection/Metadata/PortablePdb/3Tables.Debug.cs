// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.LocalVariableTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct LocalVariableTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _isStringHeapRefSizeSmall;
    private readonly int _attributesOffset;
    private readonly int _indexOffset;
    private readonly int _nameOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal LocalVariableTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._isStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._attributesOffset = 0;
      this._indexOffset = this._attributesOffset + 2;
      this._nameOffset = this._indexOffset + 2;
      this.RowSize = this._nameOffset + stringHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal LocalVariableAttributes GetAttributes(LocalVariableHandle handle) => (LocalVariableAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._attributesOffset);

    internal ushort GetIndex(LocalVariableHandle handle) => this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._indexOffset);

    internal StringHandle GetName(LocalVariableHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._nameOffset, this._isStringHeapRefSizeSmall));
  }
}

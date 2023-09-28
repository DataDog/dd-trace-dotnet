// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.EventTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal struct EventTableReader
  {
    internal int NumberOfRows;
    private readonly bool _IsTypeDefOrRefRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _NameOffset;
    private readonly int _EventTypeOffset;
    internal readonly int RowSize;
    internal MemoryBlock Block;

    internal EventTableReader(
      int numberOfRows,
      int typeDefOrRefRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsTypeDefOrRefRefSizeSmall = typeDefOrRefRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._NameOffset = this._FlagsOffset + 2;
      this._EventTypeOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._EventTypeOffset + typeDefOrRefRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal EventAttributes GetFlags(EventDefinitionHandle handle) => (EventAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal StringHandle GetName(EventDefinitionHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal EntityHandle GetEventType(EventDefinitionHandle handle) => TypeDefOrRefTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._EventTypeOffset, this._IsTypeDefOrRefRefSizeSmall));
  }
}

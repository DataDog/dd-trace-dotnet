// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ModuleTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ModuleTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsGUIDHeapRefSizeSmall;
    private readonly int _GenerationOffset;
    private readonly int _NameOffset;
    private readonly int _MVIdOffset;
    private readonly int _EnCIdOffset;
    private readonly int _EnCBaseIdOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ModuleTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      int guidHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsGUIDHeapRefSizeSmall = guidHeapRefSize == 2;
      this._GenerationOffset = 0;
      this._NameOffset = this._GenerationOffset + 2;
      this._MVIdOffset = this._NameOffset + stringHeapRefSize;
      this._EnCIdOffset = this._MVIdOffset + guidHeapRefSize;
      this._EnCBaseIdOffset = this._EnCIdOffset + guidHeapRefSize;
      this.RowSize = this._EnCBaseIdOffset + guidHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal ushort GetGeneration() => this.Block.PeekUInt16(this._GenerationOffset);

    internal StringHandle GetName() => StringHandle.FromOffset(this.Block.PeekHeapReference(this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal GuidHandle GetMvid() => GuidHandle.FromIndex(this.Block.PeekHeapReference(this._MVIdOffset, this._IsGUIDHeapRefSizeSmall));

    internal GuidHandle GetEncId() => GuidHandle.FromIndex(this.Block.PeekHeapReference(this._EnCIdOffset, this._IsGUIDHeapRefSizeSmall));

    internal GuidHandle GetEncBaseId() => GuidHandle.FromIndex(this.Block.PeekHeapReference(this._EnCBaseIdOffset, this._IsGUIDHeapRefSizeSmall));
  }
}

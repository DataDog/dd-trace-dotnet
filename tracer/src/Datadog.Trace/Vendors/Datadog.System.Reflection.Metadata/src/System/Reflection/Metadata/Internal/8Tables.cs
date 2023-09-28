// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ParamTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ParamTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _SequenceOffset;
    private readonly int _NameOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ParamTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._SequenceOffset = this._FlagsOffset + 2;
      this._NameOffset = this._SequenceOffset + 2;
      this.RowSize = this._NameOffset + stringHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal ParameterAttributes GetFlags(ParameterHandle handle) => (ParameterAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal ushort GetSequence(ParameterHandle handle) => this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._SequenceOffset);

    internal StringHandle GetName(ParameterHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));
  }
}

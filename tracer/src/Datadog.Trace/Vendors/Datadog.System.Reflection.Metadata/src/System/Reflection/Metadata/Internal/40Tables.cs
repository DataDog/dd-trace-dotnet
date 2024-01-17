﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ManifestResourceTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ManifestResourceTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsImplementationRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _OffsetOffset;
    private readonly int _FlagsOffset;
    private readonly int _NameOffset;
    private readonly int _ImplementationOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ManifestResourceTableReader(
      int numberOfRows,
      int implementationRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsImplementationRefSizeSmall = implementationRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._OffsetOffset = 0;
      this._FlagsOffset = this._OffsetOffset + 4;
      this._NameOffset = this._FlagsOffset + 4;
      this._ImplementationOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._ImplementationOffset + implementationRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal StringHandle GetName(ManifestResourceHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal EntityHandle GetImplementation(ManifestResourceHandle handle) => ImplementationTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ImplementationOffset, this._IsImplementationRefSizeSmall));

    internal uint GetOffset(ManifestResourceHandle handle) => this.Block.PeekUInt32((handle.RowId - 1) * this.RowSize + this._OffsetOffset);

    internal ManifestResourceAttributes GetFlags(ManifestResourceHandle handle) => (ManifestResourceAttributes) this.Block.PeekUInt32((handle.RowId - 1) * this.RowSize + this._FlagsOffset);
  }
}

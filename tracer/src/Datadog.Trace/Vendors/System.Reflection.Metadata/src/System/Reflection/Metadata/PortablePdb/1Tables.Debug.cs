//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
#nullable enable
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodDebugInformationTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal;

namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335
{
  internal readonly struct MethodDebugInformationTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _isDocumentRefSmall;
    private readonly bool _isBlobHeapRefSizeSmall;
    private const int DocumentOffset = 0;
    private readonly int _sequencePointsOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal MethodDebugInformationTableReader(
      int numberOfRows,
      int documentRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._isDocumentRefSmall = documentRefSize == 2;
      this._isBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._sequencePointsOffset = documentRefSize;
      this.RowSize = this._sequencePointsOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal DocumentHandle GetDocument(MethodDebugInformationHandle handle) => DocumentHandle.FromRowId(this.Block.PeekReference((handle.RowId - 1) * this.RowSize, this._isDocumentRefSmall));

    internal BlobHandle GetSequencePoints(MethodDebugInformationHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._sequencePointsOffset, this._isBlobHeapRefSizeSmall));
  }
}

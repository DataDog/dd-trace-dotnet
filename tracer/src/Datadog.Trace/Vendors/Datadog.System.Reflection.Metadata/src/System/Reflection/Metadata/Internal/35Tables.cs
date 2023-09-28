// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.AssemblyRefTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct AssemblyRefTableReader
  {
    /// <summary>
    /// In CLI metadata equal to the actual number of entries in AssemblyRef table.
    /// In WinMD metadata it includes synthesized AssemblyRefs in addition.
    /// </summary>
    internal readonly int NumberOfNonVirtualRows;
    internal readonly int NumberOfVirtualRows;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _MajorVersionOffset;
    private readonly int _MinorVersionOffset;
    private readonly int _BuildNumberOffset;
    private readonly int _RevisionNumberOffset;
    private readonly int _FlagsOffset;
    private readonly int _PublicKeyOrTokenOffset;
    private readonly int _NameOffset;
    private readonly int _CultureOffset;
    private readonly int _HashValueOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal AssemblyRefTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset,
      MetadataKind metadataKind)
    {
      this.NumberOfNonVirtualRows = numberOfRows;
      this.NumberOfVirtualRows = metadataKind == MetadataKind.Ecma335 ? 0 : 6;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._MajorVersionOffset = 0;
      this._MinorVersionOffset = this._MajorVersionOffset + 2;
      this._BuildNumberOffset = this._MinorVersionOffset + 2;
      this._RevisionNumberOffset = this._BuildNumberOffset + 2;
      this._FlagsOffset = this._RevisionNumberOffset + 2;
      this._PublicKeyOrTokenOffset = this._FlagsOffset + 4;
      this._NameOffset = this._PublicKeyOrTokenOffset + blobHeapRefSize;
      this._CultureOffset = this._NameOffset + stringHeapRefSize;
      this._HashValueOffset = this._CultureOffset + stringHeapRefSize;
      this.RowSize = this._HashValueOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal Version GetVersion(int rowId)
    {
      int num = (rowId - 1) * this.RowSize;
      return new Version((int) this.Block.PeekUInt16(num + this._MajorVersionOffset), (int) this.Block.PeekUInt16(num + this._MinorVersionOffset), (int) this.Block.PeekUInt16(num + this._BuildNumberOffset), (int) this.Block.PeekUInt16(num + this._RevisionNumberOffset));
    }

    internal AssemblyFlags GetFlags(int rowId) => (AssemblyFlags) this.Block.PeekUInt32((rowId - 1) * this.RowSize + this._FlagsOffset);

    internal BlobHandle GetPublicKeyOrToken(int rowId) => BlobHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._PublicKeyOrTokenOffset, this._IsBlobHeapRefSizeSmall));

    internal StringHandle GetName(int rowId) => StringHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal StringHandle GetCulture(int rowId) => StringHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._CultureOffset, this._IsStringHeapRefSizeSmall));

    internal BlobHandle GetHashValue(int rowId) => BlobHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._HashValueOffset, this._IsBlobHeapRefSizeSmall));
  }
}

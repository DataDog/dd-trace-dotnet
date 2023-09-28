// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.AssemblyTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct AssemblyTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _HashAlgIdOffset;
    private readonly int _MajorVersionOffset;
    private readonly int _MinorVersionOffset;
    private readonly int _BuildNumberOffset;
    private readonly int _RevisionNumberOffset;
    private readonly int _FlagsOffset;
    private readonly int _PublicKeyOffset;
    private readonly int _NameOffset;
    private readonly int _CultureOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal AssemblyTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows > 1 ? 1 : numberOfRows;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._HashAlgIdOffset = 0;
      this._MajorVersionOffset = this._HashAlgIdOffset + 4;
      this._MinorVersionOffset = this._MajorVersionOffset + 2;
      this._BuildNumberOffset = this._MinorVersionOffset + 2;
      this._RevisionNumberOffset = this._BuildNumberOffset + 2;
      this._FlagsOffset = this._RevisionNumberOffset + 2;
      this._PublicKeyOffset = this._FlagsOffset + 4;
      this._NameOffset = this._PublicKeyOffset + blobHeapRefSize;
      this._CultureOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._CultureOffset + stringHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal AssemblyHashAlgorithm GetHashAlgorithm() => (AssemblyHashAlgorithm) this.Block.PeekUInt32(this._HashAlgIdOffset);

    internal Version GetVersion() => new Version((int) this.Block.PeekUInt16(this._MajorVersionOffset), (int) this.Block.PeekUInt16(this._MinorVersionOffset), (int) this.Block.PeekUInt16(this._BuildNumberOffset), (int) this.Block.PeekUInt16(this._RevisionNumberOffset));

    internal AssemblyFlags GetFlags() => (AssemblyFlags) this.Block.PeekUInt32(this._FlagsOffset);

    internal BlobHandle GetPublicKey() => BlobHandle.FromOffset(this.Block.PeekHeapReference(this._PublicKeyOffset, this._IsBlobHeapRefSizeSmall));

    internal StringHandle GetName() => StringHandle.FromOffset(this.Block.PeekHeapReference(this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal StringHandle GetCulture() => StringHandle.FromOffset(this.Block.PeekHeapReference(this._CultureOffset, this._IsStringHeapRefSizeSmall));
  }
}

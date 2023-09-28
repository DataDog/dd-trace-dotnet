// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.DocumentTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct DocumentTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _isGuidHeapRefSizeSmall;
    private readonly bool _isBlobHeapRefSizeSmall;
    private const int NameOffset = 0;
    private readonly int _hashAlgorithmOffset;
    private readonly int _hashOffset;
    private readonly int _languageOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal DocumentTableReader(
      int numberOfRows,
      int guidHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._isGuidHeapRefSizeSmall = guidHeapRefSize == 2;
      this._isBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._hashAlgorithmOffset = blobHeapRefSize;
      this._hashOffset = this._hashAlgorithmOffset + guidHeapRefSize;
      this._languageOffset = this._hashOffset + blobHeapRefSize;
      this.RowSize = this._languageOffset + guidHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal DocumentNameBlobHandle GetName(DocumentHandle handle) => DocumentNameBlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize, this._isBlobHeapRefSizeSmall));

    internal GuidHandle GetHashAlgorithm(DocumentHandle handle) => GuidHandle.FromIndex(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._hashAlgorithmOffset, this._isGuidHeapRefSizeSmall));

    internal BlobHandle GetHash(DocumentHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._hashOffset, this._isBlobHeapRefSizeSmall));

    internal GuidHandle GetLanguage(DocumentHandle handle) => GuidHandle.FromIndex(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._languageOffset, this._isGuidHeapRefSizeSmall));
  }
}

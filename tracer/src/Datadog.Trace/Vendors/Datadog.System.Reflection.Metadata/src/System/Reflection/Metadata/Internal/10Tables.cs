// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MemberRefTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal struct MemberRefTableReader
  {
    internal int NumberOfRows;
    private readonly bool _IsMemberRefParentRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _ClassOffset;
    private readonly int _NameOffset;
    private readonly int _SignatureOffset;
    internal readonly int RowSize;
    internal MemoryBlock Block;

    internal MemberRefTableReader(
      int numberOfRows,
      int memberRefParentRefSize,
      int stringHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsMemberRefParentRefSizeSmall = memberRefParentRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._ClassOffset = 0;
      this._NameOffset = this._ClassOffset + memberRefParentRefSize;
      this._SignatureOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._SignatureOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal BlobHandle GetSignature(MemberReferenceHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._SignatureOffset, this._IsBlobHeapRefSizeSmall));

    internal StringHandle GetName(MemberReferenceHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal EntityHandle GetClass(MemberReferenceHandle handle) => MemberRefParentTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ClassOffset, this._IsMemberRefParentRefSizeSmall));
  }
}

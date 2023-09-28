// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.PropertyTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct PropertyTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _NameOffset;
    private readonly int _SignatureOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal PropertyTableReader(
      int numberOfRows,
      int stringHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._NameOffset = this._FlagsOffset + 2;
      this._SignatureOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._SignatureOffset + blobHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal PropertyAttributes GetFlags(PropertyDefinitionHandle handle) => (PropertyAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal StringHandle GetName(PropertyDefinitionHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal BlobHandle GetSignature(PropertyDefinitionHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._SignatureOffset, this._IsBlobHeapRefSizeSmall));
  }
}

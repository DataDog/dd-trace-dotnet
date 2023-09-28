// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct MethodTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsParamRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly bool _IsBlobHeapRefSizeSmall;
    private readonly int _RvaOffset;
    private readonly int _ImplFlagsOffset;
    private readonly int _FlagsOffset;
    private readonly int _NameOffset;
    private readonly int _SignatureOffset;
    private readonly int _ParamListOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal MethodTableReader(
      int numberOfRows,
      int paramRefSize,
      int stringHeapRefSize,
      int blobHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsParamRefSizeSmall = paramRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._IsBlobHeapRefSizeSmall = blobHeapRefSize == 2;
      this._RvaOffset = 0;
      this._ImplFlagsOffset = this._RvaOffset + 4;
      this._FlagsOffset = this._ImplFlagsOffset + 2;
      this._NameOffset = this._FlagsOffset + 2;
      this._SignatureOffset = this._NameOffset + stringHeapRefSize;
      this._ParamListOffset = this._SignatureOffset + blobHeapRefSize;
      this.RowSize = this._ParamListOffset + paramRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal int GetParamStart(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._ParamListOffset, this._IsParamRefSizeSmall);

    internal BlobHandle GetSignature(MethodDefinitionHandle handle) => BlobHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._SignatureOffset, this._IsBlobHeapRefSizeSmall));

    internal int GetRva(MethodDefinitionHandle handle) => this.Block.PeekInt32((handle.RowId - 1) * this.RowSize + this._RvaOffset);

    internal StringHandle GetName(MethodDefinitionHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal MethodAttributes GetFlags(MethodDefinitionHandle handle) => (MethodAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._FlagsOffset);

    internal MethodImplAttributes GetImplFlags(MethodDefinitionHandle handle) => (MethodImplAttributes) this.Block.PeekUInt16((handle.RowId - 1) * this.RowSize + this._ImplFlagsOffset);
  }
}

// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.TypeRefTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct TypeRefTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsResolutionScopeRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _ResolutionScopeOffset;
    private readonly int _NameOffset;
    private readonly int _NamespaceOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal TypeRefTableReader(
      int numberOfRows,
      int resolutionScopeRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsResolutionScopeRefSizeSmall = resolutionScopeRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._ResolutionScopeOffset = 0;
      this._NameOffset = this._ResolutionScopeOffset + resolutionScopeRefSize;
      this._NamespaceOffset = this._NameOffset + stringHeapRefSize;
      this.RowSize = this._NamespaceOffset + stringHeapRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal EntityHandle GetResolutionScope(TypeReferenceHandle handle) => ResolutionScopeTag.ConvertToHandle(this.Block.PeekTaggedReference((handle.RowId - 1) * this.RowSize + this._ResolutionScopeOffset, this._IsResolutionScopeRefSizeSmall));

    internal StringHandle GetName(TypeReferenceHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NameOffset, this._IsStringHeapRefSizeSmall));

    internal StringHandle GetNamespace(TypeReferenceHandle handle) => StringHandle.FromOffset(this.Block.PeekHeapReference((handle.RowId - 1) * this.RowSize + this._NamespaceOffset, this._IsStringHeapRefSizeSmall));
  }
}

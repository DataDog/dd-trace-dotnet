// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ExportedTypeTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ExportedTypeTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsImplementationRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _TypeDefIdOffset;
    private readonly int _TypeNameOffset;
    private readonly int _TypeNamespaceOffset;
    private readonly int _ImplementationOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ExportedTypeTableReader(
      int numberOfRows,
      int implementationRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsImplementationRefSizeSmall = implementationRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._TypeDefIdOffset = this._FlagsOffset + 4;
      this._TypeNameOffset = this._TypeDefIdOffset + 4;
      this._TypeNamespaceOffset = this._TypeNameOffset + stringHeapRefSize;
      this._ImplementationOffset = this._TypeNamespaceOffset + stringHeapRefSize;
      this.RowSize = this._ImplementationOffset + implementationRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
    }

    internal StringHandle GetTypeName(int rowId) => StringHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._TypeNameOffset, this._IsStringHeapRefSizeSmall));

    internal StringHandle GetTypeNamespaceString(int rowId) => StringHandle.FromOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._TypeNamespaceOffset, this._IsStringHeapRefSizeSmall));

    internal NamespaceDefinitionHandle GetTypeNamespace(int rowId) => NamespaceDefinitionHandle.FromFullNameOffset(this.Block.PeekHeapReference((rowId - 1) * this.RowSize + this._TypeNamespaceOffset, this._IsStringHeapRefSizeSmall));

    internal EntityHandle GetImplementation(int rowId) => ImplementationTag.ConvertToHandle(this.Block.PeekTaggedReference((rowId - 1) * this.RowSize + this._ImplementationOffset, this._IsImplementationRefSizeSmall));

    internal TypeAttributes GetFlags(int rowId) => (TypeAttributes) this.Block.PeekUInt32((rowId - 1) * this.RowSize + this._FlagsOffset);

    internal int GetTypeDefId(int rowId) => this.Block.PeekInt32((rowId - 1) * this.RowSize + this._TypeDefIdOffset);

    internal int GetNamespace(int rowId) => this.Block.PeekReference((rowId - 1) * this.RowSize + this._TypeNamespaceOffset, this._IsStringHeapRefSizeSmall);
  }
}

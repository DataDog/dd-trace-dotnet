// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ImplMapTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal readonly struct ImplMapTableReader
  {
    internal readonly int NumberOfRows;
    private readonly bool _IsModuleRefTableRowRefSizeSmall;
    private readonly bool _IsMemberForwardRowRefSizeSmall;
    private readonly bool _IsStringHeapRefSizeSmall;
    private readonly int _FlagsOffset;
    private readonly int _MemberForwardedOffset;
    private readonly int _ImportNameOffset;
    private readonly int _ImportScopeOffset;
    internal readonly int RowSize;
    internal readonly MemoryBlock Block;

    internal ImplMapTableReader(
      int numberOfRows,
      bool declaredSorted,
      int moduleRefTableRowRefSize,
      int memberForwardedRefSize,
      int stringHeapRefSize,
      MemoryBlock containingBlock,
      int containingBlockOffset)
    {
      this.NumberOfRows = numberOfRows;
      this._IsModuleRefTableRowRefSizeSmall = moduleRefTableRowRefSize == 2;
      this._IsMemberForwardRowRefSizeSmall = memberForwardedRefSize == 2;
      this._IsStringHeapRefSizeSmall = stringHeapRefSize == 2;
      this._FlagsOffset = 0;
      this._MemberForwardedOffset = this._FlagsOffset + 2;
      this._ImportNameOffset = this._MemberForwardedOffset + memberForwardedRefSize;
      this._ImportScopeOffset = this._ImportNameOffset + stringHeapRefSize;
      this.RowSize = this._ImportScopeOffset + moduleRefTableRowRefSize;
      this.Block = containingBlock.GetMemoryBlockAt(containingBlockOffset, this.RowSize * numberOfRows);
      if (declaredSorted || this.CheckSorted())
        return;
      Throw.TableNotSorted(TableIndex.ImplMap);
    }

    internal MethodImport GetImport(int rowId)
    {
      int num = (rowId - 1) * this.RowSize;
      return new MethodImport((MethodImportAttributes) this.Block.PeekUInt16(num + this._FlagsOffset), StringHandle.FromOffset(this.Block.PeekHeapReference(num + this._ImportNameOffset, this._IsStringHeapRefSizeSmall)), ModuleReferenceHandle.FromRowId(this.Block.PeekReference(num + this._ImportScopeOffset, this._IsModuleRefTableRowRefSizeSmall)));
    }

    internal EntityHandle GetMemberForwarded(int rowId) => MemberForwardedTag.ConvertToHandle(this.Block.PeekTaggedReference((rowId - 1) * this.RowSize + this._MemberForwardedOffset, this._IsMemberForwardRowRefSizeSmall));

    internal int FindImplForMethod(MethodDefinitionHandle methodDef) => this.BinarySearchTag(MemberForwardedTag.ConvertMethodDefToTag(methodDef));

    private int BinarySearchTag(uint searchCodedTag) => this.Block.BinarySearchReference(this.NumberOfRows, this.RowSize, this._MemberForwardedOffset, searchCodedTag, this._IsMemberForwardRowRefSizeSmall) + 1;

    private bool CheckSorted() => this.Block.IsOrderedByReferenceAscending(this.RowSize, this._MemberForwardedOffset, this._IsMemberForwardRowRefSizeSmall);
  }
}

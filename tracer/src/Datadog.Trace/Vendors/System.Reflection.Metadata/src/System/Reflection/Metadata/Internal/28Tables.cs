//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
#nullable enable
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ImplMapTableReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal;

namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335
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

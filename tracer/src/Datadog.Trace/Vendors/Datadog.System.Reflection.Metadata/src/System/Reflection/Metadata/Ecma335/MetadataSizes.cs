﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataSizes
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>
  /// Provides information on sizes of various metadata structures.
  /// </summary>
  public sealed class MetadataSizes
  {
    private const int StreamAlignment = 4;
    internal const int MaxMetadataVersionByteCount = 254;
    internal readonly int MetadataVersionPaddedLength;
    internal const ulong SortedDebugTables = 55169095435288576;
    internal readonly bool IsEncDelta;
    internal readonly bool IsCompressed;
    internal readonly bool BlobReferenceIsSmall;
    internal readonly bool StringReferenceIsSmall;
    internal readonly bool GuidReferenceIsSmall;
    internal readonly bool CustomAttributeTypeCodedIndexIsSmall;
    internal readonly bool DeclSecurityCodedIndexIsSmall;
    internal readonly bool EventDefReferenceIsSmall;
    internal readonly bool FieldDefReferenceIsSmall;
    internal readonly bool GenericParamReferenceIsSmall;
    internal readonly bool HasConstantCodedIndexIsSmall;
    internal readonly bool HasCustomAttributeCodedIndexIsSmall;
    internal readonly bool HasFieldMarshalCodedIndexIsSmall;
    internal readonly bool HasSemanticsCodedIndexIsSmall;
    internal readonly bool ImplementationCodedIndexIsSmall;
    internal readonly bool MemberForwardedCodedIndexIsSmall;
    internal readonly bool MemberRefParentCodedIndexIsSmall;
    internal readonly bool MethodDefReferenceIsSmall;
    internal readonly bool MethodDefOrRefCodedIndexIsSmall;
    internal readonly bool ModuleRefReferenceIsSmall;
    internal readonly bool ParameterReferenceIsSmall;
    internal readonly bool PropertyDefReferenceIsSmall;
    internal readonly bool ResolutionScopeCodedIndexIsSmall;
    internal readonly bool TypeDefReferenceIsSmall;
    internal readonly bool TypeDefOrRefCodedIndexIsSmall;
    internal readonly bool TypeOrMethodDefCodedIndexIsSmall;
    internal readonly bool DocumentReferenceIsSmall;
    internal readonly bool LocalVariableReferenceIsSmall;
    internal readonly bool LocalConstantReferenceIsSmall;
    internal readonly bool ImportScopeReferenceIsSmall;
    internal readonly bool HasCustomDebugInformationCodedIndexIsSmall;
    /// <summary>
    /// Non-empty tables that are emitted into the metadata table stream.
    /// </summary>
    internal readonly ulong PresentTablesMask;
    /// <summary>
    /// Non-empty tables stored in an external metadata table stream that might be referenced from the metadata table stream being emitted.
    /// </summary>
    internal readonly ulong ExternalTablesMask;
    /// <summary>
    /// Overall size of metadata stream storage (stream headers, table stream, heaps, additional streams).
    /// Aligned to <see cref="F:System.Reflection.Metadata.Ecma335.MetadataSizes.StreamAlignment" />.
    /// </summary>
    internal readonly int MetadataStreamStorageSize;
    /// <summary>
    /// The size of metadata stream (#- or #~). Aligned.
    /// Aligned to <see cref="F:System.Reflection.Metadata.Ecma335.MetadataSizes.StreamAlignment" />.
    /// </summary>
    internal readonly int MetadataTableStreamSize;
    /// <summary>The size of #Pdb stream. Aligned.</summary>
    internal readonly int StandalonePdbStreamSize;
    internal const int PdbIdSize = 20;

    /// <summary>Exact (unaligned) heap sizes.</summary>
    /// <remarks>Use <see cref="M:System.Reflection.Metadata.Ecma335.MetadataSizes.GetAlignedHeapSize(System.Reflection.Metadata.Ecma335.HeapIndex)" /> to get an aligned heap size.</remarks>
    public ImmutableArray<int> HeapSizes { get; }

    /// <summary>Table row counts.</summary>
    public ImmutableArray<int> RowCounts { get; }

    /// <summary>External table row counts.</summary>
    public ImmutableArray<int> ExternalRowCounts { get; }

    internal MetadataSizes(
      ImmutableArray<int> rowCounts,
      ImmutableArray<int> externalRowCounts,
      ImmutableArray<int> heapSizes,
      int metadataVersionByteCount,
      bool isStandaloneDebugMetadata)
    {
      this.RowCounts = rowCounts;
      this.ExternalRowCounts = externalRowCounts;
      this.HeapSizes = heapSizes;
      this.MetadataVersionPaddedLength = BitArithmetic.Align(metadataVersionByteCount + 1, 4);
      this.PresentTablesMask = MetadataSizes.ComputeNonEmptyTableMask(rowCounts);
      this.ExternalTablesMask = MetadataSizes.ComputeNonEmptyTableMask(externalRowCounts);
      bool flag1 = this.IsPresent(TableIndex.EncLog) || this.IsPresent(TableIndex.EncMap);
      bool flag2 = !flag1;
      this.IsEncDelta = flag1;
      this.IsCompressed = flag2;
      this.BlobReferenceIsSmall = flag2 && heapSizes[2] <= (int) ushort.MaxValue;
      this.StringReferenceIsSmall = flag2 && heapSizes[1] <= (int) ushort.MaxValue;
      this.GuidReferenceIsSmall = flag2 && heapSizes[3] <= (int) ushort.MaxValue;
      this.CustomAttributeTypeCodedIndexIsSmall = this.IsReferenceSmall(3, TableIndex.MethodDef, TableIndex.MemberRef);
      this.DeclSecurityCodedIndexIsSmall = this.IsReferenceSmall(2, TableIndex.MethodDef, TableIndex.TypeDef);
      this.EventDefReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.Event);
      this.FieldDefReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.Field);
      this.GenericParamReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.GenericParam);
      this.HasConstantCodedIndexIsSmall = this.IsReferenceSmall(2, TableIndex.Field, TableIndex.Param, TableIndex.Property);
      this.HasCustomAttributeCodedIndexIsSmall = this.IsReferenceSmall(5, TableIndex.MethodDef, TableIndex.Field, TableIndex.TypeRef, TableIndex.TypeDef, TableIndex.Param, TableIndex.InterfaceImpl, TableIndex.MemberRef, TableIndex.Module, TableIndex.DeclSecurity, TableIndex.Property, TableIndex.Event, TableIndex.StandAloneSig, TableIndex.ModuleRef, TableIndex.TypeSpec, TableIndex.Assembly, TableIndex.AssemblyRef, TableIndex.File, TableIndex.ExportedType, TableIndex.ManifestResource, TableIndex.GenericParam, TableIndex.GenericParamConstraint, TableIndex.MethodSpec);
      this.HasFieldMarshalCodedIndexIsSmall = this.IsReferenceSmall(1, TableIndex.Field, TableIndex.Param);
      this.HasSemanticsCodedIndexIsSmall = this.IsReferenceSmall(1, TableIndex.Event, TableIndex.Property);
      this.ImplementationCodedIndexIsSmall = this.IsReferenceSmall(2, TableIndex.File, TableIndex.AssemblyRef, TableIndex.ExportedType);
      this.MemberForwardedCodedIndexIsSmall = this.IsReferenceSmall(1, TableIndex.Field, TableIndex.MethodDef);
      this.MemberRefParentCodedIndexIsSmall = this.IsReferenceSmall(3, TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.ModuleRef, TableIndex.MethodDef, TableIndex.TypeSpec);
      this.MethodDefReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.MethodDef);
      this.MethodDefOrRefCodedIndexIsSmall = this.IsReferenceSmall(1, TableIndex.MethodDef, TableIndex.MemberRef);
      this.ModuleRefReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.ModuleRef);
      this.ParameterReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.Param);
      this.PropertyDefReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.Property);
      this.ResolutionScopeCodedIndexIsSmall = this.IsReferenceSmall(2, TableIndex.Module, TableIndex.ModuleRef, TableIndex.AssemblyRef, TableIndex.TypeRef);
      this.TypeDefReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.TypeDef);
      this.TypeDefOrRefCodedIndexIsSmall = this.IsReferenceSmall(2, TableIndex.TypeDef, TableIndex.TypeRef, TableIndex.TypeSpec);
      this.TypeOrMethodDefCodedIndexIsSmall = this.IsReferenceSmall(1, TableIndex.TypeDef, TableIndex.MethodDef);
      this.DocumentReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.Document);
      this.LocalVariableReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.LocalVariable);
      this.LocalConstantReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.LocalConstant);
      this.ImportScopeReferenceIsSmall = this.IsReferenceSmall(0, TableIndex.ImportScope);
      this.HasCustomDebugInformationCodedIndexIsSmall = this.IsReferenceSmall(5, TableIndex.MethodDef, TableIndex.Field, TableIndex.TypeRef, TableIndex.TypeDef, TableIndex.Param, TableIndex.InterfaceImpl, TableIndex.MemberRef, TableIndex.Module, TableIndex.DeclSecurity, TableIndex.Property, TableIndex.Event, TableIndex.StandAloneSig, TableIndex.ModuleRef, TableIndex.TypeSpec, TableIndex.Assembly, TableIndex.AssemblyRef, TableIndex.File, TableIndex.ExportedType, TableIndex.ManifestResource, TableIndex.GenericParam, TableIndex.GenericParamConstraint, TableIndex.MethodSpec, TableIndex.Document, TableIndex.LocalScope, TableIndex.LocalVariable, TableIndex.LocalConstant, TableIndex.ImportScope);
      int streamHeaderSize = this.CalculateTableStreamHeaderSize();
      byte rowSize1 = this.BlobReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte rowSize2 = this.StringReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num1 = this.GuidReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num2 = this.CustomAttributeTypeCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num3 = this.DeclSecurityCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num4 = this.EventDefReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num5 = this.FieldDefReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num6 = this.GenericParamReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num7 = this.HasConstantCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num8 = this.HasCustomAttributeCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num9 = this.HasFieldMarshalCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num10 = this.HasSemanticsCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num11 = this.ImplementationCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num12 = this.MemberForwardedCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num13 = this.MemberRefParentCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num14 = this.MethodDefReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num15 = this.MethodDefOrRefCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num16 = this.ModuleRefReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num17 = this.ParameterReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num18 = this.PropertyDefReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num19 = this.ResolutionScopeCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num20 = this.TypeDefReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num21 = this.TypeDefOrRefCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num22 = this.TypeOrMethodDefCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      byte num23 = this.DocumentReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num24 = this.LocalVariableReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num25 = this.LocalConstantReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num26 = this.ImportScopeReferenceIsSmall ? (byte) 2 : (byte) 4;
      byte num27 = this.HasCustomDebugInformationCodedIndexIsSmall ? (byte) 2 : (byte) 4;
      int num28 = BitArithmetic.Align(streamHeaderSize + this.GetTableSize(TableIndex.Module, 2 + 3 * (int) num1 + (int) rowSize2) + this.GetTableSize(TableIndex.TypeRef, (int) num19 + (int) rowSize2 + (int) rowSize2) + this.GetTableSize(TableIndex.TypeDef, 4 + (int) rowSize2 + (int) rowSize2 + (int) num21 + (int) num5 + (int) num14) + this.GetTableSize(TableIndex.Field, 2 + (int) rowSize2 + (int) rowSize1) + this.GetTableSize(TableIndex.MethodDef, 8 + (int) rowSize2 + (int) rowSize1 + (int) num17) + this.GetTableSize(TableIndex.Param, 4 + (int) rowSize2) + this.GetTableSize(TableIndex.InterfaceImpl, (int) num20 + (int) num21) + this.GetTableSize(TableIndex.MemberRef, (int) num13 + (int) rowSize2 + (int) rowSize1) + this.GetTableSize(TableIndex.Constant, 2 + (int) num7 + (int) rowSize1) + this.GetTableSize(TableIndex.CustomAttribute, (int) num8 + (int) num2 + (int) rowSize1) + this.GetTableSize(TableIndex.FieldMarshal, (int) num9 + (int) rowSize1) + this.GetTableSize(TableIndex.DeclSecurity, 2 + (int) num3 + (int) rowSize1) + this.GetTableSize(TableIndex.ClassLayout, 6 + (int) num20) + this.GetTableSize(TableIndex.FieldLayout, 4 + (int) num5) + this.GetTableSize(TableIndex.StandAloneSig, (int) rowSize1) + this.GetTableSize(TableIndex.EventMap, (int) num20 + (int) num4) + this.GetTableSize(TableIndex.Event, 2 + (int) rowSize2 + (int) num21) + this.GetTableSize(TableIndex.PropertyMap, (int) num20 + (int) num18) + this.GetTableSize(TableIndex.Property, 2 + (int) rowSize2 + (int) rowSize1) + this.GetTableSize(TableIndex.MethodSemantics, 2 + (int) num14 + (int) num10) + this.GetTableSize(TableIndex.MethodImpl, (int) num20 + (int) num15 + (int) num15) + this.GetTableSize(TableIndex.ModuleRef, (int) rowSize2) + this.GetTableSize(TableIndex.TypeSpec, (int) rowSize1) + this.GetTableSize(TableIndex.ImplMap, 2 + (int) num12 + (int) rowSize2 + (int) num16) + this.GetTableSize(TableIndex.FieldRva, 4 + (int) num5) + this.GetTableSize(TableIndex.EncLog, 8) + this.GetTableSize(TableIndex.EncMap, 4) + this.GetTableSize(TableIndex.Assembly, 16 + (int) rowSize1 + (int) rowSize2 + (int) rowSize2) + this.GetTableSize(TableIndex.AssemblyRef, 12 + (int) rowSize1 + (int) rowSize2 + (int) rowSize2 + (int) rowSize1) + this.GetTableSize(TableIndex.File, 4 + (int) rowSize2 + (int) rowSize1) + this.GetTableSize(TableIndex.ExportedType, 8 + (int) rowSize2 + (int) rowSize2 + (int) num11) + this.GetTableSize(TableIndex.ManifestResource, 8 + (int) rowSize2 + (int) num11) + this.GetTableSize(TableIndex.NestedClass, (int) num20 + (int) num20) + this.GetTableSize(TableIndex.GenericParam, 4 + (int) num22 + (int) rowSize2) + this.GetTableSize(TableIndex.MethodSpec, (int) num15 + (int) rowSize1) + this.GetTableSize(TableIndex.GenericParamConstraint, (int) num6 + (int) num21) + this.GetTableSize(TableIndex.Document, (int) rowSize1 + (int) num1 + (int) rowSize1 + (int) num1) + this.GetTableSize(TableIndex.MethodDebugInformation, (int) num23 + (int) rowSize1) + this.GetTableSize(TableIndex.LocalScope, (int) num14 + (int) num26 + (int) num24 + (int) num25 + 4 + 4) + this.GetTableSize(TableIndex.LocalVariable, 4 + (int) rowSize2) + this.GetTableSize(TableIndex.LocalConstant, (int) rowSize2 + (int) rowSize1) + this.GetTableSize(TableIndex.ImportScope, (int) num26 + (int) rowSize1) + this.GetTableSize(TableIndex.StateMachineMethod, (int) num14 + (int) num14) + this.GetTableSize(TableIndex.CustomDebugInformation, (int) num27 + (int) num1 + (int) rowSize1) + 1, 4);
      this.MetadataTableStreamSize = num28;
      int num29 = num28 + this.GetAlignedHeapSize(HeapIndex.String) + this.GetAlignedHeapSize(HeapIndex.UserString) + this.GetAlignedHeapSize(HeapIndex.Guid) + this.GetAlignedHeapSize(HeapIndex.Blob);
      this.StandalonePdbStreamSize = isStandaloneDebugMetadata ? this.CalculateStandalonePdbStreamSize() : 0;
      this.MetadataStreamStorageSize = num29 + this.StandalonePdbStreamSize;
    }

    internal bool IsStandaloneDebugMetadata => this.StandalonePdbStreamSize > 0;

    internal bool IsPresent(TableIndex table) => (this.PresentTablesMask & (ulong) (1L << (int) (table & (TableIndex.EncMap | TableIndex.Assembly)))) > 0UL;

    /// <summary>
    /// Metadata header size.
    /// Includes:
    /// - metadata storage signature
    /// - storage header
    /// - stream headers
    /// </summary>
    internal int MetadataHeaderSize => 16 + this.MetadataVersionPaddedLength + 2 + 2 + (this.IsStandaloneDebugMetadata ? 16 : 0) + 76 + (this.IsEncDelta ? 16 : 0);

    internal static int GetMetadataStreamHeaderSize(string streamName) => 8 + BitArithmetic.Align(streamName.Length + 1, 4);

    /// <summary>Total size of metadata (header and all streams).</summary>
    internal int MetadataSize => this.MetadataHeaderSize + this.MetadataStreamStorageSize;

    /// <summary>Returns aligned size of the specified heap.</summary>
    public int GetAlignedHeapSize(HeapIndex index)
    {
      int index1 = (int) index;
      ImmutableArray<int> heapSizes;
      if (index1 >= 0)
      {
        int num = index1;
        heapSizes = this.HeapSizes;
        int length = heapSizes.Length;
        if (num <= length)
          goto label_3;
      }
      Throw.ArgumentOutOfRange(nameof (index));
label_3:
      heapSizes = this.HeapSizes;
      return BitArithmetic.Align(heapSizes[index1], 4);
    }

    internal int CalculateTableStreamHeaderSize()
    {
      int streamHeaderSize = 24;
      for (int index = 0; index < this.RowCounts.Length; ++index)
      {
        if ((1L << index & (long) this.PresentTablesMask) != 0L)
          streamHeaderSize += 4;
      }
      return streamHeaderSize;
    }

    internal int CalculateStandalonePdbStreamSize() => 32 + BitArithmetic.CountBits(this.ExternalTablesMask) * 4;


    #nullable disable
    private static ulong ComputeNonEmptyTableMask(ImmutableArray<int> rowCounts)
    {
      ulong nonEmptyTableMask = 0;
      for (int index = 0; index < rowCounts.Length; ++index)
      {
        if (rowCounts[index] > 0)
          nonEmptyTableMask |= (ulong) (1L << index);
      }
      return nonEmptyTableMask;
    }

    private int GetTableSize(TableIndex index, int rowSize) => this.RowCounts[(int) index] * rowSize;

    private bool IsReferenceSmall(int tagBitSize, params TableIndex[] tables) => this.IsCompressed && this.ReferenceFits(16 - tagBitSize, tables);

    private bool ReferenceFits(int bitCount, TableIndex[] tables)
    {
      int num1 = (1 << bitCount) - 1;
      foreach (TableIndex table in tables)
      {
        ImmutableArray<int> immutableArray = this.RowCounts;
        int num2 = immutableArray[(int) table];
        immutableArray = this.ExternalRowCounts;
        int num3 = immutableArray[(int) table];
        if (num2 + num3 > num1)
          return false;
      }
      return true;
    }
  }
}

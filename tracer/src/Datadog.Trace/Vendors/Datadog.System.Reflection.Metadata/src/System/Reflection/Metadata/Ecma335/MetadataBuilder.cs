﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using System.Reflection;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    public sealed class MetadataBuilder
  {
    private const byte MetadataFormatMajorVersion = 2;
    private const byte MetadataFormatMinorVersion = 0;

    #nullable disable
    private MetadataBuilder.ModuleRow? _moduleRow;
    private MetadataBuilder.AssemblyRow? _assemblyRow;
    private readonly List<MetadataBuilder.ClassLayoutRow> _classLayoutTable = new List<MetadataBuilder.ClassLayoutRow>();
    private readonly List<MetadataBuilder.ConstantRow> _constantTable = new List<MetadataBuilder.ConstantRow>();
    private int _constantTableLastParent;
    private bool _constantTableNeedsSorting;
    private readonly List<MetadataBuilder.CustomAttributeRow> _customAttributeTable = new List<MetadataBuilder.CustomAttributeRow>();
    private int _customAttributeTableLastParent;
    private bool _customAttributeTableNeedsSorting;
    private readonly List<MetadataBuilder.DeclSecurityRow> _declSecurityTable = new List<MetadataBuilder.DeclSecurityRow>();
    private int _declSecurityTableLastParent;
    private bool _declSecurityTableNeedsSorting;
    private readonly List<MetadataBuilder.EncLogRow> _encLogTable = new List<MetadataBuilder.EncLogRow>();
    private readonly List<MetadataBuilder.EncMapRow> _encMapTable = new List<MetadataBuilder.EncMapRow>();
    private readonly List<MetadataBuilder.EventRow> _eventTable = new List<MetadataBuilder.EventRow>();
    private readonly List<MetadataBuilder.EventMapRow> _eventMapTable = new List<MetadataBuilder.EventMapRow>();
    private readonly List<MetadataBuilder.ExportedTypeRow> _exportedTypeTable = new List<MetadataBuilder.ExportedTypeRow>();
    private readonly List<MetadataBuilder.FieldLayoutRow> _fieldLayoutTable = new List<MetadataBuilder.FieldLayoutRow>();
    private readonly List<MetadataBuilder.FieldMarshalRow> _fieldMarshalTable = new List<MetadataBuilder.FieldMarshalRow>();
    private int _fieldMarshalTableLastParent;
    private bool _fieldMarshalTableNeedsSorting;
    private readonly List<MetadataBuilder.FieldRvaRow> _fieldRvaTable = new List<MetadataBuilder.FieldRvaRow>();
    private readonly List<MetadataBuilder.FieldDefRow> _fieldTable = new List<MetadataBuilder.FieldDefRow>();
    private readonly List<MetadataBuilder.FileTableRow> _fileTable = new List<MetadataBuilder.FileTableRow>();
    private readonly List<MetadataBuilder.GenericParamConstraintRow> _genericParamConstraintTable = new List<MetadataBuilder.GenericParamConstraintRow>();
    private readonly List<MetadataBuilder.GenericParamRow> _genericParamTable = new List<MetadataBuilder.GenericParamRow>();
    private readonly List<MetadataBuilder.ImplMapRow> _implMapTable = new List<MetadataBuilder.ImplMapRow>();
    private readonly List<MetadataBuilder.InterfaceImplRow> _interfaceImplTable = new List<MetadataBuilder.InterfaceImplRow>();
    private readonly List<MetadataBuilder.ManifestResourceRow> _manifestResourceTable = new List<MetadataBuilder.ManifestResourceRow>();
    private readonly List<MetadataBuilder.MemberRefRow> _memberRefTable = new List<MetadataBuilder.MemberRefRow>();
    private readonly List<MetadataBuilder.MethodImplRow> _methodImplTable = new List<MetadataBuilder.MethodImplRow>();
    private readonly List<MetadataBuilder.MethodSemanticsRow> _methodSemanticsTable = new List<MetadataBuilder.MethodSemanticsRow>();
    private int _methodSemanticsTableLastAssociation;
    private bool _methodSemanticsTableNeedsSorting;
    private readonly List<MetadataBuilder.MethodSpecRow> _methodSpecTable = new List<MetadataBuilder.MethodSpecRow>();
    private readonly List<MetadataBuilder.MethodRow> _methodDefTable = new List<MetadataBuilder.MethodRow>();
    private readonly List<MetadataBuilder.ModuleRefRow> _moduleRefTable = new List<MetadataBuilder.ModuleRefRow>();
    private readonly List<MetadataBuilder.NestedClassRow> _nestedClassTable = new List<MetadataBuilder.NestedClassRow>();
    private readonly List<MetadataBuilder.ParamRow> _paramTable = new List<MetadataBuilder.ParamRow>();
    private readonly List<MetadataBuilder.PropertyMapRow> _propertyMapTable = new List<MetadataBuilder.PropertyMapRow>();
    private readonly List<MetadataBuilder.PropertyRow> _propertyTable = new List<MetadataBuilder.PropertyRow>();
    private readonly List<MetadataBuilder.TypeDefRow> _typeDefTable = new List<MetadataBuilder.TypeDefRow>();
    private readonly List<MetadataBuilder.TypeRefRow> _typeRefTable = new List<MetadataBuilder.TypeRefRow>();
    private readonly List<MetadataBuilder.TypeSpecRow> _typeSpecTable = new List<MetadataBuilder.TypeSpecRow>();
    private readonly List<MetadataBuilder.AssemblyRefTableRow> _assemblyRefTable = new List<MetadataBuilder.AssemblyRefTableRow>();
    private readonly List<MetadataBuilder.StandaloneSigRow> _standAloneSigTable = new List<MetadataBuilder.StandaloneSigRow>();
    private readonly List<MetadataBuilder.DocumentRow> _documentTable = new List<MetadataBuilder.DocumentRow>();
    private readonly List<MetadataBuilder.MethodDebugInformationRow> _methodDebugInformationTable = new List<MetadataBuilder.MethodDebugInformationRow>();
    private readonly List<MetadataBuilder.LocalScopeRow> _localScopeTable = new List<MetadataBuilder.LocalScopeRow>();
    private readonly List<MetadataBuilder.LocalVariableRow> _localVariableTable = new List<MetadataBuilder.LocalVariableRow>();
    private readonly List<MetadataBuilder.LocalConstantRow> _localConstantTable = new List<MetadataBuilder.LocalConstantRow>();
    private readonly List<MetadataBuilder.ImportScopeRow> _importScopeTable = new List<MetadataBuilder.ImportScopeRow>();
    private readonly List<MetadataBuilder.StateMachineMethodRow> _stateMachineMethodTable = new List<MetadataBuilder.StateMachineMethodRow>();
    private readonly List<MetadataBuilder.CustomDebugInformationRow> _customDebugInformationTable = new List<MetadataBuilder.CustomDebugInformationRow>();
    private const int UserStringHeapSizeLimit = 16777216;
    private readonly Dictionary<string, UserStringHandle> _userStrings = new Dictionary<string, UserStringHandle>(256);
    private readonly MetadataBuilder.HeapBlobBuilder _userStringBuilder = new MetadataBuilder.HeapBlobBuilder(4096);
    private readonly int _userStringHeapStartOffset;
    private readonly Dictionary<string, StringHandle> _strings = new Dictionary<string, StringHandle>(256);
    private readonly int _stringHeapStartOffset;
    private int _stringHeapCapacity = 4096;
    private readonly Dictionary<ImmutableArray<byte>, BlobHandle> _blobs = new Dictionary<ImmutableArray<byte>, BlobHandle>(1024, (IEqualityComparer<ImmutableArray<byte>>) ByteSequenceComparer.Instance);
    private readonly int _blobHeapStartOffset;
    private int _blobHeapSize;
    private readonly Dictionary<Guid, GuidHandle> _guids = new Dictionary<Guid, GuidHandle>();
    private readonly MetadataBuilder.HeapBlobBuilder _guidBuilder = new MetadataBuilder.HeapBlobBuilder(16);


    #nullable enable
    internal SerializedMetadata GetSerializedMetadata(
      ImmutableArray<int> externalRowCounts,
      int metadataVersionByteCount,
      bool isStandaloneDebugMetadata)
    {
      MetadataBuilder.HeapBlobBuilder heapBlobBuilder = new MetadataBuilder.HeapBlobBuilder(this._stringHeapCapacity);
      ImmutableArray<int> stringMap = MetadataBuilder.SerializeStringHeap((BlobBuilder) heapBlobBuilder, this._strings, this._stringHeapStartOffset);
      ImmutableArray<int> heapSizes = ImmutableArray.Create<int>(this._userStringBuilder.Count, heapBlobBuilder.Count, this._blobHeapSize, this._guidBuilder.Count);
      return new SerializedMetadata(new MetadataSizes(this.GetRowCounts(), externalRowCounts, heapSizes, metadataVersionByteCount, isStandaloneDebugMetadata), (BlobBuilder) heapBlobBuilder, stringMap);
    }

    internal static void SerializeMetadataHeader(
      BlobBuilder builder,
      string metadataVersion,
      MetadataSizes sizes)
    {
      int count1 = builder.Count;
      builder.WriteUInt32(1112167234U);
      builder.WriteUInt16((ushort) 1);
      builder.WriteUInt16((ushort) 1);
      builder.WriteUInt32(0U);
      builder.WriteInt32(sizes.MetadataVersionPaddedLength);
      int count2 = builder.Count;
      builder.WriteUTF8(metadataVersion);
      builder.WriteByte((byte) 0);
      int count3 = builder.Count;
      for (int index = 0; index < sizes.MetadataVersionPaddedLength - (count3 - count2); ++index)
        builder.WriteByte((byte) 0);
      builder.WriteUInt16((ushort) 0);
      builder.WriteUInt16((ushort) (5 + (sizes.IsEncDelta ? 1 : 0) + (sizes.IsStandaloneDebugMetadata ? 1 : 0)));
      int metadataHeaderSize = sizes.MetadataHeaderSize;
      if (sizes.IsStandaloneDebugMetadata)
        MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, sizes.StandalonePdbStreamSize, "#Pdb", builder);
      MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, sizes.MetadataTableStreamSize, sizes.IsCompressed ? "#~" : "#-", builder);
      MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, sizes.GetAlignedHeapSize(HeapIndex.String), "#Strings", builder);
      MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, sizes.GetAlignedHeapSize(HeapIndex.UserString), "#US", builder);
      MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, sizes.GetAlignedHeapSize(HeapIndex.Guid), "#GUID", builder);
      MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, sizes.GetAlignedHeapSize(HeapIndex.Blob), "#Blob", builder);
      if (sizes.IsEncDelta)
        MetadataBuilder.SerializeStreamHeader(ref metadataHeaderSize, 0, "#JTD", builder);
      int count4 = builder.Count;
    }


    #nullable disable
    private static void SerializeStreamHeader(
      ref int offsetFromStartOfMetadata,
      int alignedStreamSize,
      string streamName,
      BlobBuilder builder)
    {
      int streamHeaderSize = MetadataSizes.GetMetadataStreamHeaderSize(streamName);
      builder.WriteInt32(offsetFromStartOfMetadata);
      builder.WriteInt32(alignedStreamSize);
      foreach (char ch in streamName)
        builder.WriteByte((byte) ch);
      for (uint index = (uint) (8 + streamName.Length); (long) index < (long) streamHeaderSize; ++index)
        builder.WriteByte((byte) 0);
      offsetFromStartOfMetadata += alignedStreamSize;
    }

    /// <summary>Sets the capacity of the specified table.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="table" /> is not a valid table index.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="rowCount" /> is negative.</exception>
    /// <remarks>
    /// Use to reduce allocations if the approximate number of rows is known ahead of time.
    /// </remarks>
    public void SetCapacity(TableIndex table, int rowCount)
    {
      if (rowCount < 0)
        Throw.ArgumentOutOfRange(nameof (rowCount));
      switch (table)
      {
        case TableIndex.Module:
          break;
        case TableIndex.TypeRef:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.TypeRefRow>(this._typeRefTable, rowCount);
          break;
        case TableIndex.TypeDef:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.TypeDefRow>(this._typeDefTable, rowCount);
          break;
        case TableIndex.FieldPtr:
          break;
        case TableIndex.Field:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.FieldDefRow>(this._fieldTable, rowCount);
          break;
        case TableIndex.MethodPtr:
          break;
        case TableIndex.MethodDef:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.MethodRow>(this._methodDefTable, rowCount);
          break;
        case TableIndex.ParamPtr:
          break;
        case TableIndex.Param:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ParamRow>(this._paramTable, rowCount);
          break;
        case TableIndex.InterfaceImpl:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.InterfaceImplRow>(this._interfaceImplTable, rowCount);
          break;
        case TableIndex.MemberRef:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.MemberRefRow>(this._memberRefTable, rowCount);
          break;
        case TableIndex.Constant:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ConstantRow>(this._constantTable, rowCount);
          break;
        case TableIndex.CustomAttribute:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.CustomAttributeRow>(this._customAttributeTable, rowCount);
          break;
        case TableIndex.FieldMarshal:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.FieldMarshalRow>(this._fieldMarshalTable, rowCount);
          break;
        case TableIndex.DeclSecurity:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.DeclSecurityRow>(this._declSecurityTable, rowCount);
          break;
        case TableIndex.ClassLayout:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ClassLayoutRow>(this._classLayoutTable, rowCount);
          break;
        case TableIndex.FieldLayout:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.FieldLayoutRow>(this._fieldLayoutTable, rowCount);
          break;
        case TableIndex.StandAloneSig:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.StandaloneSigRow>(this._standAloneSigTable, rowCount);
          break;
        case TableIndex.EventMap:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.EventMapRow>(this._eventMapTable, rowCount);
          break;
        case TableIndex.EventPtr:
          break;
        case TableIndex.Event:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.EventRow>(this._eventTable, rowCount);
          break;
        case TableIndex.PropertyMap:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.PropertyMapRow>(this._propertyMapTable, rowCount);
          break;
        case TableIndex.PropertyPtr:
          break;
        case TableIndex.Property:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.PropertyRow>(this._propertyTable, rowCount);
          break;
        case TableIndex.MethodSemantics:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.MethodSemanticsRow>(this._methodSemanticsTable, rowCount);
          break;
        case TableIndex.MethodImpl:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.MethodImplRow>(this._methodImplTable, rowCount);
          break;
        case TableIndex.ModuleRef:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ModuleRefRow>(this._moduleRefTable, rowCount);
          break;
        case TableIndex.TypeSpec:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.TypeSpecRow>(this._typeSpecTable, rowCount);
          break;
        case TableIndex.ImplMap:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ImplMapRow>(this._implMapTable, rowCount);
          break;
        case TableIndex.FieldRva:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.FieldRvaRow>(this._fieldRvaTable, rowCount);
          break;
        case TableIndex.EncLog:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.EncLogRow>(this._encLogTable, rowCount);
          break;
        case TableIndex.EncMap:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.EncMapRow>(this._encMapTable, rowCount);
          break;
        case TableIndex.Assembly:
          break;
        case TableIndex.AssemblyProcessor:
          break;
        case TableIndex.AssemblyOS:
          break;
        case TableIndex.AssemblyRef:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.AssemblyRefTableRow>(this._assemblyRefTable, rowCount);
          break;
        case TableIndex.AssemblyRefProcessor:
          break;
        case TableIndex.AssemblyRefOS:
          break;
        case TableIndex.File:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.FileTableRow>(this._fileTable, rowCount);
          break;
        case TableIndex.ExportedType:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ExportedTypeRow>(this._exportedTypeTable, rowCount);
          break;
        case TableIndex.ManifestResource:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ManifestResourceRow>(this._manifestResourceTable, rowCount);
          break;
        case TableIndex.NestedClass:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.NestedClassRow>(this._nestedClassTable, rowCount);
          break;
        case TableIndex.GenericParam:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.GenericParamRow>(this._genericParamTable, rowCount);
          break;
        case TableIndex.MethodSpec:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.MethodSpecRow>(this._methodSpecTable, rowCount);
          break;
        case TableIndex.GenericParamConstraint:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.GenericParamConstraintRow>(this._genericParamConstraintTable, rowCount);
          break;
        case TableIndex.Document:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.DocumentRow>(this._documentTable, rowCount);
          break;
        case TableIndex.MethodDebugInformation:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.MethodDebugInformationRow>(this._methodDebugInformationTable, rowCount);
          break;
        case TableIndex.LocalScope:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.LocalScopeRow>(this._localScopeTable, rowCount);
          break;
        case TableIndex.LocalVariable:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.LocalVariableRow>(this._localVariableTable, rowCount);
          break;
        case TableIndex.LocalConstant:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.LocalConstantRow>(this._localConstantTable, rowCount);
          break;
        case TableIndex.ImportScope:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.ImportScopeRow>(this._importScopeTable, rowCount);
          break;
        case TableIndex.StateMachineMethod:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.StateMachineMethodRow>(this._stateMachineMethodTable, rowCount);
          break;
        case TableIndex.CustomDebugInformation:
          MetadataBuilder.SetTableCapacity<MetadataBuilder.CustomDebugInformationRow>(this._customDebugInformationTable, rowCount);
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof (table));
      }
    }

    private static void SetTableCapacity<T>(List<T> table, int rowCount)
    {
      if (rowCount <= table.Count)
        return;
      table.Capacity = rowCount;
    }

    /// <summary>
    /// Returns the current number of entires in the specified table.
    /// </summary>
    /// <param name="table">Table index.</param>
    /// <returns>The number of entires in the table.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="table" /> is not a valid table index.</exception>
    public int GetRowCount(TableIndex table)
    {
      switch (table)
      {
        case TableIndex.Module:
          return !this._moduleRow.HasValue ? 0 : 1;
        case TableIndex.TypeRef:
          return this._typeRefTable.Count;
        case TableIndex.TypeDef:
          return this._typeDefTable.Count;
        case TableIndex.FieldPtr:
        case TableIndex.MethodPtr:
        case TableIndex.ParamPtr:
        case TableIndex.EventPtr:
        case TableIndex.PropertyPtr:
        case TableIndex.AssemblyProcessor:
        case TableIndex.AssemblyOS:
        case TableIndex.AssemblyRefProcessor:
        case TableIndex.AssemblyRefOS:
          return 0;
        case TableIndex.Field:
          return this._fieldTable.Count;
        case TableIndex.MethodDef:
          return this._methodDefTable.Count;
        case TableIndex.Param:
          return this._paramTable.Count;
        case TableIndex.InterfaceImpl:
          return this._interfaceImplTable.Count;
        case TableIndex.MemberRef:
          return this._memberRefTable.Count;
        case TableIndex.Constant:
          return this._constantTable.Count;
        case TableIndex.CustomAttribute:
          return this._customAttributeTable.Count;
        case TableIndex.FieldMarshal:
          return this._fieldMarshalTable.Count;
        case TableIndex.DeclSecurity:
          return this._declSecurityTable.Count;
        case TableIndex.ClassLayout:
          return this._classLayoutTable.Count;
        case TableIndex.FieldLayout:
          return this._fieldLayoutTable.Count;
        case TableIndex.StandAloneSig:
          return this._standAloneSigTable.Count;
        case TableIndex.EventMap:
          return this._eventMapTable.Count;
        case TableIndex.Event:
          return this._eventTable.Count;
        case TableIndex.PropertyMap:
          return this._propertyMapTable.Count;
        case TableIndex.Property:
          return this._propertyTable.Count;
        case TableIndex.MethodSemantics:
          return this._methodSemanticsTable.Count;
        case TableIndex.MethodImpl:
          return this._methodImplTable.Count;
        case TableIndex.ModuleRef:
          return this._moduleRefTable.Count;
        case TableIndex.TypeSpec:
          return this._typeSpecTable.Count;
        case TableIndex.ImplMap:
          return this._implMapTable.Count;
        case TableIndex.FieldRva:
          return this._fieldRvaTable.Count;
        case TableIndex.EncLog:
          return this._encLogTable.Count;
        case TableIndex.EncMap:
          return this._encMapTable.Count;
        case TableIndex.Assembly:
          return !this._assemblyRow.HasValue ? 0 : 1;
        case TableIndex.AssemblyRef:
          return this._assemblyRefTable.Count;
        case TableIndex.File:
          return this._fileTable.Count;
        case TableIndex.ExportedType:
          return this._exportedTypeTable.Count;
        case TableIndex.ManifestResource:
          return this._manifestResourceTable.Count;
        case TableIndex.NestedClass:
          return this._nestedClassTable.Count;
        case TableIndex.GenericParam:
          return this._genericParamTable.Count;
        case TableIndex.MethodSpec:
          return this._methodSpecTable.Count;
        case TableIndex.GenericParamConstraint:
          return this._genericParamConstraintTable.Count;
        case TableIndex.Document:
          return this._documentTable.Count;
        case TableIndex.MethodDebugInformation:
          return this._methodDebugInformationTable.Count;
        case TableIndex.LocalScope:
          return this._localScopeTable.Count;
        case TableIndex.LocalVariable:
          return this._localVariableTable.Count;
        case TableIndex.LocalConstant:
          return this._localConstantTable.Count;
        case TableIndex.ImportScope:
          return this._importScopeTable.Count;
        case TableIndex.StateMachineMethod:
          return this._stateMachineMethodTable.Count;
        case TableIndex.CustomDebugInformation:
          return this._customDebugInformationTable.Count;
        default:
          throw new ArgumentOutOfRangeException(nameof (table));
      }
    }


    #nullable enable
    /// <summary>Returns the current number of entires in each table.</summary>
    /// <returns>
    /// An array of size <see cref="F:System.Reflection.Metadata.Ecma335.MetadataTokens.TableCount" /> with each item filled with the current row count of the corresponding table.
    /// </returns>
    public ImmutableArray<int> GetRowCounts()
    {
      ImmutableArray<int>.Builder builder = ImmutableArray.CreateBuilder<int>(MetadataTokens.TableCount);
      builder.Count = MetadataTokens.TableCount;
      builder[32] = this._assemblyRow.HasValue ? 1 : 0;
      builder[35] = this._assemblyRefTable.Count;
      builder[15] = this._classLayoutTable.Count;
      builder[11] = this._constantTable.Count;
      builder[12] = this._customAttributeTable.Count;
      builder[14] = this._declSecurityTable.Count;
      builder[30] = this._encLogTable.Count;
      builder[31] = this._encMapTable.Count;
      builder[18] = this._eventMapTable.Count;
      builder[20] = this._eventTable.Count;
      builder[39] = this._exportedTypeTable.Count;
      builder[16] = this._fieldLayoutTable.Count;
      builder[13] = this._fieldMarshalTable.Count;
      builder[29] = this._fieldRvaTable.Count;
      builder[4] = this._fieldTable.Count;
      builder[38] = this._fileTable.Count;
      builder[44] = this._genericParamConstraintTable.Count;
      builder[42] = this._genericParamTable.Count;
      builder[28] = this._implMapTable.Count;
      builder[9] = this._interfaceImplTable.Count;
      builder[40] = this._manifestResourceTable.Count;
      builder[10] = this._memberRefTable.Count;
      builder[25] = this._methodImplTable.Count;
      builder[24] = this._methodSemanticsTable.Count;
      builder[43] = this._methodSpecTable.Count;
      builder[6] = this._methodDefTable.Count;
      builder[26] = this._moduleRefTable.Count;
      builder[0] = this._moduleRow.HasValue ? 1 : 0;
      builder[41] = this._nestedClassTable.Count;
      builder[8] = this._paramTable.Count;
      builder[21] = this._propertyMapTable.Count;
      builder[23] = this._propertyTable.Count;
      builder[17] = this._standAloneSigTable.Count;
      builder[2] = this._typeDefTable.Count;
      builder[1] = this._typeRefTable.Count;
      builder[27] = this._typeSpecTable.Count;
      builder[48] = this._documentTable.Count;
      builder[49] = this._methodDebugInformationTable.Count;
      builder[50] = this._localScopeTable.Count;
      builder[51] = this._localVariableTable.Count;
      builder[52] = this._localConstantTable.Count;
      builder[54] = this._stateMachineMethodTable.Count;
      builder[53] = this._importScopeTable.Count;
      builder[55] = this._customDebugInformationTable.Count;
      return builder.MoveToImmutable();
    }

    public ModuleDefinitionHandle AddModule(
      int generation,
      StringHandle moduleName,
      GuidHandle mvid,
      GuidHandle encId,
      GuidHandle encBaseId)
    {
      if ((uint) generation > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (generation));
      if (this._moduleRow.HasValue)
        Throw.InvalidOperation(SR.ModuleAlreadyAdded);
      this._moduleRow = new MetadataBuilder.ModuleRow?(new MetadataBuilder.ModuleRow()
      {
        Generation = (ushort) generation,
        Name = moduleName,
        ModuleVersionId = mvid,
        EncId = encId,
        EncBaseId = encBaseId
      });
      return EntityHandle.ModuleDefinition;
    }

    public AssemblyDefinitionHandle AddAssembly(
      StringHandle name,
      Version version,
      StringHandle culture,
      BlobHandle publicKey,
      AssemblyFlags flags,
      AssemblyHashAlgorithm hashAlgorithm)
    {
      if ((object) version == null)
        Throw.ArgumentNull(nameof (version));
      if (this._assemblyRow.HasValue)
        Throw.InvalidOperation(SR.AssemblyAlreadyAdded);
      this._assemblyRow = new MetadataBuilder.AssemblyRow?(new MetadataBuilder.AssemblyRow()
      {
        Flags = (ushort) flags,
        HashAlgorithm = (uint) hashAlgorithm,
        Version = version,
        AssemblyKey = publicKey,
        AssemblyName = name,
        AssemblyCulture = culture
      });
      return EntityHandle.AssemblyDefinition;
    }

    public AssemblyReferenceHandle AddAssemblyReference(
      StringHandle name,
      Version version,
      StringHandle culture,
      BlobHandle publicKeyOrToken,
      AssemblyFlags flags,
      BlobHandle hashValue)
    {
      if ((object) version == null)
        Throw.ArgumentNull(nameof (version));
      this._assemblyRefTable.Add(new MetadataBuilder.AssemblyRefTableRow()
      {
        Name = name,
        Version = version,
        Culture = culture,
        PublicKeyToken = publicKeyOrToken,
        Flags = (uint) flags,
        HashValue = hashValue
      });
      return AssemblyReferenceHandle.FromRowId(this._assemblyRefTable.Count);
    }

    /// <summary>Adds a type definition.</summary>
    /// <param name="attributes">Attributes</param>
    /// <param name="namespace">Namespace</param>
    /// <param name="name">Type name</param>
    /// <param name="baseType"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />, <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" /> or nil.</param>
    /// <param name="fieldList">
    /// If the type declares fields the handle of the first one, otherwise the handle of the first field declared by the next type definition.
    /// If no type defines any fields in the module, <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.FieldDefinitionHandle(System.Int32)" />(1).
    /// </param>
    /// <param name="methodList">
    /// If the type declares methods the handle of the first one, otherwise the handle of the first method declared by the next type definition.
    /// If no type defines any methods in the module, <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(System.Int32)" />(1).
    /// </param>
    /// <exception cref="T:System.ArgumentException"><paramref name="baseType" /> doesn't have the expected handle kind.</exception>
    public TypeDefinitionHandle AddTypeDefinition(
      TypeAttributes attributes,
      StringHandle @namespace,
      StringHandle name,
      EntityHandle baseType,
      FieldDefinitionHandle fieldList,
      MethodDefinitionHandle methodList)
    {
      this._typeDefTable.Add(new MetadataBuilder.TypeDefRow()
      {
        Flags = (uint) attributes,
        Name = name,
        Namespace = @namespace,
        Extends = baseType.IsNil ? 0 : CodedIndex.TypeDefOrRefOrSpec(baseType),
        FieldList = fieldList.RowId,
        MethodList = methodList.RowId
      });
      return TypeDefinitionHandle.FromRowId(this._typeDefTable.Count);
    }

    /// <summary>Defines a type layout of a type definition.</summary>
    /// <param name="type">Type definition.</param>
    /// <param name="packingSize">
    /// Specifies that fields should be placed within the type instance at byte addresses which are a multiple of the value,
    /// or at natural alignment for that field type, whichever is smaller. Shall be one of the following: 0, 1, 2, 4, 8, 16, 32, 64, or 128.
    /// A value of zero indicates that the packing size used should match the default for the current platform.
    /// </param>
    /// <param name="size">
    /// Indicates a minimum size of the type instance, and is intended to allow for padding.
    /// The amount of memory allocated is the maximum of the size calculated from the layout and <paramref name="size" />.
    /// Note that if this directive applies to a value type, then the size shall be less than 1 MB.
    /// </param>
    /// <remarks>
    /// Entires must be added in the same order as the corresponding type definitions.
    /// </remarks>
    public void AddTypeLayout(TypeDefinitionHandle type, ushort packingSize, uint size) => this._classLayoutTable.Add(new MetadataBuilder.ClassLayoutRow()
    {
      Parent = type.RowId,
      PackingSize = packingSize,
      ClassSize = size
    });

    /// <summary>Adds an interface implementation to a type.</summary>
    /// <param name="type">The type implementing the interface.</param>
    /// <param name="implementedInterface">
    /// The interface being implemented:
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />.
    /// </param>
    /// <remarks>
    /// Interface implementations must be added in the same order as the corresponding type definitions implementing the interface.
    /// If a type implements multiple interfaces the corresponding entries must be added in the order determined by their coded indices (<see cref="M:System.Reflection.Metadata.Ecma335.CodedIndex.TypeDefOrRefOrSpec(System.Reflection.Metadata.EntityHandle)" />).
    /// </remarks>
    /// <exception cref="T:System.ArgumentException"><paramref name="implementedInterface" /> doesn't have the expected handle kind.</exception>
    public InterfaceImplementationHandle AddInterfaceImplementation(
      TypeDefinitionHandle type,
      EntityHandle implementedInterface)
    {
      this._interfaceImplTable.Add(new MetadataBuilder.InterfaceImplRow()
      {
        Class = type.RowId,
        Interface = CodedIndex.TypeDefOrRefOrSpec(implementedInterface)
      });
      return InterfaceImplementationHandle.FromRowId(this._interfaceImplTable.Count);
    }

    /// <summary>
    /// Defines a nesting relationship to specified type definitions.
    /// </summary>
    /// <param name="type">The nested type definition handle.</param>
    /// <param name="enclosingType">The enclosing type definition handle.</param>
    /// <remarks>
    /// Entries must be added in the same order as the corresponding nested type definitions.
    /// </remarks>
    public void AddNestedType(TypeDefinitionHandle type, TypeDefinitionHandle enclosingType) => this._nestedClassTable.Add(new MetadataBuilder.NestedClassRow()
    {
      NestedClass = type.RowId,
      EnclosingClass = enclosingType.RowId
    });

    /// <summary>Add a type reference.</summary>
    /// <param name="resolutionScope">
    /// The entity declaring the target type:
    /// <see cref="T:System.Reflection.Metadata.ModuleDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />, <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />, or nil.
    /// </param>
    /// <param name="namespace">Namespace.</param>
    /// <param name="name">Type name.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="resolutionScope" /> doesn't have the expected handle kind.</exception>
    public TypeReferenceHandle AddTypeReference(
      EntityHandle resolutionScope,
      StringHandle @namespace,
      StringHandle name)
    {
      this._typeRefTable.Add(new MetadataBuilder.TypeRefRow()
      {
        ResolutionScope = resolutionScope.IsNil ? 0 : CodedIndex.ResolutionScope(resolutionScope),
        Name = name,
        Namespace = @namespace
      });
      return TypeReferenceHandle.FromRowId(this._typeRefTable.Count);
    }

    public TypeSpecificationHandle AddTypeSpecification(BlobHandle signature)
    {
      this._typeSpecTable.Add(new MetadataBuilder.TypeSpecRow()
      {
        Signature = signature
      });
      return TypeSpecificationHandle.FromRowId(this._typeSpecTable.Count);
    }

    public StandaloneSignatureHandle AddStandaloneSignature(BlobHandle signature)
    {
      this._standAloneSigTable.Add(new MetadataBuilder.StandaloneSigRow()
      {
        Signature = signature
      });
      return StandaloneSignatureHandle.FromRowId(this._standAloneSigTable.Count);
    }

    /// <summary>Adds a property definition.</summary>
    /// <param name="attributes">Attributes</param>
    /// <param name="name">Name</param>
    /// <param name="signature">Signature of the property.</param>
    public PropertyDefinitionHandle AddProperty(
      PropertyAttributes attributes,
      StringHandle name,
      BlobHandle signature)
    {
      this._propertyTable.Add(new MetadataBuilder.PropertyRow()
      {
        PropFlags = (ushort) attributes,
        Name = name,
        Type = signature
      });
      return PropertyDefinitionHandle.FromRowId(this._propertyTable.Count);
    }

    public void AddPropertyMap(
      TypeDefinitionHandle declaringType,
      PropertyDefinitionHandle propertyList)
    {
      this._propertyMapTable.Add(new MetadataBuilder.PropertyMapRow()
      {
        Parent = declaringType.RowId,
        PropertyList = propertyList.RowId
      });
    }

    /// <summary>Adds an event definition.</summary>
    /// <param name="attributes">Attributes</param>
    /// <param name="name">Name</param>
    /// <param name="type">Type of the event: <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />, or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" /></param>
    /// <exception cref="T:System.ArgumentException"><paramref name="type" /> doesn't have the expected handle kind.</exception>
    public EventDefinitionHandle AddEvent(
      EventAttributes attributes,
      StringHandle name,
      EntityHandle type)
    {
      this._eventTable.Add(new MetadataBuilder.EventRow()
      {
        EventFlags = (ushort) attributes,
        Name = name,
        EventType = CodedIndex.TypeDefOrRefOrSpec(type)
      });
      return EventDefinitionHandle.FromRowId(this._eventTable.Count);
    }

    public void AddEventMap(TypeDefinitionHandle declaringType, EventDefinitionHandle eventList) => this._eventMapTable.Add(new MetadataBuilder.EventMapRow()
    {
      Parent = declaringType.RowId,
      EventList = eventList.RowId
    });

    /// <summary>
    /// Adds a default value for a parameter, field or property.
    /// </summary>
    /// <param name="parent"><see cref="T:System.Reflection.Metadata.ParameterHandle" />, <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" />, or <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" /></param>
    /// <param name="value">The constant value.</param>
    /// <remarks>
    /// Entries may be added in any order. The table is automatically sorted when serialized.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    public ConstantHandle AddConstant(EntityHandle parent, object? value)
    {
      int num = CodedIndex.HasConstant(parent);
      this._constantTableNeedsSorting |= num < this._constantTableLastParent;
      this._constantTableLastParent = num;
      this._constantTable.Add(new MetadataBuilder.ConstantRow()
      {
        Type = (byte) MetadataWriterUtilities.GetConstantTypeCode(value),
        Parent = num,
        Value = this.GetOrAddConstantBlob(value)
      });
      return ConstantHandle.FromRowId(this._constantTable.Count);
    }

    /// <summary>
    /// Associates a method (a getter, a setter, an adder, etc.) with a property or an event.
    /// </summary>
    /// <param name="association"><see cref="T:System.Reflection.Metadata.EventDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" />.</param>
    /// <param name="semantics">Semantics.</param>
    /// <param name="methodDefinition">Method definition.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="association" /> doesn't have the expected handle kind.</exception>
    /// <remarks>
    /// Entries may be added in any order. The table is automatically sorted when serialized.
    /// </remarks>
    public void AddMethodSemantics(
      EntityHandle association,
      MethodSemanticsAttributes semantics,
      MethodDefinitionHandle methodDefinition)
    {
      int num = CodedIndex.HasSemantics(association);
      this._methodSemanticsTableNeedsSorting |= num < this._methodSemanticsTableLastAssociation;
      this._methodSemanticsTableLastAssociation = num;
      this._methodSemanticsTable.Add(new MetadataBuilder.MethodSemanticsRow()
      {
        Association = num,
        Method = methodDefinition.RowId,
        Semantic = (ushort) semantics
      });
    }

    /// <summary>Add a custom attribute.</summary>
    /// <param name="parent">
    /// An entity to attach the custom attribute to:
    /// <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ParameterHandle" />,
    /// <see cref="T:System.Reflection.Metadata.InterfaceImplementationHandle" />,
    /// <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ModuleDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.DeclarativeSecurityAttributeHandle" />,
    /// <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.EventDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.StandaloneSignatureHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />,
    /// <see cref="T:System.Reflection.Metadata.AssemblyDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.AssemblyFileHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ExportedTypeHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ManifestResourceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.GenericParameterHandle" />,
    /// <see cref="T:System.Reflection.Metadata.GenericParameterConstraintHandle" /> or
    /// <see cref="T:System.Reflection.Metadata.MethodSpecificationHandle" />.
    /// </param>
    /// <param name="constructor">
    /// Custom attribute constructor: <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" />
    /// </param>
    /// <param name="value">Custom attribute value blob.</param>
    /// <remarks>
    /// Entries may be added in any order. The table is automatically sorted when serialized.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    public CustomAttributeHandle AddCustomAttribute(
      EntityHandle parent,
      EntityHandle constructor,
      BlobHandle value)
    {
      int num = CodedIndex.HasCustomAttribute(parent);
      this._customAttributeTableNeedsSorting |= num < this._customAttributeTableLastParent;
      this._customAttributeTableLastParent = num;
      this._customAttributeTable.Add(new MetadataBuilder.CustomAttributeRow()
      {
        Parent = num,
        Type = CodedIndex.CustomAttributeType(constructor),
        Value = value
      });
      return CustomAttributeHandle.FromRowId(this._customAttributeTable.Count);
    }

    /// <summary>Adds a method specification (instantiation).</summary>
    /// <param name="method">Generic method: <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" /></param>
    /// <param name="instantiation">Instantiation blob encoding the generic arguments of the method.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="method" /> doesn't have the expected handle kind.</exception>
    public MethodSpecificationHandle AddMethodSpecification(
      EntityHandle method,
      BlobHandle instantiation)
    {
      this._methodSpecTable.Add(new MetadataBuilder.MethodSpecRow()
      {
        Method = CodedIndex.MethodDefOrRef(method),
        Instantiation = instantiation
      });
      return MethodSpecificationHandle.FromRowId(this._methodSpecTable.Count);
    }

    public ModuleReferenceHandle AddModuleReference(StringHandle moduleName)
    {
      this._moduleRefTable.Add(new MetadataBuilder.ModuleRefRow()
      {
        Name = moduleName
      });
      return ModuleReferenceHandle.FromRowId(this._moduleRefTable.Count);
    }

    /// <summary>Adds a parameter definition.</summary>
    /// <param name="attributes"><see cref="T:System.Reflection.ParameterAttributes" /></param>
    /// <param name="name">Parameter name (optional).</param>
    /// <param name="sequenceNumber">Sequence number of the parameter. Value of 0 refers to the owner method's return type; its parameters are then numbered from 1 onwards.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="sequenceNumber" /> is greater than <see cref="F:System.UInt16.MaxValue" />.</exception>
    public ParameterHandle AddParameter(
      ParameterAttributes attributes,
      StringHandle name,
      int sequenceNumber)
    {
      if ((uint) sequenceNumber > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (sequenceNumber));
      this._paramTable.Add(new MetadataBuilder.ParamRow()
      {
        Flags = (ushort) attributes,
        Name = name,
        Sequence = (ushort) sequenceNumber
      });
      return ParameterHandle.FromRowId(this._paramTable.Count);
    }

    /// <summary>Adds a generic parameter definition.</summary>
    /// <param name="parent">Parent entity handle: <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /></param>
    /// <param name="attributes">Attributes.</param>
    /// <param name="name">Parameter name.</param>
    /// <param name="index">Zero-based parameter index.</param>
    /// <remarks>
    /// Generic parameters must be added in an order determined by the coded index of their parent entity (<see cref="M:System.Reflection.Metadata.Ecma335.CodedIndex.TypeOrMethodDef(System.Reflection.Metadata.EntityHandle)" />).
    /// Generic parameters with the same parent must be ordered by their <paramref name="index" />.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is greater than <see cref="F:System.UInt16.MaxValue" />.</exception>
    public GenericParameterHandle AddGenericParameter(
      EntityHandle parent,
      GenericParameterAttributes attributes,
      StringHandle name,
      int index)
    {
      if ((uint) index > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (index));
      this._genericParamTable.Add(new MetadataBuilder.GenericParamRow()
      {
        Flags = (ushort) attributes,
        Name = name,
        Number = (ushort) index,
        Owner = CodedIndex.TypeOrMethodDef(parent)
      });
      return GenericParameterHandle.FromRowId(this._genericParamTable.Count);
    }

    /// <summary>Adds a type constraint to a generic parameter.</summary>
    /// <param name="genericParameter">Generic parameter to constrain.</param>
    /// <param name="constraint">Type constraint: <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" /></param>
    /// <exception cref="T:System.ArgumentException"><paramref name="genericParameter" /> doesn't have the expected handle kind.</exception>
    /// <remarks>
    /// Constraints must be added in the same order as the corresponding generic parameters.
    /// </remarks>
    public GenericParameterConstraintHandle AddGenericParameterConstraint(
      GenericParameterHandle genericParameter,
      EntityHandle constraint)
    {
      this._genericParamConstraintTable.Add(new MetadataBuilder.GenericParamConstraintRow()
      {
        Owner = genericParameter.RowId,
        Constraint = CodedIndex.TypeDefOrRefOrSpec(constraint)
      });
      return GenericParameterConstraintHandle.FromRowId(this._genericParamConstraintTable.Count);
    }

    /// <summary>Adds a field definition.</summary>
    /// <param name="attributes">Field attributes.</param>
    /// <param name="name">Field name.</param>
    /// <param name="signature">Field signature. Use <see cref="M:System.Reflection.Metadata.Ecma335.BlobEncoder.FieldSignature" /> to construct the blob.</param>
    public FieldDefinitionHandle AddFieldDefinition(
      FieldAttributes attributes,
      StringHandle name,
      BlobHandle signature)
    {
      this._fieldTable.Add(new MetadataBuilder.FieldDefRow()
      {
        Flags = (ushort) attributes,
        Name = name,
        Signature = signature
      });
      return FieldDefinitionHandle.FromRowId(this._fieldTable.Count);
    }

    /// <summary>Defines a field layout of a field definition.</summary>
    /// <param name="field">Field definition.</param>
    /// <param name="offset">The byte offset of the field within the declaring type instance.</param>
    /// <remarks>
    /// Entires must be added in the same order as the corresponding field definitions.
    /// </remarks>
    public void AddFieldLayout(FieldDefinitionHandle field, int offset) => this._fieldLayoutTable.Add(new MetadataBuilder.FieldLayoutRow()
    {
      Field = field.RowId,
      Offset = offset
    });

    /// <summary>
    /// Add marshalling information to a field or a parameter.
    /// </summary>
    /// <param name="parent"><see cref="T:System.Reflection.Metadata.ParameterHandle" /> or <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" />.</param>
    /// <param name="descriptor">Descriptor blob.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    /// <remarks>
    /// Entries may be added in any order. The table is automatically sorted when serialized.
    /// </remarks>
    public void AddMarshallingDescriptor(EntityHandle parent, BlobHandle descriptor)
    {
      int num = CodedIndex.HasFieldMarshal(parent);
      this._fieldMarshalTableNeedsSorting |= num < this._fieldMarshalTableLastParent;
      this._fieldMarshalTableLastParent = num;
      this._fieldMarshalTable.Add(new MetadataBuilder.FieldMarshalRow()
      {
        Parent = num,
        NativeType = descriptor
      });
    }

    /// <summary>
    /// Adds a mapping from a field to its initial value stored in the PE image.
    /// </summary>
    /// <param name="field">Field definition handle.</param>
    /// <param name="offset">
    /// Offset within the block in the PE image that stores initial values of mapped fields (usually in .text section).
    /// The final relative virtual address stored in the metadata is calculated when the metadata is serialized
    /// by adding the offset to the virtual address of the block start.
    /// </param>
    /// <remarks>
    /// Entires must be added in the same order as the corresponding field definitions.
    /// </remarks>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="offset" /> is negative.</exception>
    public void AddFieldRelativeVirtualAddress(FieldDefinitionHandle field, int offset)
    {
      if (offset < 0)
        Throw.ArgumentOutOfRange(nameof (offset));
      this._fieldRvaTable.Add(new MetadataBuilder.FieldRvaRow()
      {
        Field = field.RowId,
        Offset = offset
      });
    }

    /// <summary>Adds a method definition.</summary>
    /// <param name="attributes"><see cref="T:System.Reflection.MethodAttributes" /></param>
    /// <param name="implAttributes"><see cref="T:System.Reflection.MethodImplAttributes" /></param>
    /// <param name="name">Method name/</param>
    /// <param name="signature">Method signature.</param>
    /// <param name="bodyOffset">
    /// Offset within the block in the PE image that stores method bodies (IL stream),
    /// or -1 if the method doesn't have a body.
    /// 
    /// The final relative virtual address stored in the metadata is calculated when the metadata is serialized
    /// by adding the offset to the virtual address of the beginning of the block.
    /// </param>
    /// <param name="parameterList">
    /// If the method declares parameters in Params table the handle of the first one, otherwise the handle of the first parameter declared by the next method definition.
    /// If no parameters are declared in the module, <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.ParameterHandle(System.Int32)" />(1).
    /// </param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="bodyOffset" /> is less than -1.</exception>
    public MethodDefinitionHandle AddMethodDefinition(
      MethodAttributes attributes,
      MethodImplAttributes implAttributes,
      StringHandle name,
      BlobHandle signature,
      int bodyOffset,
      ParameterHandle parameterList)
    {
      if (bodyOffset < -1)
        Throw.ArgumentOutOfRange(nameof (bodyOffset));
      this._methodDefTable.Add(new MetadataBuilder.MethodRow()
      {
        Flags = (ushort) attributes,
        ImplFlags = (ushort) implAttributes,
        Name = name,
        Signature = signature,
        BodyOffset = bodyOffset,
        ParamList = parameterList.RowId
      });
      return MethodDefinitionHandle.FromRowId(this._methodDefTable.Count);
    }

    /// <summary>
    /// Adds import information to a method definition (P/Invoke).
    /// </summary>
    /// <param name="method">Method definition handle.</param>
    /// <param name="attributes">Attributes.</param>
    /// <param name="name">Unmanaged method name.</param>
    /// <param name="module">Module containing the unmanaged method.</param>
    /// <remarks>
    /// Method imports must be added in the same order as the corresponding method definitions.
    /// </remarks>
    public void AddMethodImport(
      MethodDefinitionHandle method,
      MethodImportAttributes attributes,
      StringHandle name,
      ModuleReferenceHandle module)
    {
      this._implMapTable.Add(new MetadataBuilder.ImplMapRow()
      {
        MemberForwarded = CodedIndex.MemberForwarded((EntityHandle) method),
        ImportName = name,
        ImportScope = module.RowId,
        MappingFlags = (ushort) attributes
      });
    }

    /// <summary>
    /// Defines an implementation for a method declaration within a type.
    /// </summary>
    /// <param name="type">Type</param>
    /// <param name="methodBody"><see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" /> which provides the implementation.</param>
    /// <param name="methodDeclaration"><see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" /> the method being implemented.</param>
    /// <remarks>
    /// Method implementations must be added in the same order as the corresponding type definitions.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException"><paramref name="methodBody" /> or <paramref name="methodDeclaration" /> doesn't have the expected handle kind.</exception>
    public MethodImplementationHandle AddMethodImplementation(
      TypeDefinitionHandle type,
      EntityHandle methodBody,
      EntityHandle methodDeclaration)
    {
      this._methodImplTable.Add(new MetadataBuilder.MethodImplRow()
      {
        Class = type.RowId,
        MethodBody = CodedIndex.MethodDefOrRef(methodBody),
        MethodDecl = CodedIndex.MethodDefOrRef(methodDeclaration)
      });
      return MethodImplementationHandle.FromRowId(this._methodImplTable.Count);
    }

    /// <summary>Adds a MemberRef table row.</summary>
    /// <param name="parent">Containing entity:
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />, or
    /// <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />.
    /// </param>
    /// <param name="name">Member name.</param>
    /// <param name="signature">Member signature.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    public MemberReferenceHandle AddMemberReference(
      EntityHandle parent,
      StringHandle name,
      BlobHandle signature)
    {
      this._memberRefTable.Add(new MetadataBuilder.MemberRefRow()
      {
        Class = CodedIndex.MemberRefParent(parent),
        Name = name,
        Signature = signature
      });
      return MemberReferenceHandle.FromRowId(this._memberRefTable.Count);
    }

    /// <summary>Adds a manifest resource.</summary>
    /// <param name="attributes">Attributes</param>
    /// <param name="name">Resource name</param>
    /// <param name="implementation"><see cref="T:System.Reflection.Metadata.AssemblyFileHandle" />, <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" />, or nil</param>
    /// <param name="offset">Specifies the byte offset within the referenced file at which this resource record begins.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="implementation" /> doesn't have the expected handle kind.</exception>
    public ManifestResourceHandle AddManifestResource(
      ManifestResourceAttributes attributes,
      StringHandle name,
      EntityHandle implementation,
      uint offset)
    {
      this._manifestResourceTable.Add(new MetadataBuilder.ManifestResourceRow()
      {
        Flags = (uint) attributes,
        Name = name,
        Implementation = implementation.IsNil ? 0 : CodedIndex.Implementation(implementation),
        Offset = offset
      });
      return ManifestResourceHandle.FromRowId(this._manifestResourceTable.Count);
    }

    public AssemblyFileHandle AddAssemblyFile(
      StringHandle name,
      BlobHandle hashValue,
      bool containsMetadata)
    {
      this._fileTable.Add(new MetadataBuilder.FileTableRow()
      {
        FileName = name,
        Flags = containsMetadata ? 0U : 1U,
        HashValue = hashValue
      });
      return AssemblyFileHandle.FromRowId(this._fileTable.Count);
    }

    /// <summary>Adds an exported type.</summary>
    /// <param name="attributes">Attributes</param>
    /// <param name="namespace">Namespace</param>
    /// <param name="name">Type name</param>
    /// <param name="implementation"><see cref="T:System.Reflection.Metadata.AssemblyFileHandle" />, <see cref="T:System.Reflection.Metadata.ExportedTypeHandle" /> or <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" /></param>
    /// <param name="typeDefinitionId">Type definition id</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="implementation" /> doesn't have the expected handle kind.</exception>
    public ExportedTypeHandle AddExportedType(
      TypeAttributes attributes,
      StringHandle @namespace,
      StringHandle name,
      EntityHandle implementation,
      int typeDefinitionId)
    {
      this._exportedTypeTable.Add(new MetadataBuilder.ExportedTypeRow()
      {
        Flags = (uint) attributes,
        Implementation = CodedIndex.Implementation(implementation),
        TypeNamespace = @namespace,
        TypeName = name,
        TypeDefId = typeDefinitionId
      });
      return ExportedTypeHandle.FromRowId(this._exportedTypeTable.Count);
    }

    /// <summary>
    /// Adds declarative security attribute to a type, method or an assembly.
    /// </summary>
    /// <param name="parent"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />, or <see cref="T:System.Reflection.Metadata.AssemblyDefinitionHandle" /></param>
    /// <param name="action">Security action</param>
    /// <param name="permissionSet">Permission set blob.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    /// <remarks>
    /// Entries may be added in any order. The table is automatically sorted when serialized.
    /// </remarks>
    public DeclarativeSecurityAttributeHandle AddDeclarativeSecurityAttribute(
      EntityHandle parent,
      DeclarativeSecurityAction action,
      BlobHandle permissionSet)
    {
      int num = CodedIndex.HasDeclSecurity(parent);
      this._declSecurityTableNeedsSorting |= num < this._declSecurityTableLastParent;
      this._declSecurityTableLastParent = num;
      this._declSecurityTable.Add(new MetadataBuilder.DeclSecurityRow()
      {
        Parent = num,
        Action = (ushort) action,
        PermissionSet = permissionSet
      });
      return DeclarativeSecurityAttributeHandle.FromRowId(this._declSecurityTable.Count);
    }

    public void AddEncLogEntry(EntityHandle entity, EditAndContinueOperation code) => this._encLogTable.Add(new MetadataBuilder.EncLogRow()
    {
      Token = entity.Token,
      FuncCode = (byte) code
    });

    public void AddEncMapEntry(EntityHandle entity) => this._encMapTable.Add(new MetadataBuilder.EncMapRow()
    {
      Token = entity.Token
    });

    /// <summary>Add document debug information.</summary>
    /// <param name="name">Document Name blob.</param>
    /// <param name="hashAlgorithm">
    /// GUID of the hash algorithm used to calculate the value of <paramref name="hash" />.
    /// </param>
    /// <param name="hash">The hash of the document content.</param>
    /// <param name="language">GUID of the language.</param>
    /// 
    ///             See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md
    public DocumentHandle AddDocument(
      BlobHandle name,
      GuidHandle hashAlgorithm,
      BlobHandle hash,
      GuidHandle language)
    {
      this._documentTable.Add(new MetadataBuilder.DocumentRow()
      {
        Name = name,
        HashAlgorithm = hashAlgorithm,
        Hash = hash,
        Language = language
      });
      return DocumentHandle.FromRowId(this._documentTable.Count);
    }

    /// <summary>Add method debug information.</summary>
    /// <param name="document">
    /// The handle of a single document containing all sequence points of the method, or nil if the method doesn't have sequence points or spans multiple documents.
    /// </param>
    /// <param name="sequencePoints">
    /// Sequence Points blob, or nil if the method doesn't have sequence points.
    /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#sequence-points-blob.
    /// </param>
    public MethodDebugInformationHandle AddMethodDebugInformation(
      DocumentHandle document,
      BlobHandle sequencePoints)
    {
      this._methodDebugInformationTable.Add(new MetadataBuilder.MethodDebugInformationRow()
      {
        Document = document.RowId,
        SequencePoints = sequencePoints
      });
      return MethodDebugInformationHandle.FromRowId(this._methodDebugInformationTable.Count);
    }

    /// <summary>Add local scope debug information.</summary>
    /// <param name="method">The containing method.</param>
    /// <param name="importScope">Handle of the associated import scope.</param>
    /// <param name="variableList">
    /// If the scope declares variables the handle of the first one, otherwise the handle of the first variable declared by the next scope definition.
    /// If no scope defines any variables, <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.LocalVariableHandle(System.Int32)" />(1).
    /// </param>
    /// <param name="constantList">
    /// If the scope declares constants the handle of the first one, otherwise the handle of the first constant declared by the next scope definition.
    /// If no scope defines any constants, <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.LocalConstantHandle(System.Int32)" />(1).
    /// </param>
    /// <param name="startOffset">Offset of the first instruction covered by the scope.</param>
    /// <param name="length">The length (in bytes) of the scope.</param>
    /// <remarks>
    /// Local scopes should be added in the same order as the corresponding method definition.
    /// Within a method they should be ordered by ascending <paramref name="startOffset" /> and then by descending <paramref name="length" />.
    /// </remarks>
    public LocalScopeHandle AddLocalScope(
      MethodDefinitionHandle method,
      ImportScopeHandle importScope,
      LocalVariableHandle variableList,
      LocalConstantHandle constantList,
      int startOffset,
      int length)
    {
      this._localScopeTable.Add(new MetadataBuilder.LocalScopeRow()
      {
        Method = method.RowId,
        ImportScope = importScope.RowId,
        VariableList = variableList.RowId,
        ConstantList = constantList.RowId,
        StartOffset = startOffset,
        Length = length
      });
      return LocalScopeHandle.FromRowId(this._localScopeTable.Count);
    }

    /// <summary>Add local variable debug information.</summary>
    /// <param name="attributes"><see cref="T:System.Reflection.Metadata.LocalVariableAttributes" /></param>
    /// <param name="index">Local variable index in the local signature (zero-based).</param>
    /// <param name="name">Name of the variable.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="index" /> is greater than <see cref="F:System.UInt16.MaxValue" />.</exception>
    public LocalVariableHandle AddLocalVariable(
      LocalVariableAttributes attributes,
      int index,
      StringHandle name)
    {
      if ((uint) index > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (index));
      this._localVariableTable.Add(new MetadataBuilder.LocalVariableRow()
      {
        Attributes = (ushort) attributes,
        Index = (ushort) index,
        Name = name
      });
      return LocalVariableHandle.FromRowId(this._localVariableTable.Count);
    }

    /// <summary>Add local constant debug information.</summary>
    /// <param name="name">Name of the variable.</param>
    /// <param name="signature">
    /// LocalConstantSig blob, see https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localconstantsig-blob.
    /// </param>
    public LocalConstantHandle AddLocalConstant(StringHandle name, BlobHandle signature)
    {
      this._localConstantTable.Add(new MetadataBuilder.LocalConstantRow()
      {
        Name = name,
        Signature = signature
      });
      return LocalConstantHandle.FromRowId(this._localConstantTable.Count);
    }

    /// <summary>Add local scope debug information.</summary>
    /// <param name="parentScope">Parent scope handle.</param>
    /// <param name="imports">
    /// Imports blob, see https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#imports-blob.
    /// </param>
    public ImportScopeHandle AddImportScope(ImportScopeHandle parentScope, BlobHandle imports)
    {
      this._importScopeTable.Add(new MetadataBuilder.ImportScopeRow()
      {
        Parent = parentScope.RowId,
        Imports = imports
      });
      return ImportScopeHandle.FromRowId(this._importScopeTable.Count);
    }

    /// <summary>Add state machine method debug information.</summary>
    /// <param name="moveNextMethod">Handle of the MoveNext method of the state machine (the compiler-generated method).</param>
    /// <param name="kickoffMethod">Handle of the kickoff method (the user defined iterator/async method)</param>
    /// <remarks>
    /// Entries should be added in the same order as the corresponding MoveNext method definitions.
    /// </remarks>
    public void AddStateMachineMethod(
      MethodDefinitionHandle moveNextMethod,
      MethodDefinitionHandle kickoffMethod)
    {
      this._stateMachineMethodTable.Add(new MetadataBuilder.StateMachineMethodRow()
      {
        MoveNextMethod = moveNextMethod.RowId,
        KickoffMethod = kickoffMethod.RowId
      });
    }

    /// <summary>Add custom debug information.</summary>
    /// <param name="parent">
    /// An entity to attach the debug information to:
    /// <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ParameterHandle" />,
    /// <see cref="T:System.Reflection.Metadata.InterfaceImplementationHandle" />,
    /// <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ModuleDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.DeclarativeSecurityAttributeHandle" />,
    /// <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.EventDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.StandaloneSignatureHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />,
    /// <see cref="T:System.Reflection.Metadata.AssemblyDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.AssemblyFileHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ExportedTypeHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ManifestResourceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.GenericParameterHandle" />,
    /// <see cref="T:System.Reflection.Metadata.GenericParameterConstraintHandle" />,
    /// <see cref="T:System.Reflection.Metadata.MethodSpecificationHandle" />,
    /// <see cref="T:System.Reflection.Metadata.DocumentHandle" />,
    /// <see cref="T:System.Reflection.Metadata.LocalScopeHandle" />,
    /// <see cref="T:System.Reflection.Metadata.LocalVariableHandle" />,
    /// <see cref="T:System.Reflection.Metadata.LocalConstantHandle" /> or
    /// <see cref="T:System.Reflection.Metadata.ImportScopeHandle" />.
    /// </param>
    /// <param name="kind">Information kind. Determines the structure of the <paramref name="value" /> blob.</param>
    /// <param name="value">Custom debug information blob.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="parent" /> doesn't have the expected handle kind.</exception>
    /// <remarks>
    /// Entries may be added in any order. The table is automatically sorted when serialized.
    /// </remarks>
    public CustomDebugInformationHandle AddCustomDebugInformation(
      EntityHandle parent,
      GuidHandle kind,
      BlobHandle value)
    {
      this._customDebugInformationTable.Add(new MetadataBuilder.CustomDebugInformationRow()
      {
        Parent = CodedIndex.HasCustomDebugInformation(parent),
        Kind = kind,
        Value = value
      });
      return CustomDebugInformationHandle.FromRowId(this._customDebugInformationTable.Count);
    }

    internal void ValidateOrder()
    {
      this.ValidateClassLayoutTable();
      this.ValidateFieldLayoutTable();
      this.ValidateFieldRvaTable();
      this.ValidateGenericParamTable();
      this.ValidateGenericParamConstaintTable();
      this.ValidateImplMapTable();
      this.ValidateInterfaceImplTable();
      this.ValidateMethodImplTable();
      this.ValidateNestedClassTable();
      this.ValidateLocalScopeTable();
      this.ValidateStateMachineMethodTable();
    }

    private void ValidateClassLayoutTable()
    {
      for (int index = 1; index < this._classLayoutTable.Count; ++index)
      {
        if (this._classLayoutTable[index - 1].Parent >= this._classLayoutTable[index].Parent)
          Throw.InvalidOperation_TableNotSorted(TableIndex.ClassLayout);
      }
    }

    private void ValidateFieldLayoutTable()
    {
      for (int index = 1; index < this._fieldLayoutTable.Count; ++index)
      {
        if (this._fieldLayoutTable[index - 1].Field >= this._fieldLayoutTable[index].Field)
          Throw.InvalidOperation_TableNotSorted(TableIndex.FieldLayout);
      }
    }

    private void ValidateFieldRvaTable()
    {
      for (int index = 1; index < this._fieldRvaTable.Count; ++index)
      {
        if (this._fieldRvaTable[index - 1].Field >= this._fieldRvaTable[index].Field)
          Throw.InvalidOperation_TableNotSorted(TableIndex.FieldRva);
      }
    }

    private void ValidateGenericParamTable()
    {
      if (this._genericParamTable.Count == 0)
        return;
      MetadataBuilder.GenericParamRow genericParamRow1 = this._genericParamTable[0];
      int index = 1;
      while (index < this._genericParamTable.Count)
      {
        MetadataBuilder.GenericParamRow genericParamRow2 = this._genericParamTable[index];
        if (genericParamRow2.Owner <= genericParamRow1.Owner && (genericParamRow1.Owner != genericParamRow2.Owner || (int) genericParamRow2.Number <= (int) genericParamRow1.Number))
          Throw.InvalidOperation_TableNotSorted(TableIndex.GenericParam);
        ++index;
        genericParamRow1 = genericParamRow2;
      }
    }

    private void ValidateGenericParamConstaintTable()
    {
      for (int index = 1; index < this._genericParamConstraintTable.Count; ++index)
      {
        if (this._genericParamConstraintTable[index - 1].Owner > this._genericParamConstraintTable[index].Owner)
          Throw.InvalidOperation_TableNotSorted(TableIndex.GenericParamConstraint);
      }
    }

    private void ValidateImplMapTable()
    {
      for (int index = 1; index < this._implMapTable.Count; ++index)
      {
        if (this._implMapTable[index - 1].MemberForwarded >= this._implMapTable[index].MemberForwarded)
          Throw.InvalidOperation_TableNotSorted(TableIndex.ImplMap);
      }
    }

    private void ValidateInterfaceImplTable()
    {
      for (int index = 1; index < this._interfaceImplTable.Count; ++index)
      {
        if (this._interfaceImplTable[index - 1].Class > this._interfaceImplTable[index].Class)
          Throw.InvalidOperation_TableNotSorted(TableIndex.InterfaceImpl);
      }
    }

    private void ValidateMethodImplTable()
    {
      for (int index = 1; index < this._methodImplTable.Count; ++index)
      {
        if (this._methodImplTable[index - 1].Class > this._methodImplTable[index].Class)
          Throw.InvalidOperation_TableNotSorted(TableIndex.MethodImpl);
      }
    }

    private void ValidateNestedClassTable()
    {
      for (int index = 1; index < this._nestedClassTable.Count; ++index)
      {
        if (this._nestedClassTable[index - 1].NestedClass >= this._nestedClassTable[index].NestedClass)
          Throw.InvalidOperation_TableNotSorted(TableIndex.NestedClass);
      }
    }

    private void ValidateLocalScopeTable()
    {
      if (this._localScopeTable.Count == 0)
        return;
      MetadataBuilder.LocalScopeRow localScopeRow1 = this._localScopeTable[0];
      int index = 1;
      while (index < this._localScopeTable.Count)
      {
        MetadataBuilder.LocalScopeRow localScopeRow2 = this._localScopeTable[index];
        if (localScopeRow2.Method <= localScopeRow1.Method && (localScopeRow2.Method != localScopeRow1.Method || localScopeRow2.StartOffset <= localScopeRow1.StartOffset && (localScopeRow2.StartOffset != localScopeRow1.StartOffset || localScopeRow1.Length < localScopeRow2.Length)))
          Throw.InvalidOperation_TableNotSorted(TableIndex.LocalScope);
        ++index;
        localScopeRow1 = localScopeRow2;
      }
    }

    private void ValidateStateMachineMethodTable()
    {
      for (int index = 1; index < this._stateMachineMethodTable.Count; ++index)
      {
        if (this._stateMachineMethodTable[index - 1].MoveNextMethod >= this._stateMachineMethodTable[index].MoveNextMethod)
          Throw.InvalidOperation_TableNotSorted(TableIndex.StateMachineMethod);
      }
    }

    internal void SerializeMetadataTables(
      BlobBuilder writer,
      MetadataSizes metadataSizes,
      ImmutableArray<int> stringMap,
      int methodBodyStreamRva,
      int mappedFieldDataStreamRva)
    {
      int count1 = writer.Count;
      MetadataBuilder.SerializeTablesHeader(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.Module))
        this.SerializeModuleTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.TypeRef))
        this.SerializeTypeRefTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.TypeDef))
        this.SerializeTypeDefTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.Field))
        this.SerializeFieldTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.MethodDef))
        this.SerializeMethodDefTable(writer, stringMap, metadataSizes, methodBodyStreamRva);
      if (metadataSizes.IsPresent(TableIndex.Param))
        this.SerializeParamTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.InterfaceImpl))
        this.SerializeInterfaceImplTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.MemberRef))
        this.SerializeMemberRefTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.Constant))
        this.SerializeConstantTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.CustomAttribute))
        this.SerializeCustomAttributeTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.FieldMarshal))
        this.SerializeFieldMarshalTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.DeclSecurity))
        this.SerializeDeclSecurityTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.ClassLayout))
        this.SerializeClassLayoutTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.FieldLayout))
        this.SerializeFieldLayoutTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.StandAloneSig))
        this.SerializeStandAloneSigTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.EventMap))
        this.SerializeEventMapTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.Event))
        this.SerializeEventTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.PropertyMap))
        this.SerializePropertyMapTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.Property))
        this.SerializePropertyTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.MethodSemantics))
        this.SerializeMethodSemanticsTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.MethodImpl))
        this.SerializeMethodImplTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.ModuleRef))
        this.SerializeModuleRefTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.TypeSpec))
        this.SerializeTypeSpecTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.ImplMap))
        this.SerializeImplMapTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.FieldRva))
        this.SerializeFieldRvaTable(writer, metadataSizes, mappedFieldDataStreamRva);
      if (metadataSizes.IsPresent(TableIndex.EncLog))
        this.SerializeEncLogTable(writer);
      if (metadataSizes.IsPresent(TableIndex.EncMap))
        this.SerializeEncMapTable(writer);
      if (metadataSizes.IsPresent(TableIndex.Assembly))
        this.SerializeAssemblyTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.AssemblyRef))
        this.SerializeAssemblyRefTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.File))
        this.SerializeFileTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.ExportedType))
        this.SerializeExportedTypeTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.ManifestResource))
        this.SerializeManifestResourceTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.NestedClass))
        this.SerializeNestedClassTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.GenericParam))
        this.SerializeGenericParamTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.MethodSpec))
        this.SerializeMethodSpecTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.GenericParamConstraint))
        this.SerializeGenericParamConstraintTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.Document))
        this.SerializeDocumentTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.MethodDebugInformation))
        this.SerializeMethodDebugInformationTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.LocalScope))
        this.SerializeLocalScopeTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.LocalVariable))
        this.SerializeLocalVariableTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.LocalConstant))
        this.SerializeLocalConstantTable(writer, stringMap, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.ImportScope))
        this.SerializeImportScopeTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.StateMachineMethod))
        this.SerializeStateMachineMethodTable(writer, metadataSizes);
      if (metadataSizes.IsPresent(TableIndex.CustomDebugInformation))
        this.SerializeCustomDebugInformationTable(writer, metadataSizes);
      writer.WriteByte((byte) 0);
      writer.Align(4);
      int count2 = writer.Count;
    }


    #nullable disable
    private static void SerializeTablesHeader(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      int count1 = writer.Count;
      HeapSizeFlag heapSizeFlag = (HeapSizeFlag) 0;
      if (!metadataSizes.StringReferenceIsSmall)
        heapSizeFlag |= HeapSizeFlag.StringHeapLarge;
      if (!metadataSizes.GuidReferenceIsSmall)
        heapSizeFlag |= HeapSizeFlag.GuidHeapLarge;
      if (!metadataSizes.BlobReferenceIsSmall)
        heapSizeFlag |= HeapSizeFlag.BlobHeapLarge;
      if (metadataSizes.IsEncDelta)
        heapSizeFlag |= HeapSizeFlag.EncDeltas | HeapSizeFlag.DeletedMarks;
      ulong num = metadataSizes.PresentTablesMask & 55169095435288576UL | (metadataSizes.IsStandaloneDebugMetadata ? 0UL : 24190111578624UL);
      writer.WriteUInt32(0U);
      writer.WriteByte((byte) 2);
      writer.WriteByte((byte) 0);
      writer.WriteByte((byte) heapSizeFlag);
      writer.WriteByte((byte) 1);
      writer.WriteUInt64(metadataSizes.PresentTablesMask);
      writer.WriteUInt64(num);
      MetadataWriterUtilities.SerializeRowCounts(writer, metadataSizes.RowCounts);
      int count2 = writer.Count;
    }


    #nullable enable
    internal void SerializeModuleTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      if (!this._moduleRow.HasValue)
        return;
      writer.WriteUInt16(this._moduleRow.Value.Generation);
      writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, this._moduleRow.Value.Name), metadataSizes.StringReferenceIsSmall);
      writer.WriteReference(MetadataBuilder.SerializeHandle(this._moduleRow.Value.ModuleVersionId), metadataSizes.GuidReferenceIsSmall);
      writer.WriteReference(MetadataBuilder.SerializeHandle(this._moduleRow.Value.EncId), metadataSizes.GuidReferenceIsSmall);
      writer.WriteReference(MetadataBuilder.SerializeHandle(this._moduleRow.Value.EncBaseId), metadataSizes.GuidReferenceIsSmall);
    }


    #nullable disable
    private void SerializeEncLogTable(BlobBuilder writer)
    {
      foreach (MetadataBuilder.EncLogRow encLogRow in this._encLogTable)
      {
        writer.WriteInt32(encLogRow.Token);
        writer.WriteUInt32((uint) encLogRow.FuncCode);
      }
    }

    private void SerializeEncMapTable(BlobBuilder writer)
    {
      foreach (MetadataBuilder.EncMapRow encMapRow in this._encMapTable)
        writer.WriteInt32(encMapRow.Token);
    }

    private void SerializeTypeRefTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.TypeRefRow typeRefRow in this._typeRefTable)
      {
        writer.WriteReference(typeRefRow.ResolutionScope, metadataSizes.ResolutionScopeCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, typeRefRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, typeRefRow.Namespace), metadataSizes.StringReferenceIsSmall);
      }
    }

    private void SerializeTypeDefTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.TypeDefRow typeDefRow in this._typeDefTable)
      {
        writer.WriteUInt32(typeDefRow.Flags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, typeDefRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, typeDefRow.Namespace), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(typeDefRow.Extends, metadataSizes.TypeDefOrRefCodedIndexIsSmall);
        writer.WriteReference(typeDefRow.FieldList, metadataSizes.FieldDefReferenceIsSmall);
        writer.WriteReference(typeDefRow.MethodList, metadataSizes.MethodDefReferenceIsSmall);
      }
    }

    private void SerializeFieldTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.FieldDefRow fieldDefRow in this._fieldTable)
      {
        writer.WriteUInt16(fieldDefRow.Flags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, fieldDefRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(fieldDefRow.Signature), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeMethodDefTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes,
      int methodBodyStreamRva)
    {
      foreach (MetadataBuilder.MethodRow methodRow in this._methodDefTable)
      {
        if (methodRow.BodyOffset == -1)
          writer.WriteUInt32(0U);
        else
          writer.WriteInt32(methodBodyStreamRva + methodRow.BodyOffset);
        writer.WriteUInt16(methodRow.ImplFlags);
        writer.WriteUInt16(methodRow.Flags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, methodRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(methodRow.Signature), metadataSizes.BlobReferenceIsSmall);
        writer.WriteReference(methodRow.ParamList, metadataSizes.ParameterReferenceIsSmall);
      }
    }

    private void SerializeParamTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ParamRow paramRow in this._paramTable)
      {
        writer.WriteUInt16(paramRow.Flags);
        writer.WriteUInt16(paramRow.Sequence);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, paramRow.Name), metadataSizes.StringReferenceIsSmall);
      }
    }

    private void SerializeInterfaceImplTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.InterfaceImplRow interfaceImplRow in this._interfaceImplTable)
      {
        writer.WriteReference(interfaceImplRow.Class, metadataSizes.TypeDefReferenceIsSmall);
        writer.WriteReference(interfaceImplRow.Interface, metadataSizes.TypeDefOrRefCodedIndexIsSmall);
      }
    }

    private void SerializeMemberRefTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.MemberRefRow memberRefRow in this._memberRefTable)
      {
        writer.WriteReference(memberRefRow.Class, metadataSizes.MemberRefParentCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, memberRefRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(memberRefRow.Signature), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeConstantTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ConstantRow constantRow in this._constantTableNeedsSorting ? this._constantTable.OrderBy<MetadataBuilder.ConstantRow>((Comparison<MetadataBuilder.ConstantRow>) ((x, y) => x.Parent - y.Parent)) : (IEnumerable<MetadataBuilder.ConstantRow>) this._constantTable)
      {
        writer.WriteByte(constantRow.Type);
        writer.WriteByte((byte) 0);
        writer.WriteReference(constantRow.Parent, metadataSizes.HasConstantCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(constantRow.Value), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeCustomAttributeTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.CustomAttributeRow customAttributeRow in this._customAttributeTableNeedsSorting ? this._customAttributeTable.OrderBy<MetadataBuilder.CustomAttributeRow>((Comparison<MetadataBuilder.CustomAttributeRow>) ((x, y) => x.Parent - y.Parent)) : (IEnumerable<MetadataBuilder.CustomAttributeRow>) this._customAttributeTable)
      {
        writer.WriteReference(customAttributeRow.Parent, metadataSizes.HasCustomAttributeCodedIndexIsSmall);
        writer.WriteReference(customAttributeRow.Type, metadataSizes.CustomAttributeTypeCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(customAttributeRow.Value), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeFieldMarshalTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.FieldMarshalRow fieldMarshalRow in this._fieldMarshalTableNeedsSorting ? this._fieldMarshalTable.OrderBy<MetadataBuilder.FieldMarshalRow>((Comparison<MetadataBuilder.FieldMarshalRow>) ((x, y) => x.Parent - y.Parent)) : (IEnumerable<MetadataBuilder.FieldMarshalRow>) this._fieldMarshalTable)
      {
        writer.WriteReference(fieldMarshalRow.Parent, metadataSizes.HasFieldMarshalCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(fieldMarshalRow.NativeType), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeDeclSecurityTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.DeclSecurityRow declSecurityRow in this._declSecurityTableNeedsSorting ? this._declSecurityTable.OrderBy<MetadataBuilder.DeclSecurityRow>((Comparison<MetadataBuilder.DeclSecurityRow>) ((x, y) => x.Parent - y.Parent)) : (IEnumerable<MetadataBuilder.DeclSecurityRow>) this._declSecurityTable)
      {
        writer.WriteUInt16(declSecurityRow.Action);
        writer.WriteReference(declSecurityRow.Parent, metadataSizes.DeclSecurityCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(declSecurityRow.PermissionSet), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeClassLayoutTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ClassLayoutRow classLayoutRow in this._classLayoutTable)
      {
        writer.WriteUInt16(classLayoutRow.PackingSize);
        writer.WriteUInt32(classLayoutRow.ClassSize);
        writer.WriteReference(classLayoutRow.Parent, metadataSizes.TypeDefReferenceIsSmall);
      }
    }

    private void SerializeFieldLayoutTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.FieldLayoutRow fieldLayoutRow in this._fieldLayoutTable)
      {
        writer.WriteInt32(fieldLayoutRow.Offset);
        writer.WriteReference(fieldLayoutRow.Field, metadataSizes.FieldDefReferenceIsSmall);
      }
    }

    private void SerializeStandAloneSigTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.StandaloneSigRow standaloneSigRow in this._standAloneSigTable)
        writer.WriteReference(MetadataBuilder.SerializeHandle(standaloneSigRow.Signature), metadataSizes.BlobReferenceIsSmall);
    }

    private void SerializeEventMapTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.EventMapRow eventMapRow in this._eventMapTable)
      {
        writer.WriteReference(eventMapRow.Parent, metadataSizes.TypeDefReferenceIsSmall);
        writer.WriteReference(eventMapRow.EventList, metadataSizes.EventDefReferenceIsSmall);
      }
    }

    private void SerializeEventTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.EventRow eventRow in this._eventTable)
      {
        writer.WriteUInt16(eventRow.EventFlags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, eventRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(eventRow.EventType, metadataSizes.TypeDefOrRefCodedIndexIsSmall);
      }
    }

    private void SerializePropertyMapTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.PropertyMapRow propertyMapRow in this._propertyMapTable)
      {
        writer.WriteReference(propertyMapRow.Parent, metadataSizes.TypeDefReferenceIsSmall);
        writer.WriteReference(propertyMapRow.PropertyList, metadataSizes.PropertyDefReferenceIsSmall);
      }
    }

    private void SerializePropertyTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.PropertyRow propertyRow in this._propertyTable)
      {
        writer.WriteUInt16(propertyRow.PropFlags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, propertyRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(propertyRow.Type), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeMethodSemanticsTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.MethodSemanticsRow methodSemanticsRow in this._methodSemanticsTableNeedsSorting ? this._methodSemanticsTable.OrderBy<MetadataBuilder.MethodSemanticsRow>((Comparison<MetadataBuilder.MethodSemanticsRow>) ((x, y) => x.Association - y.Association)) : (IEnumerable<MetadataBuilder.MethodSemanticsRow>) this._methodSemanticsTable)
      {
        writer.WriteUInt16(methodSemanticsRow.Semantic);
        writer.WriteReference(methodSemanticsRow.Method, metadataSizes.MethodDefReferenceIsSmall);
        writer.WriteReference(methodSemanticsRow.Association, metadataSizes.HasSemanticsCodedIndexIsSmall);
      }
    }

    private void SerializeMethodImplTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.MethodImplRow methodImplRow in this._methodImplTable)
      {
        writer.WriteReference(methodImplRow.Class, metadataSizes.TypeDefReferenceIsSmall);
        writer.WriteReference(methodImplRow.MethodBody, metadataSizes.MethodDefOrRefCodedIndexIsSmall);
        writer.WriteReference(methodImplRow.MethodDecl, metadataSizes.MethodDefOrRefCodedIndexIsSmall);
      }
    }

    private void SerializeModuleRefTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ModuleRefRow moduleRefRow in this._moduleRefTable)
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, moduleRefRow.Name), metadataSizes.StringReferenceIsSmall);
    }

    private void SerializeTypeSpecTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.TypeSpecRow typeSpecRow in this._typeSpecTable)
        writer.WriteReference(MetadataBuilder.SerializeHandle(typeSpecRow.Signature), metadataSizes.BlobReferenceIsSmall);
    }

    private void SerializeImplMapTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ImplMapRow implMapRow in this._implMapTable)
      {
        writer.WriteUInt16(implMapRow.MappingFlags);
        writer.WriteReference(implMapRow.MemberForwarded, metadataSizes.MemberForwardedCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, implMapRow.ImportName), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(implMapRow.ImportScope, metadataSizes.ModuleRefReferenceIsSmall);
      }
    }

    private void SerializeFieldRvaTable(
      BlobBuilder writer,
      MetadataSizes metadataSizes,
      int mappedFieldDataStreamRva)
    {
      foreach (MetadataBuilder.FieldRvaRow fieldRvaRow in this._fieldRvaTable)
      {
        writer.WriteInt32(mappedFieldDataStreamRva + fieldRvaRow.Offset);
        writer.WriteReference(fieldRvaRow.Field, metadataSizes.FieldDefReferenceIsSmall);
      }
    }

    private void SerializeAssemblyTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      if (!this._assemblyRow.HasValue)
        return;
      Version version = this._assemblyRow.Value.Version;
      writer.WriteUInt32(this._assemblyRow.Value.HashAlgorithm);
      writer.WriteUInt16((ushort) version.Major);
      writer.WriteUInt16((ushort) version.Minor);
      writer.WriteUInt16((ushort) version.Build);
      writer.WriteUInt16((ushort) version.Revision);
      writer.WriteUInt32((uint) this._assemblyRow.Value.Flags);
      writer.WriteReference(MetadataBuilder.SerializeHandle(this._assemblyRow.Value.AssemblyKey), metadataSizes.BlobReferenceIsSmall);
      writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, this._assemblyRow.Value.AssemblyName), metadataSizes.StringReferenceIsSmall);
      writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, this._assemblyRow.Value.AssemblyCulture), metadataSizes.StringReferenceIsSmall);
    }

    private void SerializeAssemblyRefTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.AssemblyRefTableRow assemblyRefTableRow in this._assemblyRefTable)
      {
        writer.WriteUInt16((ushort) assemblyRefTableRow.Version.Major);
        writer.WriteUInt16((ushort) assemblyRefTableRow.Version.Minor);
        writer.WriteUInt16((ushort) assemblyRefTableRow.Version.Build);
        writer.WriteUInt16((ushort) assemblyRefTableRow.Version.Revision);
        writer.WriteUInt32(assemblyRefTableRow.Flags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(assemblyRefTableRow.PublicKeyToken), metadataSizes.BlobReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, assemblyRefTableRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, assemblyRefTableRow.Culture), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(assemblyRefTableRow.HashValue), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeFileTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.FileTableRow fileTableRow in this._fileTable)
      {
        writer.WriteUInt32(fileTableRow.Flags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, fileTableRow.FileName), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(fileTableRow.HashValue), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeExportedTypeTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ExportedTypeRow exportedTypeRow in this._exportedTypeTable)
      {
        writer.WriteUInt32(exportedTypeRow.Flags);
        writer.WriteInt32(exportedTypeRow.TypeDefId);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, exportedTypeRow.TypeName), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, exportedTypeRow.TypeNamespace), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(exportedTypeRow.Implementation, metadataSizes.ImplementationCodedIndexIsSmall);
      }
    }

    private void SerializeManifestResourceTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ManifestResourceRow manifestResourceRow in this._manifestResourceTable)
      {
        writer.WriteUInt32(manifestResourceRow.Offset);
        writer.WriteUInt32(manifestResourceRow.Flags);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, manifestResourceRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(manifestResourceRow.Implementation, metadataSizes.ImplementationCodedIndexIsSmall);
      }
    }

    private void SerializeNestedClassTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.NestedClassRow nestedClassRow in this._nestedClassTable)
      {
        writer.WriteReference(nestedClassRow.NestedClass, metadataSizes.TypeDefReferenceIsSmall);
        writer.WriteReference(nestedClassRow.EnclosingClass, metadataSizes.TypeDefReferenceIsSmall);
      }
    }

    private void SerializeGenericParamTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.GenericParamRow genericParamRow in this._genericParamTable)
      {
        writer.WriteUInt16(genericParamRow.Number);
        writer.WriteUInt16(genericParamRow.Flags);
        writer.WriteReference(genericParamRow.Owner, metadataSizes.TypeOrMethodDefCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, genericParamRow.Name), metadataSizes.StringReferenceIsSmall);
      }
    }

    private void SerializeGenericParamConstraintTable(
      BlobBuilder writer,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.GenericParamConstraintRow paramConstraintRow in this._genericParamConstraintTable)
      {
        writer.WriteReference(paramConstraintRow.Owner, metadataSizes.GenericParamReferenceIsSmall);
        writer.WriteReference(paramConstraintRow.Constraint, metadataSizes.TypeDefOrRefCodedIndexIsSmall);
      }
    }

    private void SerializeMethodSpecTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.MethodSpecRow methodSpecRow in this._methodSpecTable)
      {
        writer.WriteReference(methodSpecRow.Method, metadataSizes.MethodDefOrRefCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(methodSpecRow.Instantiation), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeDocumentTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.DocumentRow documentRow in this._documentTable)
      {
        writer.WriteReference(MetadataBuilder.SerializeHandle(documentRow.Name), metadataSizes.BlobReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(documentRow.HashAlgorithm), metadataSizes.GuidReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(documentRow.Hash), metadataSizes.BlobReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(documentRow.Language), metadataSizes.GuidReferenceIsSmall);
      }
    }

    private void SerializeMethodDebugInformationTable(
      BlobBuilder writer,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.MethodDebugInformationRow debugInformationRow in this._methodDebugInformationTable)
      {
        writer.WriteReference(debugInformationRow.Document, metadataSizes.DocumentReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(debugInformationRow.SequencePoints), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeLocalScopeTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.LocalScopeRow localScopeRow in this._localScopeTable)
      {
        writer.WriteReference(localScopeRow.Method, metadataSizes.MethodDefReferenceIsSmall);
        writer.WriteReference(localScopeRow.ImportScope, metadataSizes.ImportScopeReferenceIsSmall);
        writer.WriteReference(localScopeRow.VariableList, metadataSizes.LocalVariableReferenceIsSmall);
        writer.WriteReference(localScopeRow.ConstantList, metadataSizes.LocalConstantReferenceIsSmall);
        writer.WriteInt32(localScopeRow.StartOffset);
        writer.WriteInt32(localScopeRow.Length);
      }
    }

    private void SerializeLocalVariableTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.LocalVariableRow localVariableRow in this._localVariableTable)
      {
        writer.WriteUInt16(localVariableRow.Attributes);
        writer.WriteUInt16(localVariableRow.Index);
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, localVariableRow.Name), metadataSizes.StringReferenceIsSmall);
      }
    }

    private void SerializeLocalConstantTable(
      BlobBuilder writer,
      ImmutableArray<int> stringMap,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.LocalConstantRow localConstantRow in this._localConstantTable)
      {
        writer.WriteReference(MetadataBuilder.SerializeHandle(stringMap, localConstantRow.Name), metadataSizes.StringReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(localConstantRow.Signature), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeImportScopeTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.ImportScopeRow importScopeRow in this._importScopeTable)
      {
        writer.WriteReference(importScopeRow.Parent, metadataSizes.ImportScopeReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(importScopeRow.Imports), metadataSizes.BlobReferenceIsSmall);
      }
    }

    private void SerializeStateMachineMethodTable(BlobBuilder writer, MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.StateMachineMethodRow machineMethodRow in this._stateMachineMethodTable)
      {
        writer.WriteReference(machineMethodRow.MoveNextMethod, metadataSizes.MethodDefReferenceIsSmall);
        writer.WriteReference(machineMethodRow.KickoffMethod, metadataSizes.MethodDefReferenceIsSmall);
      }
    }

    private void SerializeCustomDebugInformationTable(
      BlobBuilder writer,
      MetadataSizes metadataSizes)
    {
      foreach (MetadataBuilder.CustomDebugInformationRow debugInformationRow in this._customDebugInformationTable.OrderBy<MetadataBuilder.CustomDebugInformationRow>((Comparison<MetadataBuilder.CustomDebugInformationRow>) ((x, y) =>
      {
        int num = x.Parent - y.Parent;
        return num == 0 ? x.Kind.Index - y.Kind.Index : num;
      })))
      {
        writer.WriteReference(debugInformationRow.Parent, metadataSizes.HasCustomDebugInformationCodedIndexIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(debugInformationRow.Kind), metadataSizes.GuidReferenceIsSmall);
        writer.WriteReference(MetadataBuilder.SerializeHandle(debugInformationRow.Value), metadataSizes.BlobReferenceIsSmall);
      }
    }

    /// <summary>Creates a builder for metadata tables and heaps.</summary>
    /// <param name="userStringHeapStartOffset">
    /// Start offset of the User String heap.
    /// The cumulative size of User String heaps of all previous EnC generations. Should be 0 unless the metadata is EnC delta metadata.
    /// </param>
    /// <param name="stringHeapStartOffset">
    /// Start offset of the String heap.
    /// The cumulative size of String heaps of all previous EnC generations. Should be 0 unless the metadata is EnC delta metadata.
    /// </param>
    /// <param name="blobHeapStartOffset">
    /// Start offset of the Blob heap.
    /// The cumulative size of Blob heaps of all previous EnC generations. Should be 0 unless the metadata is EnC delta metadata.
    /// </param>
    /// <param name="guidHeapStartOffset">
    /// Start offset of the Guid heap.
    /// The cumulative size of Guid heaps of all previous EnC generations. Should be 0 unless the metadata is EnC delta metadata.
    /// </param>
    /// <exception cref="T:System.Reflection.Metadata.ImageFormatLimitationException">Offset is too big.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Offset is negative.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="guidHeapStartOffset" /> is not a multiple of size of GUID.</exception>
    public MetadataBuilder(
      int userStringHeapStartOffset = 0,
      int stringHeapStartOffset = 0,
      int blobHeapStartOffset = 0,
      int guidHeapStartOffset = 0)
    {
      if (userStringHeapStartOffset >= 16777215)
        Throw.HeapSizeLimitExceeded(HeapIndex.UserString);
      if (userStringHeapStartOffset < 0)
        Throw.ArgumentOutOfRange(nameof (userStringHeapStartOffset));
      if (stringHeapStartOffset < 0)
        Throw.ArgumentOutOfRange(nameof (stringHeapStartOffset));
      if (blobHeapStartOffset < 0)
        Throw.ArgumentOutOfRange(nameof (blobHeapStartOffset));
      if (guidHeapStartOffset < 0)
        Throw.ArgumentOutOfRange(nameof (guidHeapStartOffset));
      if (guidHeapStartOffset % 16 != 0)
        throw new ArgumentException();
      this._userStringBuilder.WriteByte((byte) 0);
      this._blobs.Add(ImmutableArray<byte>.Empty, new BlobHandle());
      this._blobHeapSize = 1;
      this._userStringHeapStartOffset = userStringHeapStartOffset;
      this._stringHeapStartOffset = stringHeapStartOffset;
      this._blobHeapStartOffset = blobHeapStartOffset;
      this._guidBuilder.WriteBytes((byte) 0, guidHeapStartOffset);
    }

    /// <summary>Sets the capacity of the specified table.</summary>
    /// <param name="heap">Heap index.</param>
    /// <param name="byteCount">Number of bytes.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="heap" /> is not a valid heap index.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="byteCount" /> is negative.</exception>
    /// <remarks>
    /// Use to reduce allocations if the approximate number of bytes is known ahead of time.
    /// </remarks>
    public void SetCapacity(HeapIndex heap, int byteCount)
    {
      if (byteCount < 0)
        Throw.ArgumentOutOfRange(nameof (byteCount));
      switch (heap)
      {
        case HeapIndex.UserString:
          this._userStringBuilder.SetCapacity(byteCount);
          break;
        case HeapIndex.String:
          this._stringHeapCapacity = byteCount;
          break;
        case HeapIndex.Blob:
          break;
        case HeapIndex.Guid:
          this._guidBuilder.SetCapacity(byteCount);
          break;
        default:
          Throw.ArgumentOutOfRange(nameof (heap));
          break;
      }
    }


    #nullable enable
    internal static int SerializeHandle(ImmutableArray<int> map, StringHandle handle) => map[handle.GetWriterVirtualIndex()];

    internal static int SerializeHandle(BlobHandle handle) => handle.GetHeapOffset();

    internal static int SerializeHandle(GuidHandle handle) => handle.Index;

    internal static int SerializeHandle(UserStringHandle handle) => handle.GetHeapOffset();

    /// <summary>
    /// Adds specified blob to Blob heap, if it's not there already.
    /// </summary>
    /// <param name="value"><see cref="T:System.Reflection.Metadata.BlobBuilder" /> containing the blob.</param>
    /// <returns>Handle to the added or existing blob.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public BlobHandle GetOrAddBlob(BlobBuilder value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      return this.GetOrAddBlob(value.ToImmutableArray());
    }

    /// <summary>
    /// Adds specified blob to Blob heap, if it's not there already.
    /// </summary>
    /// <param name="value">Array containing the blob.</param>
    /// <returns>Handle to the added or existing blob.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public BlobHandle GetOrAddBlob(byte[] value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      return this.GetOrAddBlob(ImmutableArray.Create<byte>(value));
    }

    /// <summary>
    /// Adds specified blob to Blob heap, if it's not there already.
    /// </summary>
    /// <param name="value">Array containing the blob.</param>
    /// <returns>Handle to the added or existing blob.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public BlobHandle GetOrAddBlob(ImmutableArray<byte> value)
    {
      if (value.IsDefault)
        Throw.ArgumentNull(nameof (value));
      BlobHandle orAddBlob;
      if (!this._blobs.TryGetValue(value, out orAddBlob))
      {
        orAddBlob = BlobHandle.FromOffset(this._blobHeapStartOffset + this._blobHeapSize);
        this._blobs.Add(value, orAddBlob);
        this._blobHeapSize += BlobWriterImpl.GetCompressedIntegerSize(value.Length) + value.Length;
      }
      return orAddBlob;
    }

    /// <summary>
    /// Encodes a constant value to a blob and adds it to the Blob heap, if it's not there already.
    /// Uses UTF16 to encode string constants.
    /// </summary>
    /// <param name="value">Constant value.</param>
    /// <returns>Handle to the added or existing blob.</returns>
    public BlobHandle GetOrAddConstantBlob(object? value)
    {
      if (value is string str)
        return this.GetOrAddBlobUTF16(str);
      PooledBlobBuilder instance = PooledBlobBuilder.GetInstance();
      instance.WriteConstant(value);
      BlobHandle orAddBlob = this.GetOrAddBlob((BlobBuilder) instance);
      instance.Free();
      return orAddBlob;
    }

    /// <summary>
    /// Encodes a string using UTF16 encoding to a blob and adds it to the Blob heap, if it's not there already.
    /// </summary>
    /// <param name="value">String.</param>
    /// <returns>Handle to the added or existing blob.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public BlobHandle GetOrAddBlobUTF16(string value)
    {
      PooledBlobBuilder instance = PooledBlobBuilder.GetInstance();
      instance.WriteUTF16(value);
      BlobHandle orAddBlob = this.GetOrAddBlob((BlobBuilder) instance);
      instance.Free();
      return orAddBlob;
    }

    /// <summary>
    /// Encodes a string using UTF8 encoding to a blob and adds it to the Blob heap, if it's not there already.
    /// </summary>
    /// <param name="value">Constant value.</param>
    /// <param name="allowUnpairedSurrogates">
    /// True to encode unpaired surrogates as specified, otherwise replace them with U+FFFD character.
    /// </param>
    /// <returns>Handle to the added or existing blob.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public BlobHandle GetOrAddBlobUTF8(string value, bool allowUnpairedSurrogates = true)
    {
      PooledBlobBuilder instance = PooledBlobBuilder.GetInstance();
      instance.WriteUTF8(value, allowUnpairedSurrogates);
      BlobHandle orAddBlob = this.GetOrAddBlob((BlobBuilder) instance);
      instance.Free();
      return orAddBlob;
    }

    /// <summary>
    /// Encodes a debug document name and adds it to the Blob heap, if it's not there already.
    /// </summary>
    /// <param name="value">Document name.</param>
    /// <returns>
    /// Handle to the added or existing document name blob
    /// (see https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#DocumentNameBlob).
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public BlobHandle GetOrAddDocumentName(string value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      char ch = MetadataBuilder.ChooseSeparator(value);
      PooledBlobBuilder instance1 = PooledBlobBuilder.GetInstance();
      instance1.WriteByte((byte) ch);
      PooledBlobBuilder instance2 = PooledBlobBuilder.GetInstance();
      int num1 = 0;
      while (true)
      {
        int num2 = value.IndexOf(ch, num1);
        instance2.WriteUTF8(value, num1, (num2 >= 0 ? num2 : value.Length) - num1, true, false);
        instance1.WriteCompressedInteger(this.GetOrAddBlob((BlobBuilder) instance2).GetHeapOffset());
        if (num2 != -1)
        {
          if (num2 != value.Length - 1)
          {
            instance2.Clear();
            num1 = num2 + 1;
          }
          else
            break;
        }
        else
          goto label_7;
      }
      instance1.WriteByte((byte) 0);
label_7:
      instance2.Free();
      BlobHandle orAddBlob = this.GetOrAddBlob((BlobBuilder) instance1);
      instance1.Free();
      return orAddBlob;
    }


    #nullable disable
    private static char ChooseSeparator(string str)
    {
      int num1 = 0;
      int num2 = 0;
      foreach (char ch in str)
      {
        switch (ch)
        {
          case '/':
            ++num1;
            break;
          case '\\':
            ++num2;
            break;
        }
      }
      return num1 < num2 ? '\\' : '/';
    }

    /// <summary>
    /// Adds specified Guid to Guid heap, if it's not there already.
    /// </summary>
    /// <param name="guid">Guid to add.</param>
    /// <returns>Handle to the added or existing Guid.</returns>
    public GuidHandle GetOrAddGuid(Guid guid)
    {
      if (guid == Guid.Empty)
        return new GuidHandle();
      GuidHandle newGuidHandle;
      if (this._guids.TryGetValue(guid, out newGuidHandle))
        return newGuidHandle;
      newGuidHandle = this.GetNewGuidHandle();
      this._guids.Add(guid, newGuidHandle);
      this._guidBuilder.WriteGuid(guid);
      return newGuidHandle;
    }


    #nullable enable
    /// <summary>Reserves space on the Guid heap for a GUID.</summary>
    /// <returns>
    /// Handle to the reserved Guid and a <see cref="T:System.Reflection.Metadata.Blob" /> representing the GUID blob as stored on the heap.
    /// </returns>
    /// <exception cref="T:System.Reflection.Metadata.ImageFormatLimitationException">The remaining space on the heap is too small to fit the string.</exception>
    public ReservedBlob<GuidHandle> ReserveGuid() => new ReservedBlob<GuidHandle>(this.GetNewGuidHandle(), this._guidBuilder.ReserveBytes(16));

    private GuidHandle GetNewGuidHandle() => GuidHandle.FromIndex((this._guidBuilder.Count >> 4) + 1);

    /// <summary>
    /// Adds specified string to String heap, if it's not there already.
    /// </summary>
    /// <param name="value">Array containing the blob.</param>
    /// <returns>Handle to the added or existing blob.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public StringHandle GetOrAddString(string value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      StringHandle orAddString;
      if (value.Length == 0)
        orAddString = new StringHandle();
      else if (!this._strings.TryGetValue(value, out orAddString))
      {
        orAddString = StringHandle.FromWriterVirtualIndex(this._strings.Count + 1);
        this._strings.Add(value, orAddString);
      }
      return orAddString;
    }

    /// <summary>
    /// Reserves space on the User String heap for a string of specified length.
    /// </summary>
    /// <param name="length">The number of characters to reserve.</param>
    /// <returns>
    /// Handle to the reserved User String and a <see cref="T:System.Reflection.Metadata.Blob" /> representing the entire User String blob (including its length and terminal character).
    /// 
    /// Handle may be used in <see cref="M:System.Reflection.Metadata.Ecma335.InstructionEncoder.LoadString(System.Reflection.Metadata.UserStringHandle)" />.
    /// Use <see cref="M:System.Reflection.Metadata.BlobWriter.WriteUserString(System.String)" /> to fill in the blob content.
    /// </returns>
    /// <exception cref="T:System.Reflection.Metadata.ImageFormatLimitationException">The remaining space on the heap is too small to fit the string.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="length" /> is negative.</exception>
    public ReservedBlob<UserStringHandle> ReserveUserString(int length)
    {
      if (length < 0)
        Throw.ArgumentOutOfRange(nameof (length));
      UserStringHandle userStringHandle = this.GetNewUserStringHandle();
      int stringByteLength = BlobUtilities.GetUserStringByteLength(length);
      Blob content = this._userStringBuilder.ReserveBytes(BlobWriterImpl.GetCompressedIntegerSize(stringByteLength) + stringByteLength);
      return new ReservedBlob<UserStringHandle>(userStringHandle, content);
    }

    /// <summary>
    /// Adds specified string to User String heap, if it's not there already.
    /// </summary>
    /// <param name="value">String to add.</param>
    /// <returns>
    /// Handle to the added or existing string.
    /// May be used in <see cref="M:System.Reflection.Metadata.Ecma335.InstructionEncoder.LoadString(System.Reflection.Metadata.UserStringHandle)" />.
    /// </returns>
    /// <exception cref="T:System.Reflection.Metadata.ImageFormatLimitationException">The remaining space on the heap is too small to fit the string.</exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="value" /> is null.</exception>
    public UserStringHandle GetOrAddUserString(string value)
    {
      if (value == null)
        Throw.ArgumentNull(nameof (value));
      UserStringHandle userStringHandle;
      if (!this._userStrings.TryGetValue(value, out userStringHandle))
      {
        userStringHandle = this.GetNewUserStringHandle();
        this._userStrings.Add(value, userStringHandle);
        this._userStringBuilder.WriteUserString(value);
      }
      return userStringHandle;
    }

    private UserStringHandle GetNewUserStringHandle()
    {
      int heapOffset = this._userStringHeapStartOffset + this._userStringBuilder.Count;
      if (heapOffset >= 16777216)
        Throw.HeapSizeLimitExceeded(HeapIndex.UserString);
      return UserStringHandle.FromOffset(heapOffset);
    }


    #nullable disable
    /// <summary>
    /// Fills in stringIndexMap with data from stringIndex and write to stringWriter.
    /// Releases stringIndex as the stringTable is sealed after this point.
    /// </summary>
    private static ImmutableArray<int> SerializeStringHeap(
      BlobBuilder heapBuilder,
      Dictionary<string, StringHandle> strings,
      int stringHeapStartOffset)
    {
      List<KeyValuePair<string, StringHandle>> keyValuePairList = new List<KeyValuePair<string, StringHandle>>((IEnumerable<KeyValuePair<string, StringHandle>>) strings);
      keyValuePairList.Sort((IComparer<KeyValuePair<string, StringHandle>>) MetadataBuilder.SuffixSort.Instance);
      int initialCapacity = keyValuePairList.Count + 1;
      ImmutableArray<int>.Builder builder = ImmutableArray.CreateBuilder<int>(initialCapacity);
      builder.Count = initialCapacity;
      builder[0] = 0;
      heapBuilder.WriteByte((byte) 0);
      string str = string.Empty;
      foreach (KeyValuePair<string, StringHandle> keyValuePair in keyValuePairList)
      {
        int num = stringHeapStartOffset + heapBuilder.Count;
        if (str.EndsWith(keyValuePair.Key, StringComparison.Ordinal) && !BlobUtilities.IsLowSurrogateChar((int) keyValuePair.Key[0]))
        {
          builder[keyValuePair.Value.GetWriterVirtualIndex()] = num - (BlobUtilities.GetUTF8ByteCount(keyValuePair.Key) + 1);
        }
        else
        {
          builder[keyValuePair.Value.GetWriterVirtualIndex()] = num;
          heapBuilder.WriteUTF8(keyValuePair.Key, false);
          heapBuilder.WriteByte((byte) 0);
        }
        str = keyValuePair.Key;
      }
      return builder.MoveToImmutable();
    }


    #nullable enable
    internal void WriteHeapsTo(BlobBuilder builder, BlobBuilder stringHeap)
    {
      MetadataBuilder.WriteAligned(stringHeap, builder);
      MetadataBuilder.WriteAligned((BlobBuilder) this._userStringBuilder, builder);
      MetadataBuilder.WriteAligned((BlobBuilder) this._guidBuilder, builder);
      this.WriteAlignedBlobHeap(builder);
    }


    #nullable disable
    private void WriteAlignedBlobHeap(BlobBuilder builder)
    {
      int byteCount = BitArithmetic.Align(this._blobHeapSize, 4) - this._blobHeapSize;
      BlobWriter blobWriter = new BlobWriter(builder.ReserveBytes(this._blobHeapSize + byteCount));
      int blobHeapStartOffset = this._blobHeapStartOffset;
      foreach (KeyValuePair<ImmutableArray<byte>, BlobHandle> blob in this._blobs)
      {
        int heapOffset = blob.Value.GetHeapOffset();
        ImmutableArray<byte> key = blob.Key;
        blobWriter.Offset = heapOffset == 0 ? 0 : heapOffset - blobHeapStartOffset;
        blobWriter.WriteCompressedInteger(key.Length);
        blobWriter.WriteBytes(key);
      }
      blobWriter.Offset = this._blobHeapSize;
      blobWriter.WriteBytes((byte) 0, byteCount);
    }

    private static void WriteAligned(BlobBuilder source, BlobBuilder target)
    {
      int count = source.Count;
      target.LinkSuffix(source);
      target.WriteBytes((byte) 0, BitArithmetic.Align(count, 4) - count);
    }

    private struct AssemblyRefTableRow
    {
      public Version Version;
      public BlobHandle PublicKeyToken;
      public StringHandle Name;
      public StringHandle Culture;
      public uint Flags;
      public BlobHandle HashValue;
    }

    private struct ModuleRow
    {
      public ushort Generation;
      public StringHandle Name;
      public GuidHandle ModuleVersionId;
      public GuidHandle EncId;
      public GuidHandle EncBaseId;
    }

    private struct AssemblyRow
    {
      public uint HashAlgorithm;
      public Version Version;
      public ushort Flags;
      public BlobHandle AssemblyKey;
      public StringHandle AssemblyName;
      public StringHandle AssemblyCulture;
    }

    private struct ClassLayoutRow
    {
      public ushort PackingSize;
      public uint ClassSize;
      public int Parent;
    }

    private struct ConstantRow
    {
      public byte Type;
      public int Parent;
      public BlobHandle Value;
    }

    private struct CustomAttributeRow
    {
      public int Parent;
      public int Type;
      public BlobHandle Value;
    }

    private struct DeclSecurityRow
    {
      public ushort Action;
      public int Parent;
      public BlobHandle PermissionSet;
    }

    private struct EncLogRow
    {
      public int Token;
      public byte FuncCode;
    }

    private struct EncMapRow
    {
      public int Token;
    }

    private struct EventRow
    {
      public ushort EventFlags;
      public StringHandle Name;
      public int EventType;
    }

    private struct EventMapRow
    {
      public int Parent;
      public int EventList;
    }

    private struct ExportedTypeRow
    {
      public uint Flags;
      public int TypeDefId;
      public StringHandle TypeName;
      public StringHandle TypeNamespace;
      public int Implementation;
    }

    private struct FieldLayoutRow
    {
      public int Offset;
      public int Field;
    }

    private struct FieldMarshalRow
    {
      public int Parent;
      public BlobHandle NativeType;
    }

    private struct FieldRvaRow
    {
      public int Offset;
      public int Field;
    }

    private struct FieldDefRow
    {
      public ushort Flags;
      public StringHandle Name;
      public BlobHandle Signature;
    }

    private struct FileTableRow
    {
      public uint Flags;
      public StringHandle FileName;
      public BlobHandle HashValue;
    }

    private struct GenericParamConstraintRow
    {
      public int Owner;
      public int Constraint;
    }

    private struct GenericParamRow
    {
      public ushort Number;
      public ushort Flags;
      public int Owner;
      public StringHandle Name;
    }

    private struct ImplMapRow
    {
      public ushort MappingFlags;
      public int MemberForwarded;
      public StringHandle ImportName;
      public int ImportScope;
    }

    private struct InterfaceImplRow
    {
      public int Class;
      public int Interface;
    }

    private struct ManifestResourceRow
    {
      public uint Offset;
      public uint Flags;
      public StringHandle Name;
      public int Implementation;
    }

    private struct MemberRefRow
    {
      public int Class;
      public StringHandle Name;
      public BlobHandle Signature;
    }

    private struct MethodImplRow
    {
      public int Class;
      public int MethodBody;
      public int MethodDecl;
    }

    private struct MethodSemanticsRow
    {
      public ushort Semantic;
      public int Method;
      public int Association;
    }

    private struct MethodSpecRow
    {
      public int Method;
      public BlobHandle Instantiation;
    }

    private struct MethodRow
    {
      public int BodyOffset;
      public ushort ImplFlags;
      public ushort Flags;
      public StringHandle Name;
      public BlobHandle Signature;
      public int ParamList;
    }

    private struct ModuleRefRow
    {
      public StringHandle Name;
    }

    private struct NestedClassRow
    {
      public int NestedClass;
      public int EnclosingClass;
    }

    private struct ParamRow
    {
      public ushort Flags;
      public ushort Sequence;
      public StringHandle Name;
    }

    private struct PropertyMapRow
    {
      public int Parent;
      public int PropertyList;
    }

    private struct PropertyRow
    {
      public ushort PropFlags;
      public StringHandle Name;
      public BlobHandle Type;
    }

    private struct TypeDefRow
    {
      public uint Flags;
      public StringHandle Name;
      public StringHandle Namespace;
      public int Extends;
      public int FieldList;
      public int MethodList;
    }

    private struct TypeRefRow
    {
      public int ResolutionScope;
      public StringHandle Name;
      public StringHandle Namespace;
    }

    private struct TypeSpecRow
    {
      public BlobHandle Signature;
    }

    private struct StandaloneSigRow
    {
      public BlobHandle Signature;
    }

    private struct DocumentRow
    {
      public BlobHandle Name;
      public GuidHandle HashAlgorithm;
      public BlobHandle Hash;
      public GuidHandle Language;
    }

    private struct MethodDebugInformationRow
    {
      public int Document;
      public BlobHandle SequencePoints;
    }

    private struct LocalScopeRow
    {
      public int Method;
      public int ImportScope;
      public int VariableList;
      public int ConstantList;
      public int StartOffset;
      public int Length;
    }

    private struct LocalVariableRow
    {
      public ushort Attributes;
      public ushort Index;
      public StringHandle Name;
    }

    private struct LocalConstantRow
    {
      public StringHandle Name;
      public BlobHandle Signature;
    }

    private struct ImportScopeRow
    {
      public int Parent;
      public BlobHandle Imports;
    }

    private struct StateMachineMethodRow
    {
      public int MoveNextMethod;
      public int KickoffMethod;
    }

    private struct CustomDebugInformationRow
    {
      public int Parent;
      public GuidHandle Kind;
      public BlobHandle Value;
    }

    private sealed class HeapBlobBuilder : BlobBuilder
    {
      private int _capacityExpansion;

      public HeapBlobBuilder(int capacity)
        : base(capacity)
      {
      }

      protected override BlobBuilder AllocateChunk(int minimalSize) => (BlobBuilder) new MetadataBuilder.HeapBlobBuilder(Math.Max(Math.Max(minimalSize, this.ChunkCapacity), this._capacityExpansion));

      internal void SetCapacity(int capacity) => this._capacityExpansion = Math.Max(0, capacity - this.Count - this.FreeBytes);
    }

    /// <summary>
    /// Sorts strings such that a string is followed immediately by all strings
    /// that are a suffix of it.
    /// </summary>
    private sealed class SuffixSort : IComparer<KeyValuePair<string, StringHandle>>
    {
      internal static MetadataBuilder.SuffixSort Instance = new MetadataBuilder.SuffixSort();

      public int Compare(
        KeyValuePair<string, StringHandle> xPair,
        KeyValuePair<string, StringHandle> yPair)
      {
        string key1 = xPair.Key;
        string key2 = yPair.Key;
        int index1 = key1.Length - 1;
        for (int index2 = key2.Length - 1; index1 >= 0 & index2 >= 0; --index2)
        {
          if ((int) key1[index1] < (int) key2[index2])
            return -1;
          if ((int) key1[index1] > (int) key2[index2])
            return 1;
          --index1;
        }
        return key2.Length.CompareTo(key1.Length);
      }
    }
  }
}

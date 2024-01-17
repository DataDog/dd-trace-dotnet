﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataReaderExtensions
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    /// <summary>
    /// Provides extension methods for working with certain raw elements of the ECMA-335 metadata tables and heaps.
    /// </summary>
    public static class MetadataReaderExtensions
  {
    /// <summary>Returns the number of rows in the specified table.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="tableIndex" /> is not a valid table index.</exception>
    public static int GetTableRowCount(this MetadataReader reader, TableIndex tableIndex)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      if (tableIndex >= (TableIndex) MetadataTokens.TableCount)
        Throw.TableIndexOutOfRange();
      return reader.TableRowCounts[(int) tableIndex];
    }

    /// <summary>Returns the size of a row in the specified table.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="tableIndex" /> is not a valid table index.</exception>
    public static int GetTableRowSize(this MetadataReader reader, TableIndex tableIndex)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      switch (tableIndex)
      {
        case TableIndex.Module:
          return reader.ModuleTable.RowSize;
        case TableIndex.TypeRef:
          return reader.TypeRefTable.RowSize;
        case TableIndex.TypeDef:
          return reader.TypeDefTable.RowSize;
        case TableIndex.FieldPtr:
          return reader.FieldPtrTable.RowSize;
        case TableIndex.Field:
          return reader.FieldTable.RowSize;
        case TableIndex.MethodPtr:
          return reader.MethodPtrTable.RowSize;
        case TableIndex.MethodDef:
          return reader.MethodDefTable.RowSize;
        case TableIndex.ParamPtr:
          return reader.ParamPtrTable.RowSize;
        case TableIndex.Param:
          return reader.ParamTable.RowSize;
        case TableIndex.InterfaceImpl:
          return reader.InterfaceImplTable.RowSize;
        case TableIndex.MemberRef:
          return reader.MemberRefTable.RowSize;
        case TableIndex.Constant:
          return reader.ConstantTable.RowSize;
        case TableIndex.CustomAttribute:
          return reader.CustomAttributeTable.RowSize;
        case TableIndex.FieldMarshal:
          return reader.FieldMarshalTable.RowSize;
        case TableIndex.DeclSecurity:
          return reader.DeclSecurityTable.RowSize;
        case TableIndex.ClassLayout:
          return reader.ClassLayoutTable.RowSize;
        case TableIndex.FieldLayout:
          return reader.FieldLayoutTable.RowSize;
        case TableIndex.StandAloneSig:
          return reader.StandAloneSigTable.RowSize;
        case TableIndex.EventMap:
          return reader.EventMapTable.RowSize;
        case TableIndex.EventPtr:
          return reader.EventPtrTable.RowSize;
        case TableIndex.Event:
          return reader.EventTable.RowSize;
        case TableIndex.PropertyMap:
          return reader.PropertyMapTable.RowSize;
        case TableIndex.PropertyPtr:
          return reader.PropertyPtrTable.RowSize;
        case TableIndex.Property:
          return reader.PropertyTable.RowSize;
        case TableIndex.MethodSemantics:
          return reader.MethodSemanticsTable.RowSize;
        case TableIndex.MethodImpl:
          return reader.MethodImplTable.RowSize;
        case TableIndex.ModuleRef:
          return reader.ModuleRefTable.RowSize;
        case TableIndex.TypeSpec:
          return reader.TypeSpecTable.RowSize;
        case TableIndex.ImplMap:
          return reader.ImplMapTable.RowSize;
        case TableIndex.FieldRva:
          return reader.FieldRvaTable.RowSize;
        case TableIndex.EncLog:
          return reader.EncLogTable.RowSize;
        case TableIndex.EncMap:
          return reader.EncMapTable.RowSize;
        case TableIndex.Assembly:
          return reader.AssemblyTable.RowSize;
        case TableIndex.AssemblyProcessor:
          return reader.AssemblyProcessorTable.RowSize;
        case TableIndex.AssemblyOS:
          return reader.AssemblyOSTable.RowSize;
        case TableIndex.AssemblyRef:
          return reader.AssemblyRefTable.RowSize;
        case TableIndex.AssemblyRefProcessor:
          return reader.AssemblyRefProcessorTable.RowSize;
        case TableIndex.AssemblyRefOS:
          return reader.AssemblyRefOSTable.RowSize;
        case TableIndex.File:
          return reader.FileTable.RowSize;
        case TableIndex.ExportedType:
          return reader.ExportedTypeTable.RowSize;
        case TableIndex.ManifestResource:
          return reader.ManifestResourceTable.RowSize;
        case TableIndex.NestedClass:
          return reader.NestedClassTable.RowSize;
        case TableIndex.GenericParam:
          return reader.GenericParamTable.RowSize;
        case TableIndex.MethodSpec:
          return reader.MethodSpecTable.RowSize;
        case TableIndex.GenericParamConstraint:
          return reader.GenericParamConstraintTable.RowSize;
        case TableIndex.Document:
          return reader.DocumentTable.RowSize;
        case TableIndex.MethodDebugInformation:
          return reader.MethodDebugInformationTable.RowSize;
        case TableIndex.LocalScope:
          return reader.LocalScopeTable.RowSize;
        case TableIndex.LocalVariable:
          return reader.LocalVariableTable.RowSize;
        case TableIndex.LocalConstant:
          return reader.LocalConstantTable.RowSize;
        case TableIndex.ImportScope:
          return reader.ImportScopeTable.RowSize;
        case TableIndex.StateMachineMethod:
          return reader.StateMachineMethodTable.RowSize;
        case TableIndex.CustomDebugInformation:
          return reader.CustomDebugInformationTable.RowSize;
        default:
          throw new ArgumentOutOfRangeException(nameof (tableIndex));
      }
    }

    /// <summary>
    /// Returns the offset from the start of metadata to the specified table.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="tableIndex" /> is not a valid table index.</exception>
    public static unsafe int GetTableMetadataOffset(
      this MetadataReader reader,
      TableIndex tableIndex)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return (int) (reader.GetTableMetadataBlock(tableIndex).Pointer - reader.Block.Pointer);
    }


    #nullable disable
    private static MemoryBlock GetTableMetadataBlock(
      this MetadataReader reader,
      TableIndex tableIndex)
    {
      switch (tableIndex)
      {
        case TableIndex.Module:
          return reader.ModuleTable.Block;
        case TableIndex.TypeRef:
          return reader.TypeRefTable.Block;
        case TableIndex.TypeDef:
          return reader.TypeDefTable.Block;
        case TableIndex.FieldPtr:
          return reader.FieldPtrTable.Block;
        case TableIndex.Field:
          return reader.FieldTable.Block;
        case TableIndex.MethodPtr:
          return reader.MethodPtrTable.Block;
        case TableIndex.MethodDef:
          return reader.MethodDefTable.Block;
        case TableIndex.ParamPtr:
          return reader.ParamPtrTable.Block;
        case TableIndex.Param:
          return reader.ParamTable.Block;
        case TableIndex.InterfaceImpl:
          return reader.InterfaceImplTable.Block;
        case TableIndex.MemberRef:
          return reader.MemberRefTable.Block;
        case TableIndex.Constant:
          return reader.ConstantTable.Block;
        case TableIndex.CustomAttribute:
          return reader.CustomAttributeTable.Block;
        case TableIndex.FieldMarshal:
          return reader.FieldMarshalTable.Block;
        case TableIndex.DeclSecurity:
          return reader.DeclSecurityTable.Block;
        case TableIndex.ClassLayout:
          return reader.ClassLayoutTable.Block;
        case TableIndex.FieldLayout:
          return reader.FieldLayoutTable.Block;
        case TableIndex.StandAloneSig:
          return reader.StandAloneSigTable.Block;
        case TableIndex.EventMap:
          return reader.EventMapTable.Block;
        case TableIndex.EventPtr:
          return reader.EventPtrTable.Block;
        case TableIndex.Event:
          return reader.EventTable.Block;
        case TableIndex.PropertyMap:
          return reader.PropertyMapTable.Block;
        case TableIndex.PropertyPtr:
          return reader.PropertyPtrTable.Block;
        case TableIndex.Property:
          return reader.PropertyTable.Block;
        case TableIndex.MethodSemantics:
          return reader.MethodSemanticsTable.Block;
        case TableIndex.MethodImpl:
          return reader.MethodImplTable.Block;
        case TableIndex.ModuleRef:
          return reader.ModuleRefTable.Block;
        case TableIndex.TypeSpec:
          return reader.TypeSpecTable.Block;
        case TableIndex.ImplMap:
          return reader.ImplMapTable.Block;
        case TableIndex.FieldRva:
          return reader.FieldRvaTable.Block;
        case TableIndex.EncLog:
          return reader.EncLogTable.Block;
        case TableIndex.EncMap:
          return reader.EncMapTable.Block;
        case TableIndex.Assembly:
          return reader.AssemblyTable.Block;
        case TableIndex.AssemblyProcessor:
          return reader.AssemblyProcessorTable.Block;
        case TableIndex.AssemblyOS:
          return reader.AssemblyOSTable.Block;
        case TableIndex.AssemblyRef:
          return reader.AssemblyRefTable.Block;
        case TableIndex.AssemblyRefProcessor:
          return reader.AssemblyRefProcessorTable.Block;
        case TableIndex.AssemblyRefOS:
          return reader.AssemblyRefOSTable.Block;
        case TableIndex.File:
          return reader.FileTable.Block;
        case TableIndex.ExportedType:
          return reader.ExportedTypeTable.Block;
        case TableIndex.ManifestResource:
          return reader.ManifestResourceTable.Block;
        case TableIndex.NestedClass:
          return reader.NestedClassTable.Block;
        case TableIndex.GenericParam:
          return reader.GenericParamTable.Block;
        case TableIndex.MethodSpec:
          return reader.MethodSpecTable.Block;
        case TableIndex.GenericParamConstraint:
          return reader.GenericParamConstraintTable.Block;
        case TableIndex.Document:
          return reader.DocumentTable.Block;
        case TableIndex.MethodDebugInformation:
          return reader.MethodDebugInformationTable.Block;
        case TableIndex.LocalScope:
          return reader.LocalScopeTable.Block;
        case TableIndex.LocalVariable:
          return reader.LocalVariableTable.Block;
        case TableIndex.LocalConstant:
          return reader.LocalConstantTable.Block;
        case TableIndex.ImportScope:
          return reader.ImportScopeTable.Block;
        case TableIndex.StateMachineMethod:
          return reader.StateMachineMethodTable.Block;
        case TableIndex.CustomDebugInformation:
          return reader.CustomDebugInformationTable.Block;
        default:
          throw new ArgumentOutOfRangeException(nameof (tableIndex));
      }
    }


    #nullable enable
    /// <summary>Returns the size of the specified heap.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="heapIndex" /> is not a valid heap index.</exception>
    public static int GetHeapSize(this MetadataReader reader, HeapIndex heapIndex)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return reader.GetMetadataBlock(heapIndex).Length;
    }

    /// <summary>
    /// Returns the offset from the start of metadata to the specified heap.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="heapIndex" /> is not a valid heap index.</exception>
    public static unsafe int GetHeapMetadataOffset(this MetadataReader reader, HeapIndex heapIndex)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return (int) (reader.GetMetadataBlock(heapIndex).Pointer - reader.Block.Pointer);
    }


    #nullable disable
    /// <summary>Returns the size of the specified heap.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="heapIndex" /> is not a valid heap index.</exception>
    private static MemoryBlock GetMetadataBlock(this MetadataReader reader, HeapIndex heapIndex)
    {
      switch (heapIndex)
      {
        case HeapIndex.UserString:
          return reader.UserStringHeap.Block;
        case HeapIndex.String:
          return reader.StringHeap.Block;
        case HeapIndex.Blob:
          return reader.BlobHeap.Block;
        case HeapIndex.Guid:
          return reader.GuidHeap.Block;
        default:
          throw new ArgumentOutOfRangeException(nameof (heapIndex));
      }
    }


    #nullable enable
    /// <summary>
    /// Returns the a handle to the UserString that follows the given one in the UserString heap or a nil handle if it is the last one.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    public static UserStringHandle GetNextHandle(
      this MetadataReader reader,
      UserStringHandle handle)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return reader.UserStringHeap.GetNextHandle(handle);
    }

    /// <summary>
    /// Returns the a handle to the Blob that follows the given one in the Blob heap or a nil handle if it is the last one.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    public static BlobHandle GetNextHandle(this MetadataReader reader, BlobHandle handle)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return reader.BlobHeap.GetNextHandle(handle);
    }

    /// <summary>
    /// Returns the a handle to the String that follows the given one in the String heap or a nil handle if it is the last one.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    public static StringHandle GetNextHandle(this MetadataReader reader, StringHandle handle)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return reader.StringHeap.GetNextHandle(handle);
    }

    /// <summary>Enumerates entries of EnC log.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    public static IEnumerable<EditAndContinueLogEntry> GetEditAndContinueLogEntries(
      this MetadataReader reader)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return Core(reader);

      static IEnumerable<EditAndContinueLogEntry> Core(MetadataReader reader)
      {
        for (int rid = 1; rid <= reader.EncLogTable.NumberOfRows; ++rid)
          yield return new EditAndContinueLogEntry(new EntityHandle(reader.EncLogTable.GetToken(rid)), reader.EncLogTable.GetFuncCode(rid));
      }
    }

    /// <summary>Enumerates entries of EnC map.</summary>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="reader" /> is null.</exception>
    public static IEnumerable<EntityHandle> GetEditAndContinueMapEntries(this MetadataReader reader)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return Core(reader);

      static IEnumerable<EntityHandle> Core(MetadataReader reader)
      {
        for (int rid = 1; rid <= reader.EncMapTable.NumberOfRows; ++rid)
          yield return new EntityHandle(reader.EncMapTable.GetToken(rid));
      }
    }

    /// <summary>Enumerate types that define one or more properties.</summary>
    /// <returns>
    /// The resulting sequence corresponds exactly to entries in PropertyMap table,
    /// i.e. n-th returned <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> is stored in n-th row of PropertyMap.
    /// </returns>
    public static IEnumerable<TypeDefinitionHandle> GetTypesWithProperties(
      this MetadataReader reader)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return Core(reader);

      static IEnumerable<TypeDefinitionHandle> Core(MetadataReader reader)
      {
        for (int rid = 1; rid <= reader.PropertyMapTable.NumberOfRows; ++rid)
          yield return reader.PropertyMapTable.GetParentType(rid);
      }
    }

    /// <summary>Enumerate types that define one or more events.</summary>
    /// <returns>
    /// The resulting sequence corresponds exactly to entries in EventMap table,
    /// i.e. n-th returned <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> is stored in n-th row of EventMap.
    /// </returns>
    public static IEnumerable<TypeDefinitionHandle> GetTypesWithEvents(this MetadataReader reader)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      return Core(reader);

      static IEnumerable<TypeDefinitionHandle> Core(MetadataReader reader)
      {
        for (int rid = 1; rid <= reader.EventMapTable.NumberOfRows; ++rid)
          yield return reader.EventMapTable.GetParentType(rid);
      }
    }

    /// <summary>
    /// Given a type handle and a raw type kind found in a signature blob determines whether the target type is a value type or a reference type.
    /// </summary>
    public static SignatureTypeKind ResolveSignatureTypeKind(
      this MetadataReader reader,
      EntityHandle typeHandle,
      byte rawTypeKind)
    {
      if (reader == null)
        Throw.ArgumentNull(nameof (reader));
      SignatureTypeKind signatureTypeKind = (SignatureTypeKind) rawTypeKind;
      switch (signatureTypeKind)
      {
        case SignatureTypeKind.Unknown:
          return SignatureTypeKind.Unknown;
        case SignatureTypeKind.ValueType:
        case SignatureTypeKind.Class:
          switch (typeHandle.Kind)
          {
            case HandleKind.TypeReference:
              TypeRefSignatureTreatment signatureTreatment = reader.GetTypeReference((TypeReferenceHandle) typeHandle).SignatureTreatment;
              switch (signatureTreatment)
              {
                case TypeRefSignatureTreatment.None:
                  return signatureTypeKind;
                case TypeRefSignatureTreatment.ProjectedToClass:
                  return SignatureTypeKind.Class;
                case TypeRefSignatureTreatment.ProjectedToValueType:
                  return SignatureTypeKind.ValueType;
                default:
                  throw ExceptionUtilities.UnexpectedValue((object) signatureTreatment);
              }
            case HandleKind.TypeDefinition:
              return signatureTypeKind;
            case HandleKind.TypeSpecification:
              return SignatureTypeKind.Unknown;
            default:
              throw new ArgumentOutOfRangeException(nameof (typeHandle), SR.Format(SR.UnexpectedHandleKind, (object) typeHandle.Kind));
          }
        default:
          throw new ArgumentOutOfRangeException(nameof (rawTypeKind));
      }
    }
  }
}

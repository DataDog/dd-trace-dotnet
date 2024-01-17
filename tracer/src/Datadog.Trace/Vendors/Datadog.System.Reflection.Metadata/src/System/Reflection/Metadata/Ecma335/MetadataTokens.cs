﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataTokens
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public static class MetadataTokens
  {
    /// <summary>
    /// Maximum number of tables that can be present in Ecma335 metadata.
    /// </summary>
    public static readonly int TableCount = 64;
    /// <summary>
    /// Maximum number of tables that can be present in Ecma335 metadata.
    /// </summary>
    public static readonly int HeapCount = 4;

    /// <summary>
    /// Returns the row number of a metadata table entry that corresponds
    /// to the specified <paramref name="handle" /> in the context of <paramref name="reader" />.
    /// </summary>
    /// <returns>One based row number.</returns>
    /// <exception cref="T:System.ArgumentException">The <paramref name="handle" /> is not a valid metadata table handle.</exception>
    public static int GetRowNumber(this MetadataReader reader, System.Reflection.Metadata.EntityHandle handle) => handle.IsVirtual ? MetadataTokens.MapVirtualHandleRowId(reader, (System.Reflection.Metadata.Handle) handle) : handle.RowId;

    /// <summary>
    /// Returns the offset of metadata heap data that corresponds
    /// to the specified <paramref name="handle" /> in the context of <paramref name="reader" />.
    /// </summary>
    /// <returns>Zero based offset, or -1 if <paramref name="handle" /> isn't a metadata heap handle.</returns>
    /// <exception cref="T:System.NotSupportedException">The operation is not supported for the specified <paramref name="handle" />.</exception>
    /// <exception cref="T:System.ArgumentException">The <paramref name="handle" /> is invalid.</exception>
    public static int GetHeapOffset(this MetadataReader reader, System.Reflection.Metadata.Handle handle)
    {
      if (!handle.IsHeapHandle)
        Throw.HeapHandleRequired();
      return handle.IsVirtual ? MetadataTokens.MapVirtualHandleRowId(reader, handle) : handle.Offset;
    }

    /// <summary>
    /// Returns the metadata token of the specified <paramref name="handle" /> in the context of <paramref name="reader" />.
    /// </summary>
    /// <returns>Metadata token.</returns>
    /// <exception cref="T:System.NotSupportedException">The operation is not supported for the specified <paramref name="handle" />.</exception>
    public static int GetToken(this MetadataReader reader, System.Reflection.Metadata.EntityHandle handle) => handle.IsVirtual ? (int) handle.Type | MetadataTokens.MapVirtualHandleRowId(reader, (System.Reflection.Metadata.Handle) handle) : handle.Token;

    /// <summary>
    /// Returns the metadata token of the specified <paramref name="handle" /> in the context of <paramref name="reader" />.
    /// </summary>
    /// <returns>Metadata token.</returns>
    /// <exception cref="T:System.ArgumentException">
    /// Handle represents a metadata entity that doesn't have a token.
    /// A token can only be retrieved for a metadata table handle or a heap handle of type <see cref="F:System.Reflection.Metadata.HandleKind.UserString" />.
    /// </exception>
    /// <exception cref="T:System.NotSupportedException">The operation is not supported for the specified <paramref name="handle" />.</exception>
    public static int GetToken(this MetadataReader reader, System.Reflection.Metadata.Handle handle)
    {
      if (!handle.IsEntityOrUserStringHandle)
        Throw.EntityOrUserStringHandleRequired();
      return handle.IsVirtual ? (int) handle.EntityHandleType | MetadataTokens.MapVirtualHandleRowId(reader, handle) : handle.Token;
    }


    #nullable disable
    private static int MapVirtualHandleRowId(MetadataReader reader, System.Reflection.Metadata.Handle handle)
    {
      switch (handle.Kind)
      {
        case HandleKind.AssemblyReference:
          return reader.AssemblyRefTable.NumberOfNonVirtualRows + 1 + handle.RowId;
        case HandleKind.Blob:
        case HandleKind.String:
          throw new NotSupportedException(SR.CantGetOffsetForVirtualHeapHandle);
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(handle.Kind);
          return 0;
      }
    }

    /// <summary>
    /// Returns the row number of a metadata table entry that corresponds
    /// to the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// One based row number, or -1 if <paramref name="handle" /> can only be interpreted in a context of a specific <see cref="T:System.Reflection.Metadata.MetadataReader" />.
    /// See <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.GetRowNumber(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.EntityHandle)" />.
    /// </returns>
    public static int GetRowNumber(System.Reflection.Metadata.EntityHandle handle) => !handle.IsVirtual ? handle.RowId : -1;

    /// <summary>
    /// Returns the offset of metadata heap data that corresponds
    /// to the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// An offset in the corresponding heap, or -1 if <paramref name="handle" /> can only be interpreted in a context of a specific <see cref="T:System.Reflection.Metadata.MetadataReader" /> or <see cref="T:System.Reflection.Metadata.Ecma335.MetadataBuilder" />.
    /// See <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.GetHeapOffset(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.Handle)" />.
    /// </returns>
    public static int GetHeapOffset(System.Reflection.Metadata.Handle handle)
    {
      if (!handle.IsHeapHandle)
        Throw.HeapHandleRequired();
      return handle.IsVirtual ? -1 : handle.Offset;
    }

    /// <summary>
    /// Returns the offset of metadata heap data that corresponds
    /// to the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// Zero based offset, or -1 if <paramref name="handle" /> can only be interpreted in a context of a specific <see cref="T:System.Reflection.Metadata.MetadataReader" /> or <see cref="T:System.Reflection.Metadata.Ecma335.MetadataBuilder" />.
    /// See <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.GetHeapOffset(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.Handle)" />.
    /// </returns>
    public static int GetHeapOffset(System.Reflection.Metadata.BlobHandle handle) => !handle.IsVirtual ? handle.GetHeapOffset() : -1;

    /// <summary>
    /// Returns the offset of metadata heap data that corresponds
    /// to the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// 1-based index into the #Guid heap. Unlike other heaps, which are essentially byte arrays, the #Guid heap is an array of 16-byte GUIDs.
    /// </returns>
    public static int GetHeapOffset(System.Reflection.Metadata.GuidHandle handle) => handle.Index;

    /// <summary>
    /// Returns the offset of metadata heap data that corresponds
    /// to the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>Zero based offset.</returns>
    public static int GetHeapOffset(System.Reflection.Metadata.UserStringHandle handle) => handle.GetHeapOffset();

    /// <summary>
    /// Returns the offset of metadata heap data that corresponds
    /// to the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// Zero based offset, or -1 if <paramref name="handle" /> can only be interpreted in a context of a specific <see cref="T:System.Reflection.Metadata.MetadataReader" /> or <see cref="T:System.Reflection.Metadata.Ecma335.MetadataBuilder" />.
    /// See <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.GetHeapOffset(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.Handle)" />.
    /// </returns>
    public static int GetHeapOffset(System.Reflection.Metadata.StringHandle handle) => !handle.IsVirtual ? handle.GetHeapOffset() : -1;

    /// <summary>
    /// Returns the metadata token of the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// Metadata token, or 0 if <paramref name="handle" /> can only be interpreted in a context of a specific <see cref="T:System.Reflection.Metadata.MetadataReader" />.
    /// See <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.Handle)" />.
    /// </returns>
    /// <exception cref="T:System.ArgumentException">
    /// Handle represents a metadata entity that doesn't have a token.
    /// A token can only be retrieved for a metadata table handle or a heap handle of type <see cref="F:System.Reflection.Metadata.HandleKind.UserString" />.
    /// </exception>
    public static int GetToken(System.Reflection.Metadata.Handle handle)
    {
      if (!handle.IsEntityOrUserStringHandle)
        Throw.EntityOrUserStringHandleRequired();
      return handle.IsVirtual ? 0 : handle.Token;
    }

    /// <summary>
    /// Returns the metadata token of the specified <paramref name="handle" />.
    /// </summary>
    /// <returns>
    /// Metadata token, or 0 if <paramref name="handle" /> can only be interpreted in a context of a specific <see cref="T:System.Reflection.Metadata.MetadataReader" />.
    /// See <see cref="M:System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.EntityHandle)" />.
    /// </returns>
    public static int GetToken(System.Reflection.Metadata.EntityHandle handle) => !handle.IsVirtual ? handle.Token : 0;


    #nullable enable
    /// <summary>
    /// Gets the <see cref="T:System.Reflection.Metadata.Ecma335.TableIndex" /> of the table corresponding to the specified <see cref="T:System.Reflection.Metadata.HandleKind" />.
    /// </summary>
    /// <param name="type">Handle type.</param>
    /// <param name="index">Table index.</param>
    /// <returns>True if the handle type corresponds to an Ecma335 or Portable PDB table, false otherwise.</returns>
    public static bool TryGetTableIndex(HandleKind type, out TableIndex index)
    {
      if (type < (HandleKind) MetadataTokens.TableCount && (1L << (int) (type & (HandleKind.CustomDebugInformation | HandleKind.Parameter)) & 71811071505072127L) != 0L)
      {
        index = (TableIndex) type;
        return true;
      }
      index = TableIndex.Module;
      return false;
    }

    /// <summary>
    /// Gets the <see cref="T:System.Reflection.Metadata.Ecma335.HeapIndex" /> of the heap corresponding to the specified <see cref="T:System.Reflection.Metadata.HandleKind" />.
    /// </summary>
    /// <param name="type">Handle type.</param>
    /// <param name="index">Heap index.</param>
    /// <returns>True if the handle type corresponds to an Ecma335 heap, false otherwise.</returns>
    public static bool TryGetHeapIndex(HandleKind type, out HeapIndex index)
    {
      switch (type)
      {
        case HandleKind.UserString:
          index = HeapIndex.UserString;
          return true;
        case HandleKind.Blob:
          index = HeapIndex.Blob;
          return true;
        case HandleKind.Guid:
          index = HeapIndex.Guid;
          return true;
        case HandleKind.String:
        case HandleKind.NamespaceDefinition:
          index = HeapIndex.String;
          return true;
        default:
          index = HeapIndex.UserString;
          return false;
      }
    }

    /// <summary>Creates a handle from a token value.</summary>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="token" /> is not a valid metadata token.
    /// It must encode a metadata table entity or an offset in <see cref="F:System.Reflection.Metadata.HandleKind.UserString" /> heap.
    /// </exception>
    public static System.Reflection.Metadata.Handle Handle(int token)
    {
      if (!TokenTypeIds.IsEntityOrUserStringToken((uint) token))
        Throw.InvalidToken();
      return System.Reflection.Metadata.Handle.FromVToken((uint) token);
    }

    /// <summary>Creates an entity handle from a token value.</summary>
    /// <exception cref="T:System.ArgumentException"><paramref name="token" /> is not a valid metadata entity token.</exception>
    public static System.Reflection.Metadata.EntityHandle EntityHandle(int token)
    {
      if (!TokenTypeIds.IsEntityToken((uint) token))
        Throw.InvalidToken();
      return new System.Reflection.Metadata.EntityHandle((uint) token);
    }

    /// <summary>
    /// Creates an <see cref="T:System.Reflection.Metadata.EntityHandle" /> from a token value.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="tableIndex" /> is not a valid table index.</exception>
    public static System.Reflection.Metadata.EntityHandle EntityHandle(
      TableIndex tableIndex,
      int rowNumber)
    {
      return MetadataTokens.Handle(tableIndex, rowNumber);
    }

    /// <summary>
    /// Creates an <see cref="T:System.Reflection.Metadata.EntityHandle" /> from a token value.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">
    /// <paramref name="tableIndex" /> is not a valid table index.</exception>
    public static System.Reflection.Metadata.EntityHandle Handle(
      TableIndex tableIndex,
      int rowNumber)
    {
      int vToken = (int) tableIndex << 24 | rowNumber;
      if (!TokenTypeIds.IsEntityOrUserStringToken((uint) vToken))
        Throw.TableIndexOutOfRange();
      return new System.Reflection.Metadata.EntityHandle((uint) vToken);
    }

    private static int ToRowId(int rowNumber) => rowNumber & 16777215;

    public static System.Reflection.Metadata.MethodDefinitionHandle MethodDefinitionHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.MethodDefinitionHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.MethodImplementationHandle MethodImplementationHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.MethodImplementationHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.MethodSpecificationHandle MethodSpecificationHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.MethodSpecificationHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.TypeDefinitionHandle TypeDefinitionHandle(int rowNumber) => System.Reflection.Metadata.TypeDefinitionHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.ExportedTypeHandle ExportedTypeHandle(int rowNumber) => System.Reflection.Metadata.ExportedTypeHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.TypeReferenceHandle TypeReferenceHandle(int rowNumber) => System.Reflection.Metadata.TypeReferenceHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.TypeSpecificationHandle TypeSpecificationHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.TypeSpecificationHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.InterfaceImplementationHandle InterfaceImplementationHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.InterfaceImplementationHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.MemberReferenceHandle MemberReferenceHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.MemberReferenceHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.FieldDefinitionHandle FieldDefinitionHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.FieldDefinitionHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.EventDefinitionHandle EventDefinitionHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.EventDefinitionHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.PropertyDefinitionHandle PropertyDefinitionHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.PropertyDefinitionHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.StandaloneSignatureHandle StandaloneSignatureHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.StandaloneSignatureHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.ParameterHandle ParameterHandle(int rowNumber) => System.Reflection.Metadata.ParameterHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.GenericParameterHandle GenericParameterHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.GenericParameterHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.GenericParameterConstraintHandle GenericParameterConstraintHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.GenericParameterConstraintHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.ModuleReferenceHandle ModuleReferenceHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.ModuleReferenceHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.AssemblyReferenceHandle AssemblyReferenceHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.AssemblyReferenceHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.CustomAttributeHandle CustomAttributeHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.CustomAttributeHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.DeclarativeSecurityAttributeHandle DeclarativeSecurityAttributeHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.DeclarativeSecurityAttributeHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.ConstantHandle ConstantHandle(int rowNumber) => System.Reflection.Metadata.ConstantHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.ManifestResourceHandle ManifestResourceHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.ManifestResourceHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.AssemblyFileHandle AssemblyFileHandle(int rowNumber) => System.Reflection.Metadata.AssemblyFileHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.DocumentHandle DocumentHandle(int rowNumber) => System.Reflection.Metadata.DocumentHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.MethodDebugInformationHandle MethodDebugInformationHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.MethodDebugInformationHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.LocalScopeHandle LocalScopeHandle(int rowNumber) => System.Reflection.Metadata.LocalScopeHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.LocalVariableHandle LocalVariableHandle(int rowNumber) => System.Reflection.Metadata.LocalVariableHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.LocalConstantHandle LocalConstantHandle(int rowNumber) => System.Reflection.Metadata.LocalConstantHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.ImportScopeHandle ImportScopeHandle(int rowNumber) => System.Reflection.Metadata.ImportScopeHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));

    public static System.Reflection.Metadata.CustomDebugInformationHandle CustomDebugInformationHandle(
      int rowNumber)
    {
      return System.Reflection.Metadata.CustomDebugInformationHandle.FromRowId(MetadataTokens.ToRowId(rowNumber));
    }

    public static System.Reflection.Metadata.UserStringHandle UserStringHandle(int offset) => System.Reflection.Metadata.UserStringHandle.FromOffset(offset & 16777215);

    public static System.Reflection.Metadata.StringHandle StringHandle(int offset) => System.Reflection.Metadata.StringHandle.FromOffset(offset);

    public static System.Reflection.Metadata.BlobHandle BlobHandle(int offset) => System.Reflection.Metadata.BlobHandle.FromOffset(offset);

    public static System.Reflection.Metadata.GuidHandle GuidHandle(int offset) => System.Reflection.Metadata.GuidHandle.FromIndex(offset);

    public static System.Reflection.Metadata.DocumentNameBlobHandle DocumentNameBlobHandle(
      int offset)
    {
      return System.Reflection.Metadata.DocumentNameBlobHandle.FromOffset(offset);
    }
  }
}

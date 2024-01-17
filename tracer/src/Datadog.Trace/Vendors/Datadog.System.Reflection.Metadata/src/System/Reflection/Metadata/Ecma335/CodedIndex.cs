﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CodedIndex
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public static class CodedIndex
  {
    /// <summary>
    /// Calculates a HasCustomAttribute coded index for the specified handle.
    /// </summary>
    /// <param name="handle">
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
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int HasCustomAttribute(EntityHandle handle) => (int) ((CodedIndex.HasCustomAttributeTag) (handle.RowId << 5) | CodedIndex.ToHasCustomAttributeTag(handle.Kind));

    /// <summary>
    /// Calculates a HasConstant coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.ParameterHandle" />, <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" />, or <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int HasConstant(EntityHandle handle) => (int) ((CodedIndex.HasConstantTag) (handle.RowId << 2) | CodedIndex.ToHasConstantTag(handle.Kind));

    /// <summary>
    /// Calculates a CustomAttributeType coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int CustomAttributeType(EntityHandle handle) => (int) ((CodedIndex.CustomAttributeTypeTag) (handle.RowId << 3) | CodedIndex.ToCustomAttributeTypeTag(handle.Kind));

    /// <summary>
    /// Calculates a HasDeclSecurity coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />, or <see cref="T:System.Reflection.Metadata.AssemblyDefinitionHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int HasDeclSecurity(EntityHandle handle) => (int) ((CodedIndex.HasDeclSecurityTag) (handle.RowId << 2) | CodedIndex.ToHasDeclSecurityTag(handle.Kind));

    /// <summary>
    /// Calculates a HasFieldMarshal coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.ParameterHandle" /> or <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int HasFieldMarshal(EntityHandle handle) => (int) ((CodedIndex.HasFieldMarshalTag) (handle.RowId << 1) | CodedIndex.ToHasFieldMarshalTag(handle.Kind));

    /// <summary>
    /// Calculates a HasSemantics coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.EventDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int HasSemantics(EntityHandle handle) => (int) ((CodedIndex.HasSemanticsTag) (handle.RowId << 1) | CodedIndex.ToHasSemanticsTag(handle.Kind));

    /// <summary>
    /// Calculates a Implementation coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.AssemblyFileHandle" />, <see cref="T:System.Reflection.Metadata.ExportedTypeHandle" /> or <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int Implementation(EntityHandle handle) => (int) ((CodedIndex.ImplementationTag) (handle.RowId << 2) | CodedIndex.ToImplementationTag(handle.Kind));

    /// <summary>
    /// Calculates a MemberForwarded coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.FieldDefinition" />, <see cref="T:System.Reflection.Metadata.MethodDefinition" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int MemberForwarded(EntityHandle handle) => (int) ((CodedIndex.MemberForwardedTag) (handle.RowId << 1) | CodedIndex.ToMemberForwardedTag(handle.Kind));

    /// <summary>
    /// Calculates a MemberRefParent coded index for the specified handle.
    /// </summary>
    /// <param name="handle">
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />,
    /// <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />,
    /// <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />, or
    /// <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />.
    /// </param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int MemberRefParent(EntityHandle handle) => (int) ((CodedIndex.MemberRefParentTag) (handle.RowId << 3) | CodedIndex.ToMemberRefParentTag(handle.Kind));

    /// <summary>
    /// Calculates a MethodDefOrRef coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MemberReferenceHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int MethodDefOrRef(EntityHandle handle) => (int) ((CodedIndex.MethodDefOrRefTag) (handle.RowId << 1) | CodedIndex.ToMethodDefOrRefTag(handle.Kind));

    /// <summary>
    /// Calculates a ResolutionScope coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.ModuleDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />, <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int ResolutionScope(EntityHandle handle) => (int) ((CodedIndex.ResolutionScopeTag) (handle.RowId << 2) | CodedIndex.ToResolutionScopeTag(handle.Kind));

    /// <summary>
    /// Calculates a TypeDefOrRef coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int TypeDefOrRef(EntityHandle handle) => (int) ((CodedIndex.TypeDefOrRefTag) (handle.RowId << 2) | CodedIndex.ToTypeDefOrRefTag(handle.Kind));

    /// <summary>
    /// Calculates a TypeDefOrRefOrSpec coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int TypeDefOrRefOrSpec(EntityHandle handle) => (int) ((CodedIndex.TypeDefOrRefOrSpecTag) (handle.RowId << 2) | CodedIndex.ToTypeDefOrRefOrSpecTag(handle.Kind));

    /// <summary>
    /// Calculates a TypeOrMethodDef coded index for the specified handle.
    /// </summary>
    /// <param name="handle"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" /></param>
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int TypeOrMethodDef(EntityHandle handle) => (int) ((CodedIndex.TypeOrMethodDefTag) (handle.RowId << 1) | CodedIndex.ToTypeOrMethodDefTag(handle.Kind));

    /// <summary>
    /// Calculates a HasCustomDebugInformation coded index for the specified handle.
    /// </summary>
    /// <param name="handle">
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
    /// <exception cref="T:System.ArgumentException">Unexpected handle kind.</exception>
    public static int HasCustomDebugInformation(EntityHandle handle) => (int) ((CodedIndex.HasCustomDebugInformationTag) (handle.RowId << 5) | CodedIndex.ToHasCustomDebugInformationTag(handle.Kind));

    private static CodedIndex.HasCustomAttributeTag ToHasCustomAttributeTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.ModuleDefinition:
          return CodedIndex.HasCustomAttributeTag.Module;
        case HandleKind.TypeReference:
          return CodedIndex.HasCustomAttributeTag.TypeRef;
        case HandleKind.TypeDefinition:
          return CodedIndex.HasCustomAttributeTag.TypeDef;
        case HandleKind.FieldDefinition:
          return CodedIndex.HasCustomAttributeTag.Field;
        case HandleKind.MethodDefinition:
          return CodedIndex.HasCustomAttributeTag.MethodDef;
        case HandleKind.Parameter:
          return CodedIndex.HasCustomAttributeTag.Param;
        case HandleKind.InterfaceImplementation:
          return CodedIndex.HasCustomAttributeTag.InterfaceImpl;
        case HandleKind.MemberReference:
          return CodedIndex.HasCustomAttributeTag.MemberRef;
        case HandleKind.DeclarativeSecurityAttribute:
          return CodedIndex.HasCustomAttributeTag.DeclSecurity;
        case HandleKind.StandaloneSignature:
          return CodedIndex.HasCustomAttributeTag.StandAloneSig;
        case HandleKind.EventDefinition:
          return CodedIndex.HasCustomAttributeTag.Event;
        case HandleKind.PropertyDefinition:
          return CodedIndex.HasCustomAttributeTag.Property;
        case HandleKind.ModuleReference:
          return CodedIndex.HasCustomAttributeTag.ModuleRef;
        case HandleKind.TypeSpecification:
          return CodedIndex.HasCustomAttributeTag.TypeSpec;
        case HandleKind.AssemblyDefinition:
          return CodedIndex.HasCustomAttributeTag.Assembly;
        case HandleKind.AssemblyReference:
          return CodedIndex.HasCustomAttributeTag.AssemblyRef;
        case HandleKind.AssemblyFile:
          return CodedIndex.HasCustomAttributeTag.File;
        case HandleKind.ExportedType:
          return CodedIndex.HasCustomAttributeTag.ExportedType;
        case HandleKind.ManifestResource:
          return CodedIndex.HasCustomAttributeTag.ManifestResource;
        case HandleKind.GenericParameter:
          return CodedIndex.HasCustomAttributeTag.GenericParam;
        case HandleKind.MethodSpecification:
          return CodedIndex.HasCustomAttributeTag.MethodSpec;
        case HandleKind.GenericParameterConstraint:
          return CodedIndex.HasCustomAttributeTag.GenericParamConstraint;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.HasCustomAttributeTag.MethodDef;
      }
    }

    private static CodedIndex.HasConstantTag ToHasConstantTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.FieldDefinition:
          return CodedIndex.HasConstantTag.Field;
        case HandleKind.Parameter:
          return CodedIndex.HasConstantTag.Param;
        case HandleKind.PropertyDefinition:
          return CodedIndex.HasConstantTag.Property;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.HasConstantTag.Field;
      }
    }

    private static CodedIndex.CustomAttributeTypeTag ToCustomAttributeTypeTag(HandleKind kind)
    {
      if (kind == HandleKind.MethodDefinition)
        return CodedIndex.CustomAttributeTypeTag.MethodDef;
      if (kind == HandleKind.MemberReference)
        return CodedIndex.CustomAttributeTypeTag.MemberRef;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return (CodedIndex.CustomAttributeTypeTag) 0;
    }

    private static CodedIndex.HasDeclSecurityTag ToHasDeclSecurityTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.TypeDefinition:
          return CodedIndex.HasDeclSecurityTag.TypeDef;
        case HandleKind.MethodDefinition:
          return CodedIndex.HasDeclSecurityTag.MethodDef;
        case HandleKind.AssemblyDefinition:
          return CodedIndex.HasDeclSecurityTag.Assembly;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.HasDeclSecurityTag.TypeDef;
      }
    }

    private static CodedIndex.HasFieldMarshalTag ToHasFieldMarshalTag(HandleKind kind)
    {
      if (kind == HandleKind.FieldDefinition)
        return CodedIndex.HasFieldMarshalTag.Field;
      if (kind == HandleKind.Parameter)
        return CodedIndex.HasFieldMarshalTag.Param;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return CodedIndex.HasFieldMarshalTag.Field;
    }

    private static CodedIndex.HasSemanticsTag ToHasSemanticsTag(HandleKind kind)
    {
      if (kind == HandleKind.EventDefinition)
        return CodedIndex.HasSemanticsTag.Event;
      if (kind == HandleKind.PropertyDefinition)
        return CodedIndex.HasSemanticsTag.Property;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return CodedIndex.HasSemanticsTag.Event;
    }

    private static CodedIndex.ImplementationTag ToImplementationTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.AssemblyReference:
          return CodedIndex.ImplementationTag.AssemblyRef;
        case HandleKind.AssemblyFile:
          return CodedIndex.ImplementationTag.File;
        case HandleKind.ExportedType:
          return CodedIndex.ImplementationTag.ExportedType;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.ImplementationTag.File;
      }
    }

    private static CodedIndex.MemberForwardedTag ToMemberForwardedTag(HandleKind kind)
    {
      if (kind == HandleKind.FieldDefinition)
        return CodedIndex.MemberForwardedTag.Field;
      if (kind == HandleKind.MethodDefinition)
        return CodedIndex.MemberForwardedTag.MethodDef;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return CodedIndex.MemberForwardedTag.Field;
    }

    private static CodedIndex.MemberRefParentTag ToMemberRefParentTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.TypeReference:
          return CodedIndex.MemberRefParentTag.TypeRef;
        case HandleKind.TypeDefinition:
          return CodedIndex.MemberRefParentTag.TypeDef;
        case HandleKind.MethodDefinition:
          return CodedIndex.MemberRefParentTag.MethodDef;
        case HandleKind.ModuleReference:
          return CodedIndex.MemberRefParentTag.ModuleRef;
        case HandleKind.TypeSpecification:
          return CodedIndex.MemberRefParentTag.TypeSpec;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.MemberRefParentTag.TypeDef;
      }
    }

    private static CodedIndex.MethodDefOrRefTag ToMethodDefOrRefTag(HandleKind kind)
    {
      if (kind == HandleKind.MethodDefinition)
        return CodedIndex.MethodDefOrRefTag.MethodDef;
      if (kind == HandleKind.MemberReference)
        return CodedIndex.MethodDefOrRefTag.MemberRef;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return CodedIndex.MethodDefOrRefTag.MethodDef;
    }

    private static CodedIndex.ResolutionScopeTag ToResolutionScopeTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.ModuleDefinition:
          return CodedIndex.ResolutionScopeTag.Module;
        case HandleKind.TypeReference:
          return CodedIndex.ResolutionScopeTag.TypeRef;
        case HandleKind.ModuleReference:
          return CodedIndex.ResolutionScopeTag.ModuleRef;
        case HandleKind.AssemblyReference:
          return CodedIndex.ResolutionScopeTag.AssemblyRef;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.ResolutionScopeTag.Module;
      }
    }

    private static CodedIndex.TypeDefOrRefOrSpecTag ToTypeDefOrRefOrSpecTag(HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.TypeReference:
          return CodedIndex.TypeDefOrRefOrSpecTag.TypeRef;
        case HandleKind.TypeDefinition:
          return CodedIndex.TypeDefOrRefOrSpecTag.TypeDef;
        case HandleKind.TypeSpecification:
          return CodedIndex.TypeDefOrRefOrSpecTag.TypeSpec;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.TypeDefOrRefOrSpecTag.TypeDef;
      }
    }

    private static CodedIndex.TypeDefOrRefTag ToTypeDefOrRefTag(HandleKind kind)
    {
      if (kind == HandleKind.TypeReference)
        return CodedIndex.TypeDefOrRefTag.TypeRef;
      if (kind == HandleKind.TypeDefinition)
        return CodedIndex.TypeDefOrRefTag.TypeDef;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return CodedIndex.TypeDefOrRefTag.TypeDef;
    }

    private static CodedIndex.TypeOrMethodDefTag ToTypeOrMethodDefTag(HandleKind kind)
    {
      if (kind == HandleKind.TypeDefinition)
        return CodedIndex.TypeOrMethodDefTag.TypeDef;
      if (kind == HandleKind.MethodDefinition)
        return CodedIndex.TypeOrMethodDefTag.MethodDef;
      Throw.InvalidArgument_UnexpectedHandleKind(kind);
      return CodedIndex.TypeOrMethodDefTag.TypeDef;
    }

    private static CodedIndex.HasCustomDebugInformationTag ToHasCustomDebugInformationTag(
      HandleKind kind)
    {
      switch (kind)
      {
        case HandleKind.ModuleDefinition:
          return CodedIndex.HasCustomDebugInformationTag.Module;
        case HandleKind.TypeReference:
          return CodedIndex.HasCustomDebugInformationTag.TypeRef;
        case HandleKind.TypeDefinition:
          return CodedIndex.HasCustomDebugInformationTag.TypeDef;
        case HandleKind.FieldDefinition:
          return CodedIndex.HasCustomDebugInformationTag.Field;
        case HandleKind.MethodDefinition:
          return CodedIndex.HasCustomDebugInformationTag.MethodDef;
        case HandleKind.Parameter:
          return CodedIndex.HasCustomDebugInformationTag.Param;
        case HandleKind.InterfaceImplementation:
          return CodedIndex.HasCustomDebugInformationTag.InterfaceImpl;
        case HandleKind.MemberReference:
          return CodedIndex.HasCustomDebugInformationTag.MemberRef;
        case HandleKind.DeclarativeSecurityAttribute:
          return CodedIndex.HasCustomDebugInformationTag.DeclSecurity;
        case HandleKind.StandaloneSignature:
          return CodedIndex.HasCustomDebugInformationTag.StandAloneSig;
        case HandleKind.EventDefinition:
          return CodedIndex.HasCustomDebugInformationTag.Event;
        case HandleKind.PropertyDefinition:
          return CodedIndex.HasCustomDebugInformationTag.Property;
        case HandleKind.ModuleReference:
          return CodedIndex.HasCustomDebugInformationTag.ModuleRef;
        case HandleKind.TypeSpecification:
          return CodedIndex.HasCustomDebugInformationTag.TypeSpec;
        case HandleKind.AssemblyDefinition:
          return CodedIndex.HasCustomDebugInformationTag.Assembly;
        case HandleKind.AssemblyReference:
          return CodedIndex.HasCustomDebugInformationTag.AssemblyRef;
        case HandleKind.AssemblyFile:
          return CodedIndex.HasCustomDebugInformationTag.File;
        case HandleKind.ExportedType:
          return CodedIndex.HasCustomDebugInformationTag.ExportedType;
        case HandleKind.ManifestResource:
          return CodedIndex.HasCustomDebugInformationTag.ManifestResource;
        case HandleKind.GenericParameter:
          return CodedIndex.HasCustomDebugInformationTag.GenericParam;
        case HandleKind.MethodSpecification:
          return CodedIndex.HasCustomDebugInformationTag.MethodSpec;
        case HandleKind.GenericParameterConstraint:
          return CodedIndex.HasCustomDebugInformationTag.GenericParamConstraint;
        case HandleKind.Document:
          return CodedIndex.HasCustomDebugInformationTag.Document;
        case HandleKind.LocalScope:
          return CodedIndex.HasCustomDebugInformationTag.LocalScope;
        case HandleKind.LocalVariable:
          return CodedIndex.HasCustomDebugInformationTag.LocalVariable;
        case HandleKind.LocalConstant:
          return CodedIndex.HasCustomDebugInformationTag.LocalConstant;
        case HandleKind.ImportScope:
          return CodedIndex.HasCustomDebugInformationTag.ImportScope;
        default:
          Throw.InvalidArgument_UnexpectedHandleKind(kind);
          return CodedIndex.HasCustomDebugInformationTag.MethodDef;
      }
    }

    private enum HasCustomAttributeTag
    {
      MethodDef = 0,
      Field = 1,
      TypeRef = 2,
      TypeDef = 3,
      Param = 4,
      BitCount = 5,
      InterfaceImpl = 5,
      MemberRef = 6,
      Module = 7,
      DeclSecurity = 8,
      Property = 9,
      Event = 10, // 0x0000000A
      StandAloneSig = 11, // 0x0000000B
      ModuleRef = 12, // 0x0000000C
      TypeSpec = 13, // 0x0000000D
      Assembly = 14, // 0x0000000E
      AssemblyRef = 15, // 0x0000000F
      File = 16, // 0x00000010
      ExportedType = 17, // 0x00000011
      ManifestResource = 18, // 0x00000012
      GenericParam = 19, // 0x00000013
      GenericParamConstraint = 20, // 0x00000014
      MethodSpec = 21, // 0x00000015
    }

    private enum HasConstantTag
    {
      Field = 0,
      Param = 1,
      BitCount = 2,
      Property = 2,
    }

    private enum CustomAttributeTypeTag
    {
      MethodDef = 2,
      BitCount = 3,
      MemberRef = 3,
    }

    private enum HasDeclSecurityTag
    {
      TypeDef = 0,
      MethodDef = 1,
      Assembly = 2,
      BitCount = 2,
    }

    private enum HasFieldMarshalTag
    {
      Field = 0,
      BitCount = 1,
      Param = 1,
    }

    private enum HasSemanticsTag
    {
      Event = 0,
      BitCount = 1,
      Property = 1,
    }

    private enum ImplementationTag
    {
      File = 0,
      AssemblyRef = 1,
      BitCount = 2,
      ExportedType = 2,
    }

    private enum MemberForwardedTag
    {
      Field = 0,
      BitCount = 1,
      MethodDef = 1,
    }

    private enum MemberRefParentTag
    {
      TypeDef = 0,
      TypeRef = 1,
      ModuleRef = 2,
      BitCount = 3,
      MethodDef = 3,
      TypeSpec = 4,
    }

    private enum MethodDefOrRefTag
    {
      MethodDef = 0,
      BitCount = 1,
      MemberRef = 1,
    }

    private enum ResolutionScopeTag
    {
      Module = 0,
      ModuleRef = 1,
      AssemblyRef = 2,
      BitCount = 2,
      TypeRef = 3,
    }

    private enum TypeDefOrRefOrSpecTag
    {
      TypeDef = 0,
      TypeRef = 1,
      BitCount = 2,
      TypeSpec = 2,
    }

    private enum TypeDefOrRefTag
    {
      TypeDef,
      TypeRef,
      BitCount,
    }

    private enum TypeOrMethodDefTag
    {
      TypeDef = 0,
      BitCount = 1,
      MethodDef = 1,
    }

    private enum HasCustomDebugInformationTag
    {
      MethodDef = 0,
      Field = 1,
      TypeRef = 2,
      TypeDef = 3,
      Param = 4,
      BitCount = 5,
      InterfaceImpl = 5,
      MemberRef = 6,
      Module = 7,
      DeclSecurity = 8,
      Property = 9,
      Event = 10, // 0x0000000A
      StandAloneSig = 11, // 0x0000000B
      ModuleRef = 12, // 0x0000000C
      TypeSpec = 13, // 0x0000000D
      Assembly = 14, // 0x0000000E
      AssemblyRef = 15, // 0x0000000F
      File = 16, // 0x00000010
      ExportedType = 17, // 0x00000011
      ManifestResource = 18, // 0x00000012
      GenericParam = 19, // 0x00000013
      GenericParamConstraint = 20, // 0x00000014
      MethodSpec = 21, // 0x00000015
      Document = 22, // 0x00000016
      LocalScope = 23, // 0x00000017
      LocalVariable = 24, // 0x00000018
      LocalConstant = 25, // 0x00000019
      ImportScope = 26, // 0x0000001A
    }
  }
}

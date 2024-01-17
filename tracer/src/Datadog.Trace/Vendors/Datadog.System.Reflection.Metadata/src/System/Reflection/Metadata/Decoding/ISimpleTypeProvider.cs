﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ISimpleTypeProvider`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public interface ISimpleTypeProvider<TType>
  {
    /// <summary>Gets the type symbol for a primitive type.</summary>
    TType GetPrimitiveType(PrimitiveTypeCode typeCode);

    /// <summary>Gets the type symbol for a type definition.</summary>
    /// <param name="reader">
    /// The metadata reader that was passed to the signature decoder. It may be null.
    /// </param>
    /// <param name="handle">The type definition handle.</param>
    /// <param name="rawTypeKind">
    /// The kind of the type as specified in the signature. To interpret this value use <see cref="M:System.Reflection.Metadata.Ecma335.MetadataReaderExtensions.ResolveSignatureTypeKind(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.EntityHandle,System.Byte)" />
    /// Note that when the signature comes from a WinMD file additional processing is needed to determine whether the target type is a value type or a reference type.
    /// </param>
    TType GetTypeFromDefinition(
      MetadataReader reader,
      TypeDefinitionHandle handle,
      byte rawTypeKind);

    /// <summary>Gets the type symbol for a type reference.</summary>
    /// <param name="reader">
    /// The metadata reader that was passed to the signature decoder. It may be null.
    /// </param>
    /// <param name="handle">The type definition handle.</param>
    /// <param name="rawTypeKind">
    /// The kind of the type as specified in the signature. To interpret this value use <see cref="M:System.Reflection.Metadata.Ecma335.MetadataReaderExtensions.ResolveSignatureTypeKind(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.EntityHandle,System.Byte)" />
    /// Note that when the signature comes from a WinMD file additional processing is needed to determine whether the target type is a value type or a reference type.
    /// </param>
    TType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind);
  }
}

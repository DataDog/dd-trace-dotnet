﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ISignatureTypeProvider`2
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public interface ISignatureTypeProvider<TType, TGenericContext> : 
    ISimpleTypeProvider<TType>,
    IConstructedTypeProvider<TType>,
    ISZArrayTypeProvider<TType>
  {
    /// <summary>
    /// Gets the a type symbol for the function pointer type of the given method signature.
    /// </summary>
    TType GetFunctionPointerType(MethodSignature<TType> signature);

    /// <summary>
    /// Gets the type symbol for the generic method parameter at the given zero-based index.
    /// </summary>
    TType GetGenericMethodParameter(TGenericContext genericContext, int index);

    /// <summary>
    /// Gets the type symbol for the generic type parameter at the given zero-based index.
    /// </summary>
    TType GetGenericTypeParameter(TGenericContext genericContext, int index);

    /// <summary>
    /// Gets the type symbol for a type with a custom modifier applied.
    /// </summary>
    /// <param name="modifier">The modifier type applied. </param>
    /// <param name="unmodifiedType">The type symbol of the underlying type without modifiers applied.</param>
    /// <param name="isRequired">True if the modifier is required, false if it's optional.</param>
    TType GetModifiedType(TType modifier, TType unmodifiedType, bool isRequired);

    /// <summary>
    /// Gets the type symbol for a local variable type that is marked as pinned.
    /// </summary>
    TType GetPinnedType(TType elementType);

    /// <summary>Gets the type symbol for a type specification.</summary>
    /// <param name="reader">
    /// The metadata reader that was passed to the signature decoder. It may be null.
    /// </param>
    /// <param name="genericContext">
    /// The context that was passed to the signature decoder.
    /// </param>
    /// <param name="handle">The type specification handle.</param>
    /// <param name="rawTypeKind">
    /// The kind of the type as specified in the signature. To interpret this value use <see cref="M:System.Reflection.Metadata.Ecma335.MetadataReaderExtensions.ResolveSignatureTypeKind(System.Reflection.Metadata.MetadataReader,System.Reflection.Metadata.EntityHandle,System.Byte)" />
    /// Note that when the signature comes from a WinMD file additional processing is needed to determine whether the target type is a value type or a reference type.
    /// </param>
    TType GetTypeFromSpecification(
      MetadataReader reader,
      TGenericContext genericContext,
      TypeSpecificationHandle handle,
      byte rawTypeKind);
  }
}

﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.SignatureDecoder`2
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>
  /// Decodes signature blobs.
  /// See Metadata Specification section II.23.2: Blobs and signatures.
  /// </summary>
  public readonly struct SignatureDecoder<TType, TGenericContext>
  {

    #nullable disable
    private readonly ISignatureTypeProvider<TType, TGenericContext> _provider;
    private readonly MetadataReader _metadataReaderOpt;
    private readonly TGenericContext _genericContext;


    #nullable enable
    /// <summary>Creates a new SignatureDecoder.</summary>
    /// <param name="provider">The provider used to obtain type symbols as the signature is decoded.</param>
    /// <param name="metadataReader">
    /// The metadata reader from which the signature was obtained. It may be null if the given provider allows it.
    /// </param>
    /// <param name="genericContext">
    /// Additional context needed to resolve generic parameters.
    /// </param>
    public SignatureDecoder(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      MetadataReader metadataReader,
      TGenericContext genericContext)
    {
      if (provider == null)
        Throw.ArgumentNull(nameof (provider));
      this._metadataReaderOpt = metadataReader;
      this._provider = provider;
      this._genericContext = genericContext;
    }

    /// <summary>
    /// Decodes a type embedded in a signature and advances the reader past the type.
    /// </summary>
    /// <param name="blobReader">The blob reader positioned at the leading SignatureTypeCode</param>
    /// <param name="allowTypeSpecifications">Allow a <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" /> to follow a (CLASS | VALUETYPE) in the signature.
    /// At present, the only context where that would be valid is in a LocalConstantSig as defined by the Portable PDB specification.
    /// </param>
    /// <returns>The decoded type.</returns>
    /// <exception cref="T:System.BadImageFormatException">The reader was not positioned at a valid signature type.</exception>
    public TType DecodeType(ref BlobReader blobReader, bool allowTypeSpecifications = false) => this.DecodeType(ref blobReader, allowTypeSpecifications, blobReader.ReadCompressedInteger());


    #nullable disable
    private TType DecodeType(ref BlobReader blobReader, bool allowTypeSpecifications, int typeCode)
    {
      switch (typeCode)
      {
        case 1:
        case 2:
        case 3:
        case 4:
        case 5:
        case 6:
        case 7:
        case 8:
        case 9:
        case 10:
        case 11:
        case 12:
        case 13:
        case 14:
        case 22:
        case 24:
        case 25:
        case 28:
          return this._provider.GetPrimitiveType((PrimitiveTypeCode) typeCode);
        case 15:
          return this._provider.GetPointerType(this.DecodeType(ref blobReader));
        case 16:
          return this._provider.GetByReferenceType(this.DecodeType(ref blobReader));
        case 17:
        case 18:
          return this.DecodeTypeHandle(ref blobReader, (byte) typeCode, allowTypeSpecifications);
        case 19:
          return this._provider.GetGenericTypeParameter(this._genericContext, blobReader.ReadCompressedInteger());
        case 20:
          return this.DecodeArrayType(ref blobReader);
        case 21:
          return this.DecodeGenericTypeInstance(ref blobReader);
        case 27:
          return this._provider.GetFunctionPointerType(this.DecodeMethodSignature(ref blobReader));
        case 29:
          return this._provider.GetSZArrayType(this.DecodeType(ref blobReader));
        case 30:
          return this._provider.GetGenericMethodParameter(this._genericContext, blobReader.ReadCompressedInteger());
        case 31:
          return this.DecodeModifiedType(ref blobReader, true);
        case 32:
          return this.DecodeModifiedType(ref blobReader, false);
        case 69:
          return this._provider.GetPinnedType(this.DecodeType(ref blobReader));
        default:
          throw new BadImageFormatException(SR.Format(SR.UnexpectedSignatureTypeCode, (object) typeCode));
      }
    }

    /// <summary>
    /// Decodes a list of types, with at least one instance that is preceded by its count as a compressed integer.
    /// </summary>
    private ImmutableArray<TType> DecodeTypeSequence(ref BlobReader blobReader)
    {
      int initialCapacity = blobReader.ReadCompressedInteger();
      ImmutableArray<TType>.Builder builder = initialCapacity != 0 ? ImmutableArray.CreateBuilder<TType>(initialCapacity) : throw new BadImageFormatException(SR.SignatureTypeSequenceMustHaveAtLeastOneElement);
      for (int index = 0; index < initialCapacity; ++index)
        builder.Add(this.DecodeType(ref blobReader));
      return builder.MoveToImmutable();
    }


    #nullable enable
    /// <summary>
    /// Decodes a method (definition, reference, or standalone) or property signature blob.
    /// </summary>
    /// <param name="blobReader">BlobReader positioned at a method signature.</param>
    /// <returns>The decoded method signature.</returns>
    /// <exception cref="T:System.BadImageFormatException">The method signature is invalid.</exception>
    public MethodSignature<TType> DecodeMethodSignature(ref BlobReader blobReader)
    {
      SignatureHeader header = blobReader.ReadSignatureHeader();
      SignatureDecoder<TType, TGenericContext>.CheckMethodOrPropertyHeader(header);
      int genericParameterCount = 0;
      if (header.IsGeneric)
        genericParameterCount = blobReader.ReadCompressedInteger();
      int initialCapacity = blobReader.ReadCompressedInteger();
      TType returnType = this.DecodeType(ref blobReader);
      int requiredParameterCount;
      ImmutableArray<TType> parameterTypes;
      if (initialCapacity == 0)
      {
        requiredParameterCount = 0;
        parameterTypes = ImmutableArray<TType>.Empty;
      }
      else
      {
        ImmutableArray<TType>.Builder builder = ImmutableArray.CreateBuilder<TType>(initialCapacity);
        int num;
        for (num = 0; num < initialCapacity; ++num)
        {
          int typeCode = blobReader.ReadCompressedInteger();
          if (typeCode != 65)
            builder.Add(this.DecodeType(ref blobReader, false, typeCode));
          else
            break;
        }
        requiredParameterCount = num;
        for (; num < initialCapacity; ++num)
          builder.Add(this.DecodeType(ref blobReader));
        parameterTypes = builder.MoveToImmutable();
      }
      return new MethodSignature<TType>(header, returnType, requiredParameterCount, genericParameterCount, parameterTypes);
    }

    /// <summary>
    /// Decodes a method specification signature blob and advances the reader past the signature.
    /// </summary>
    /// <param name="blobReader">A BlobReader positioned at a valid method specification signature.</param>
    /// <returns>The types used to instantiate a generic method via the method specification.</returns>
    public ImmutableArray<TType> DecodeMethodSpecificationSignature(ref BlobReader blobReader)
    {
      SignatureDecoder<TType, TGenericContext>.CheckHeader(blobReader.ReadSignatureHeader(), SignatureKind.MethodSpecification);
      return this.DecodeTypeSequence(ref blobReader);
    }

    /// <summary>
    /// Decodes a local variable signature blob and advances the reader past the signature.
    /// </summary>
    /// <param name="blobReader">The blob reader positioned at a local variable signature.</param>
    /// <returns>The local variable types.</returns>
    /// <exception cref="T:System.BadImageFormatException">The local variable signature is invalid.</exception>
    public ImmutableArray<TType> DecodeLocalSignature(ref BlobReader blobReader)
    {
      SignatureDecoder<TType, TGenericContext>.CheckHeader(blobReader.ReadSignatureHeader(), SignatureKind.LocalVariables);
      return this.DecodeTypeSequence(ref blobReader);
    }

    /// <summary>
    /// Decodes a field signature blob and advances the reader past the signature.
    /// </summary>
    /// <param name="blobReader">The blob reader positioned at a field signature.</param>
    /// <returns>The decoded field type.</returns>
    public TType DecodeFieldSignature(ref BlobReader blobReader)
    {
      SignatureDecoder<TType, TGenericContext>.CheckHeader(blobReader.ReadSignatureHeader(), SignatureKind.Field);
      return this.DecodeType(ref blobReader);
    }


    #nullable disable
    private TType DecodeArrayType(ref BlobReader blobReader)
    {
      TType elementType = this.DecodeType(ref blobReader);
      int rank = blobReader.ReadCompressedInteger();
      ImmutableArray<int> sizes = ImmutableArray<int>.Empty;
      ImmutableArray<int> lowerBounds = ImmutableArray<int>.Empty;
      int initialCapacity1 = blobReader.ReadCompressedInteger();
      if (initialCapacity1 > 0)
      {
        ImmutableArray<int>.Builder builder = ImmutableArray.CreateBuilder<int>(initialCapacity1);
        for (int index = 0; index < initialCapacity1; ++index)
          builder.Add(blobReader.ReadCompressedInteger());
        sizes = builder.MoveToImmutable();
      }
      int initialCapacity2 = blobReader.ReadCompressedInteger();
      if (initialCapacity2 > 0)
      {
        ImmutableArray<int>.Builder builder = ImmutableArray.CreateBuilder<int>(initialCapacity2);
        for (int index = 0; index < initialCapacity2; ++index)
          builder.Add(blobReader.ReadCompressedSignedInteger());
        lowerBounds = builder.MoveToImmutable();
      }
      ArrayShape shape = new ArrayShape(rank, sizes, lowerBounds);
      return this._provider.GetArrayType(elementType, shape);
    }

    private TType DecodeGenericTypeInstance(ref BlobReader blobReader) => this._provider.GetGenericInstantiation(this.DecodeType(ref blobReader), this.DecodeTypeSequence(ref blobReader));

    private TType DecodeModifiedType(ref BlobReader blobReader, bool isRequired) => this._provider.GetModifiedType(this.DecodeTypeHandle(ref blobReader, (byte) 0, true), this.DecodeType(ref blobReader), isRequired);

    private TType DecodeTypeHandle(
      ref BlobReader blobReader,
      byte rawTypeKind,
      bool allowTypeSpecifications)
    {
      EntityHandle handle = blobReader.ReadTypeHandle();
      if (!handle.IsNil)
      {
        switch (handle.Kind)
        {
          case HandleKind.TypeReference:
            return this._provider.GetTypeFromReference(this._metadataReaderOpt, (TypeReferenceHandle) handle, rawTypeKind);
          case HandleKind.TypeDefinition:
            return this._provider.GetTypeFromDefinition(this._metadataReaderOpt, (TypeDefinitionHandle) handle, rawTypeKind);
          case HandleKind.TypeSpecification:
            if (!allowTypeSpecifications)
              throw new BadImageFormatException(SR.NotTypeDefOrRefHandle);
            return this._provider.GetTypeFromSpecification(this._metadataReaderOpt, this._genericContext, (TypeSpecificationHandle) handle, rawTypeKind);
        }
      }
      throw new BadImageFormatException(SR.NotTypeDefOrRefOrSpecHandle);
    }

    private static void CheckHeader(SignatureHeader header, SignatureKind expectedKind)
    {
      if (header.Kind != expectedKind)
        throw new BadImageFormatException(SR.Format(SR.UnexpectedSignatureHeader, (object) expectedKind, (object) header.Kind, (object) header.RawValue));
    }

    private static void CheckMethodOrPropertyHeader(SignatureHeader header)
    {
      switch (header.Kind)
      {
        case SignatureKind.Method:
          break;
        case SignatureKind.Property:
          break;
        default:
          throw new BadImageFormatException(SR.Format(SR.UnexpectedSignatureHeader2, (object) SignatureKind.Property, (object) SignatureKind.Method, (object) header.Kind, (object) header.RawValue));
      }
    }
  }
}

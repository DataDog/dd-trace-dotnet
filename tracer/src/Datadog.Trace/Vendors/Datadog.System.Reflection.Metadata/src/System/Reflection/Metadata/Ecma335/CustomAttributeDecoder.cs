﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CustomAttributeDecoder`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>Decodes custom attribute blobs.</summary>
  internal readonly struct CustomAttributeDecoder<TType>
  {

    #nullable disable
    private readonly ICustomAttributeTypeProvider<TType> _provider;
    private readonly MetadataReader _reader;


    #nullable enable
    public CustomAttributeDecoder(
      ICustomAttributeTypeProvider<TType> provider,
      MetadataReader reader)
    {
      this._reader = reader;
      this._provider = provider;
    }

    public CustomAttributeValue<TType> DecodeValue(EntityHandle constructor, BlobHandle value)
    {
      BlobHandle handle = new BlobHandle();
      BlobHandle signature;
      switch (constructor.Kind)
      {
        case HandleKind.MethodDefinition:
          signature = this._reader.GetMethodDefinition((MethodDefinitionHandle) constructor).Signature;
          break;
        case HandleKind.MemberReference:
          MemberReference memberReference = this._reader.GetMemberReference((MemberReferenceHandle) constructor);
          signature = memberReference.Signature;
          if (memberReference.Parent.Kind == HandleKind.TypeSpecification)
          {
            handle = this._reader.GetTypeSpecification((TypeSpecificationHandle) memberReference.Parent).Signature;
            break;
          }
          break;
        default:
          throw new BadImageFormatException();
      }
      BlobReader blobReader1 = this._reader.GetBlobReader(signature);
      BlobReader blobReader2 = this._reader.GetBlobReader(value);
      if (blobReader2.ReadUInt16() != (ushort) 1)
        throw new BadImageFormatException();
      SignatureHeader signatureHeader = blobReader1.ReadSignatureHeader();
      if (signatureHeader.Kind != SignatureKind.Method || signatureHeader.IsGeneric)
        throw new BadImageFormatException();
      int count = blobReader1.ReadCompressedInteger();
      if (blobReader1.ReadSignatureTypeCode() != SignatureTypeCode.Void)
        throw new BadImageFormatException();
      BlobReader genericContextReader = new BlobReader();
      if (!handle.IsNil)
      {
        genericContextReader = this._reader.GetBlobReader(handle);
        if (genericContextReader.ReadSignatureTypeCode() == SignatureTypeCode.GenericTypeInstance)
        {
          switch (genericContextReader.ReadCompressedInteger())
          {
            case 17:
            case 18:
              genericContextReader.ReadTypeHandle();
              break;
            default:
              throw new BadImageFormatException();
          }
        }
        else
          genericContextReader = new BlobReader();
      }
      return new CustomAttributeValue<TType>(this.DecodeFixedArguments(ref blobReader1, ref blobReader2, count, genericContextReader), this.DecodeNamedArguments(ref blobReader2));
    }


    #nullable disable
    private ImmutableArray<CustomAttributeTypedArgument<TType>> DecodeFixedArguments(
      ref BlobReader signatureReader,
      ref BlobReader valueReader,
      int count,
      BlobReader genericContextReader)
    {
      if (count == 0)
        return ImmutableArray<CustomAttributeTypedArgument<TType>>.Empty;
      ImmutableArray<CustomAttributeTypedArgument<TType>>.Builder builder = ImmutableArray.CreateBuilder<CustomAttributeTypedArgument<TType>>(count);
      for (int index = 0; index < count; ++index)
      {
        CustomAttributeDecoder<TType>.ArgumentTypeInfo info = this.DecodeFixedArgumentType(ref signatureReader, genericContextReader);
        builder.Add(this.DecodeArgument(ref valueReader, info));
      }
      return builder.MoveToImmutable();
    }

    private ImmutableArray<CustomAttributeNamedArgument<TType>> DecodeNamedArguments(
      ref BlobReader valueReader)
    {
      int initialCapacity = (int) valueReader.ReadUInt16();
      if (initialCapacity == 0)
        return ImmutableArray<CustomAttributeNamedArgument<TType>>.Empty;
      ImmutableArray<CustomAttributeNamedArgument<TType>>.Builder builder = ImmutableArray.CreateBuilder<CustomAttributeNamedArgument<TType>>(initialCapacity);
      for (int index = 0; index < initialCapacity; ++index)
      {
        CustomAttributeNamedArgumentKind kind = (CustomAttributeNamedArgumentKind) valueReader.ReadSerializationTypeCode();
        switch (kind)
        {
          case CustomAttributeNamedArgumentKind.Field:
          case CustomAttributeNamedArgumentKind.Property:
            CustomAttributeDecoder<TType>.ArgumentTypeInfo info = this.DecodeNamedArgumentType(ref valueReader);
            string name = valueReader.ReadSerializedString();
            CustomAttributeTypedArgument<TType> attributeTypedArgument = this.DecodeArgument(ref valueReader, info);
            builder.Add(new CustomAttributeNamedArgument<TType>(name, kind, attributeTypedArgument.Type, attributeTypedArgument.Value));
            continue;
          default:
            throw new BadImageFormatException();
        }
      }
      return builder.MoveToImmutable();
    }

    private CustomAttributeDecoder<TType>.ArgumentTypeInfo DecodeFixedArgumentType(
      ref BlobReader signatureReader,
      BlobReader genericContextReader,
      bool isElementType = false)
    {
      SignatureTypeCode typeCode = signatureReader.ReadSignatureTypeCode();
      CustomAttributeDecoder<TType>.ArgumentTypeInfo argumentTypeInfo1 = new CustomAttributeDecoder<TType>.ArgumentTypeInfo()
      {
        TypeCode = (SerializationTypeCode) typeCode
      };
      switch (typeCode)
      {
        case SignatureTypeCode.Boolean:
        case SignatureTypeCode.Char:
        case SignatureTypeCode.SByte:
        case SignatureTypeCode.Byte:
        case SignatureTypeCode.Int16:
        case SignatureTypeCode.UInt16:
        case SignatureTypeCode.Int32:
        case SignatureTypeCode.UInt32:
        case SignatureTypeCode.Int64:
        case SignatureTypeCode.UInt64:
        case SignatureTypeCode.Single:
        case SignatureTypeCode.Double:
        case SignatureTypeCode.String:
          argumentTypeInfo1.Type = this._provider.GetPrimitiveType((PrimitiveTypeCode) typeCode);
          break;
        case SignatureTypeCode.GenericTypeParameter:
          if (genericContextReader.Length == 0)
            throw new BadImageFormatException();
          int num1 = signatureReader.ReadCompressedInteger();
          int num2 = genericContextReader.ReadCompressedInteger();
          if (num1 >= num2)
            throw new BadImageFormatException();
          for (; num1 > 0; --num1)
            CustomAttributeDecoder<TType>.SkipType(ref genericContextReader);
          return this.DecodeFixedArgumentType(ref genericContextReader, new BlobReader(), isElementType);
        case SignatureTypeCode.Object:
          argumentTypeInfo1.TypeCode = SerializationTypeCode.TaggedObject;
          argumentTypeInfo1.Type = this._provider.GetPrimitiveType(PrimitiveTypeCode.Object);
          break;
        case SignatureTypeCode.SZArray:
          if (isElementType)
            throw new BadImageFormatException();
          CustomAttributeDecoder<TType>.ArgumentTypeInfo argumentTypeInfo2 = this.DecodeFixedArgumentType(ref signatureReader, genericContextReader, true);
          argumentTypeInfo1.ElementType = argumentTypeInfo2.Type;
          argumentTypeInfo1.ElementTypeCode = argumentTypeInfo2.TypeCode;
          argumentTypeInfo1.Type = this._provider.GetSZArrayType(argumentTypeInfo1.ElementType);
          break;
        case SignatureTypeCode.TypeHandle:
          EntityHandle handle = signatureReader.ReadTypeHandle();
          argumentTypeInfo1.Type = this.GetTypeFromHandle(handle);
          argumentTypeInfo1.TypeCode = this._provider.IsSystemType(argumentTypeInfo1.Type) ? SerializationTypeCode.Type : (SerializationTypeCode) this._provider.GetUnderlyingEnumType(argumentTypeInfo1.Type);
          break;
        default:
          throw new BadImageFormatException();
      }
      return argumentTypeInfo1;
    }

    private CustomAttributeDecoder<TType>.ArgumentTypeInfo DecodeNamedArgumentType(
      ref BlobReader valueReader,
      bool isElementType = false)
    {
      CustomAttributeDecoder<TType>.ArgumentTypeInfo argumentTypeInfo1 = new CustomAttributeDecoder<TType>.ArgumentTypeInfo()
      {
        TypeCode = valueReader.ReadSerializationTypeCode()
      };
      SerializationTypeCode typeCode = argumentTypeInfo1.TypeCode;
      if ((uint) typeCode <= 29U)
      {
        if ((uint) (typeCode - (byte) 2) > 12U)
        {
          if (typeCode == SerializationTypeCode.SZArray)
          {
            if (isElementType)
              throw new BadImageFormatException();
            CustomAttributeDecoder<TType>.ArgumentTypeInfo argumentTypeInfo2 = this.DecodeNamedArgumentType(ref valueReader, true);
            argumentTypeInfo1.ElementType = argumentTypeInfo2.Type;
            argumentTypeInfo1.ElementTypeCode = argumentTypeInfo2.TypeCode;
            argumentTypeInfo1.Type = this._provider.GetSZArrayType(argumentTypeInfo1.ElementType);
            goto label_12;
          }
        }
        else
        {
          argumentTypeInfo1.Type = this._provider.GetPrimitiveType((PrimitiveTypeCode) argumentTypeInfo1.TypeCode);
          goto label_12;
        }
      }
      else
      {
        switch (typeCode)
        {
          case SerializationTypeCode.Type:
            argumentTypeInfo1.Type = this._provider.GetSystemType();
            goto label_12;
          case SerializationTypeCode.TaggedObject:
            argumentTypeInfo1.Type = this._provider.GetPrimitiveType(PrimitiveTypeCode.Object);
            goto label_12;
          case SerializationTypeCode.Enum:
            string name = valueReader.ReadSerializedString();
            argumentTypeInfo1.Type = this._provider.GetTypeFromSerializedName(name);
            argumentTypeInfo1.TypeCode = (SerializationTypeCode) this._provider.GetUnderlyingEnumType(argumentTypeInfo1.Type);
            goto label_12;
        }
      }
      throw new BadImageFormatException();
label_12:
      return argumentTypeInfo1;
    }

    private CustomAttributeTypedArgument<TType> DecodeArgument(
      ref BlobReader valueReader,
      CustomAttributeDecoder<TType>.ArgumentTypeInfo info)
    {
      if (info.TypeCode == SerializationTypeCode.TaggedObject)
        info = this.DecodeNamedArgumentType(ref valueReader);
      object obj;
      switch (info.TypeCode)
      {
        case SerializationTypeCode.Boolean:
          obj = (object) valueReader.ReadBoolean();
          break;
        case SerializationTypeCode.Char:
          obj = (object) valueReader.ReadChar();
          break;
        case SerializationTypeCode.SByte:
          obj = (object) valueReader.ReadSByte();
          break;
        case SerializationTypeCode.Byte:
          obj = (object) valueReader.ReadByte();
          break;
        case SerializationTypeCode.Int16:
          obj = (object) valueReader.ReadInt16();
          break;
        case SerializationTypeCode.UInt16:
          obj = (object) valueReader.ReadUInt16();
          break;
        case SerializationTypeCode.Int32:
          obj = (object) valueReader.ReadInt32();
          break;
        case SerializationTypeCode.UInt32:
          obj = (object) valueReader.ReadUInt32();
          break;
        case SerializationTypeCode.Int64:
          obj = (object) valueReader.ReadInt64();
          break;
        case SerializationTypeCode.UInt64:
          obj = (object) valueReader.ReadUInt64();
          break;
        case SerializationTypeCode.Single:
          obj = (object) valueReader.ReadSingle();
          break;
        case SerializationTypeCode.Double:
          obj = (object) valueReader.ReadDouble();
          break;
        case SerializationTypeCode.String:
          obj = (object) valueReader.ReadSerializedString();
          break;
        case SerializationTypeCode.SZArray:
          obj = (object) this.DecodeArrayArgument(ref valueReader, info);
          break;
        case SerializationTypeCode.Type:
          obj = (object) this._provider.GetTypeFromSerializedName(valueReader.ReadSerializedString());
          break;
        default:
          throw new BadImageFormatException();
      }
      return new CustomAttributeTypedArgument<TType>(info.Type, obj);
    }

    private ImmutableArray<CustomAttributeTypedArgument<TType>>? DecodeArrayArgument(
      ref BlobReader blobReader,
      CustomAttributeDecoder<TType>.ArgumentTypeInfo info)
    {
      int initialCapacity = blobReader.ReadInt32();
      switch (initialCapacity)
      {
        case -1:
          return new ImmutableArray<CustomAttributeTypedArgument<TType>>?();
        case 0:
          return new ImmutableArray<CustomAttributeTypedArgument<TType>>?(ImmutableArray<CustomAttributeTypedArgument<TType>>.Empty);
        default:
          if (initialCapacity < 0)
            throw new BadImageFormatException();
          CustomAttributeDecoder<TType>.ArgumentTypeInfo info1 = new CustomAttributeDecoder<TType>.ArgumentTypeInfo()
          {
            Type = info.ElementType,
            TypeCode = info.ElementTypeCode
          };
          ImmutableArray<CustomAttributeTypedArgument<TType>>.Builder builder = ImmutableArray.CreateBuilder<CustomAttributeTypedArgument<TType>>(initialCapacity);
          for (int index = 0; index < initialCapacity; ++index)
            builder.Add(this.DecodeArgument(ref blobReader, info1));
          return new ImmutableArray<CustomAttributeTypedArgument<TType>>?(builder.MoveToImmutable());
      }
    }

    private TType GetTypeFromHandle(EntityHandle handle)
    {
      switch (handle.Kind)
      {
        case HandleKind.TypeReference:
          return this._provider.GetTypeFromReference(this._reader, (TypeReferenceHandle) handle, (byte) 0);
        case HandleKind.TypeDefinition:
          return this._provider.GetTypeFromDefinition(this._reader, (TypeDefinitionHandle) handle, (byte) 0);
        default:
          throw new BadImageFormatException(SR.NotTypeDefOrRefHandle);
      }
    }

    private static void SkipType(ref BlobReader blobReader)
    {
      switch (blobReader.ReadCompressedInteger())
      {
        case 1:
          break;
        case 2:
          break;
        case 3:
          break;
        case 4:
          break;
        case 5:
          break;
        case 6:
          break;
        case 7:
          break;
        case 8:
          break;
        case 9:
          break;
        case 10:
          break;
        case 11:
          break;
        case 12:
          break;
        case 13:
          break;
        case 14:
          break;
        case 15:
        case 16:
        case 29:
        case 69:
          CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          break;
        case 17:
        case 18:
          CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          break;
        case 19:
          blobReader.ReadCompressedInteger();
          break;
        case 20:
          CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          blobReader.ReadCompressedInteger();
          int num1 = blobReader.ReadCompressedInteger();
          for (int index = 0; index < num1; ++index)
            blobReader.ReadCompressedInteger();
          int num2 = blobReader.ReadCompressedInteger();
          for (int index = 0; index < num2; ++index)
            blobReader.ReadCompressedSignedInteger();
          break;
        case 21:
          CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          int num3 = blobReader.ReadCompressedInteger();
          for (int index = 0; index < num3; ++index)
            CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          break;
        case 22:
          break;
        case 24:
          break;
        case 25:
          break;
        case 27:
          if (blobReader.ReadSignatureHeader().IsGeneric)
            blobReader.ReadCompressedInteger();
          int num4 = blobReader.ReadCompressedInteger();
          CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          for (int index = 0; index < num4; ++index)
            CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          break;
        case 28:
          break;
        case 31:
        case 32:
          blobReader.ReadTypeHandle();
          CustomAttributeDecoder<TType>.SkipType(ref blobReader);
          break;
        default:
          throw new BadImageFormatException();
      }
    }

    private struct ArgumentTypeInfo
    {
      public TType Type;
      public TType ElementType;
      public SerializationTypeCode TypeCode;
      public SerializationTypeCode ElementTypeCode;
    }
  }
}

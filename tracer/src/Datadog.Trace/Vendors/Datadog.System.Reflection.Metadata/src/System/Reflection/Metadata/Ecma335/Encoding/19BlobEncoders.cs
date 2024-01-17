﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.SignatureTypeEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct SignatureTypeEncoder
  {
    public BlobBuilder Builder { get; }

    public SignatureTypeEncoder(BlobBuilder builder) => this.Builder = builder;

    private void WriteTypeCode(SignatureTypeCode value) => this.Builder.WriteByte((byte) value);

    private void ClassOrValue(bool isValueType) => this.Builder.WriteByte(isValueType ? (byte) 17 : (byte) 18);

    public void Boolean() => this.WriteTypeCode(SignatureTypeCode.Boolean);

    public void Char() => this.WriteTypeCode(SignatureTypeCode.Char);

    public void SByte() => this.WriteTypeCode(SignatureTypeCode.SByte);

    public void Byte() => this.WriteTypeCode(SignatureTypeCode.Byte);

    public void Int16() => this.WriteTypeCode(SignatureTypeCode.Int16);

    public void UInt16() => this.WriteTypeCode(SignatureTypeCode.UInt16);

    public void Int32() => this.WriteTypeCode(SignatureTypeCode.Int32);

    public void UInt32() => this.WriteTypeCode(SignatureTypeCode.UInt32);

    public void Int64() => this.WriteTypeCode(SignatureTypeCode.Int64);

    public void UInt64() => this.WriteTypeCode(SignatureTypeCode.UInt64);

    public void Single() => this.WriteTypeCode(SignatureTypeCode.Single);

    public void Double() => this.WriteTypeCode(SignatureTypeCode.Double);

    public void String() => this.WriteTypeCode(SignatureTypeCode.String);

    public void IntPtr() => this.WriteTypeCode(SignatureTypeCode.IntPtr);

    public void UIntPtr() => this.WriteTypeCode(SignatureTypeCode.UIntPtr);

    public void Object() => this.WriteTypeCode(SignatureTypeCode.Object);

    /// <summary>Writes primitive type code.</summary>
    /// <param name="type">Any primitive type code except for <see cref="F:System.Reflection.Metadata.PrimitiveTypeCode.TypedReference" /> and <see cref="F:System.Reflection.Metadata.PrimitiveTypeCode.Void" />.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="type" /> is not valid in this context.</exception>
    public void PrimitiveType(PrimitiveTypeCode type)
    {
      switch (type)
      {
        case PrimitiveTypeCode.Boolean:
        case PrimitiveTypeCode.Char:
        case PrimitiveTypeCode.SByte:
        case PrimitiveTypeCode.Byte:
        case PrimitiveTypeCode.Int16:
        case PrimitiveTypeCode.UInt16:
        case PrimitiveTypeCode.Int32:
        case PrimitiveTypeCode.UInt32:
        case PrimitiveTypeCode.Int64:
        case PrimitiveTypeCode.UInt64:
        case PrimitiveTypeCode.Single:
        case PrimitiveTypeCode.Double:
        case PrimitiveTypeCode.String:
        case PrimitiveTypeCode.IntPtr:
        case PrimitiveTypeCode.UIntPtr:
        case PrimitiveTypeCode.Object:
          this.Builder.WriteByte((byte) type);
          break;
        default:
          Throw.ArgumentOutOfRange(nameof (type));
          break;
      }
    }

    /// <summary>
    /// Encodes an array type.
    /// Returns a pair of encoders that must be used in the order they appear in the parameter list.
    /// </summary>
    /// <param name="elementType">Use first, to encode the type of the element.</param>
    /// <param name="arrayShape">Use second, to encode the shape of the array.</param>
    public void Array(out SignatureTypeEncoder elementType, out ArrayShapeEncoder arrayShape)
    {
      this.Builder.WriteByte((byte) 20);
      elementType = this;
      arrayShape = new ArrayShapeEncoder(this.Builder);
    }

    /// <summary>Encodes an array type.</summary>
    /// <param name="elementType">Called first, to encode the type of the element.</param>
    /// <param name="arrayShape">Called second, to encode the shape of the array.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="elementType" /> or <paramref name="arrayShape" /> is null.</exception>
    public void Array(
      Action<SignatureTypeEncoder> elementType,
      Action<ArrayShapeEncoder> arrayShape)
    {
      if (elementType == null)
        Throw.ArgumentNull(nameof (elementType));
      if (arrayShape == null)
        Throw.ArgumentNull(nameof (arrayShape));
      SignatureTypeEncoder elementType1;
      ArrayShapeEncoder arrayShape1;
      this.Array(out elementType1, out arrayShape1);
      elementType(elementType1);
      arrayShape(arrayShape1);
    }

    /// <summary>Encodes a reference to a type.</summary>
    /// <param name="type"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />.</param>
    /// <param name="isValueType">True to mark the type as value type, false to mark it as a reference type in the signature.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="type" /> doesn't have the expected handle kind.</exception>
    public void Type(EntityHandle type, bool isValueType)
    {
      int num = CodedIndex.TypeDefOrRef(type);
      this.ClassOrValue(isValueType);
      this.Builder.WriteCompressedInteger(num);
    }

    /// <summary>Starts a function pointer signature.</summary>
    /// <param name="convention">Calling convention.</param>
    /// <param name="attributes">Function pointer attributes.</param>
    /// <param name="genericParameterCount">Generic parameter count.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="attributes" /> is invalid.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="genericParameterCount" /> is not in range [0, 0xffff].</exception>
    public MethodSignatureEncoder FunctionPointer(
      SignatureCallingConvention convention = SignatureCallingConvention.Default,
      FunctionPointerAttributes attributes = FunctionPointerAttributes.None,
      int genericParameterCount = 0)
    {
      if (attributes != FunctionPointerAttributes.None && attributes != FunctionPointerAttributes.HasThis && attributes != FunctionPointerAttributes.HasExplicitThis)
        throw new ArgumentException(SR.InvalidSignature, nameof (attributes));
      if ((uint) genericParameterCount > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (genericParameterCount));
      this.Builder.WriteByte((byte) 27);
      this.Builder.WriteByte(new SignatureHeader(SignatureKind.Method, convention, (SignatureAttributes) attributes).RawValue);
      if (genericParameterCount != 0)
        this.Builder.WriteCompressedInteger(genericParameterCount);
      return new MethodSignatureEncoder(this.Builder, convention == SignatureCallingConvention.VarArgs);
    }

    /// <summary>Starts a generic instantiation signature.</summary>
    /// <param name="genericType"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />.</param>
    /// <param name="genericArgumentCount">Generic argument count.</param>
    /// <param name="isValueType">True to mark the type as value type, false to mark it as a reference type in the signature.</param>
    /// <exception cref="T:System.ArgumentException"><paramref name="genericType" /> doesn't have the expected handle kind.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="genericArgumentCount" /> is not in range [1, 0xffff].</exception>
    public GenericTypeArgumentsEncoder GenericInstantiation(
      EntityHandle genericType,
      int genericArgumentCount,
      bool isValueType)
    {
      if ((uint) (genericArgumentCount - 1) > 65534U)
        Throw.ArgumentOutOfRange(nameof (genericArgumentCount));
      int num = CodedIndex.TypeDefOrRef(genericType);
      this.Builder.WriteByte((byte) 21);
      this.ClassOrValue(isValueType);
      this.Builder.WriteCompressedInteger(num);
      this.Builder.WriteCompressedInteger(genericArgumentCount);
      return new GenericTypeArgumentsEncoder(this.Builder);
    }

    /// <summary>
    /// Encodes a reference to type parameter of a containing generic method.
    /// </summary>
    /// <param name="parameterIndex">Parameter index.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="parameterIndex" /> is not in range [0, 0xffff].</exception>
    public void GenericMethodTypeParameter(int parameterIndex)
    {
      if ((uint) parameterIndex > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (parameterIndex));
      this.Builder.WriteByte((byte) 30);
      this.Builder.WriteCompressedInteger(parameterIndex);
    }

    /// <summary>
    /// Encodes a reference to type parameter of a containing generic type.
    /// </summary>
    /// <param name="parameterIndex">Parameter index.</param>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="parameterIndex" /> is not in range [0, 0xffff].</exception>
    public void GenericTypeParameter(int parameterIndex)
    {
      if ((uint) parameterIndex > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (parameterIndex));
      this.Builder.WriteByte((byte) 19);
      this.Builder.WriteCompressedInteger(parameterIndex);
    }

    /// <summary>Starts pointer signature.</summary>
    public SignatureTypeEncoder Pointer()
    {
      this.Builder.WriteByte((byte) 15);
      return this;
    }

    /// <summary>
    /// Encodes <code>void*</code>.
    /// </summary>
    public void VoidPointer()
    {
      this.Builder.WriteByte((byte) 15);
      this.Builder.WriteByte((byte) 1);
    }

    /// <summary>Starts SZ array (vector) signature.</summary>
    public SignatureTypeEncoder SZArray()
    {
      this.Builder.WriteByte((byte) 29);
      return this;
    }

    /// <summary>Starts a signature of a type with custom modifiers.</summary>
    public CustomModifiersEncoder CustomModifiers() => new CustomModifiersEncoder(this.Builder);
  }
}

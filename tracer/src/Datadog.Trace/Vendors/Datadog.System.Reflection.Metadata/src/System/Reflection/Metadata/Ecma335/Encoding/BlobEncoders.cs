﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.BlobEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct BlobEncoder
  {
    public BlobBuilder Builder { get; }

    public BlobEncoder(BlobBuilder builder)
    {
      if (builder == null)
        Throw.ArgumentNull(nameof (builder));
      this.Builder = builder;
    }

    /// <summary>
    /// Encodes Field Signature blob, with additional support for
    /// encoding ref fields, custom modifiers and typed references.
    /// </summary>
    /// <returns>Encoder of the field type.</returns>
    public FieldTypeEncoder Field()
    {
      this.Builder.WriteByte((byte) 6);
      return new FieldTypeEncoder(this.Builder);
    }

    /// <summary>Encodes Field Signature blob.</summary>
    /// <returns>Encoder of the field type.</returns>
    /// <remarks>To encode byref fields, custom modifiers or typed
    /// references use <see cref="M:System.Reflection.Metadata.Ecma335.BlobEncoder.Field" /> instead.</remarks>
    public SignatureTypeEncoder FieldSignature() => this.Field().Type();

    /// <summary>Encodes Method Specification Signature blob.</summary>
    /// <param name="genericArgumentCount">Number of generic arguments.</param>
    /// <returns>Encoder of generic arguments.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="genericArgumentCount" /> is not in range [0, 0xffff].</exception>
    public GenericTypeArgumentsEncoder MethodSpecificationSignature(int genericArgumentCount)
    {
      if ((uint) genericArgumentCount > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (genericArgumentCount));
      this.Builder.WriteByte((byte) 10);
      this.Builder.WriteCompressedInteger(genericArgumentCount);
      return new GenericTypeArgumentsEncoder(this.Builder);
    }

    /// <summary>Encodes Method Signature blob.</summary>
    /// <param name="convention">Calling convention.</param>
    /// <param name="genericParameterCount">Number of generic parameters.</param>
    /// <param name="isInstanceMethod">True to encode an instance method signature, false to encode a static method signature.</param>
    /// <returns>An Encoder of the rest of the signature including return value and parameters.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="genericParameterCount" /> is not in range [0, 0xffff].</exception>
    public MethodSignatureEncoder MethodSignature(
      SignatureCallingConvention convention = SignatureCallingConvention.Default,
      int genericParameterCount = 0,
      bool isInstanceMethod = false)
    {
      if ((uint) genericParameterCount > (uint) ushort.MaxValue)
        Throw.ArgumentOutOfRange(nameof (genericParameterCount));
      SignatureAttributes attributes = (SignatureAttributes) ((genericParameterCount != 0 ? 16 : 0) | (isInstanceMethod ? 32 : 0));
      this.Builder.WriteByte(new SignatureHeader(SignatureKind.Method, convention, attributes).RawValue);
      if (genericParameterCount != 0)
        this.Builder.WriteCompressedInteger(genericParameterCount);
      return new MethodSignatureEncoder(this.Builder, convention == SignatureCallingConvention.VarArgs);
    }

    /// <summary>Encodes Property Signature blob.</summary>
    /// <param name="isInstanceProperty">True to encode an instance property signature, false to encode a static property signature.</param>
    /// <returns>An Encoder of the rest of the signature including return value and parameters, which has the same structure as Method Signature.</returns>
    public MethodSignatureEncoder PropertySignature(bool isInstanceProperty = false)
    {
      this.Builder.WriteByte(new SignatureHeader(SignatureKind.Property, SignatureCallingConvention.Default, isInstanceProperty ? SignatureAttributes.Instance : SignatureAttributes.None).RawValue);
      return new MethodSignatureEncoder(this.Builder, false);
    }

    /// <summary>
    /// Encodes Custom Attribute Signature blob.
    /// Returns a pair of encoders that must be used in the order they appear in the parameter list.
    /// </summary>
    /// <param name="fixedArguments">Use first, to encode fixed arguments.</param>
    /// <param name="namedArguments">Use second, to encode named arguments.</param>
    public void CustomAttributeSignature(
      out FixedArgumentsEncoder fixedArguments,
      out CustomAttributeNamedArgumentsEncoder namedArguments)
    {
      this.Builder.WriteUInt16((ushort) 1);
      fixedArguments = new FixedArgumentsEncoder(this.Builder);
      namedArguments = new CustomAttributeNamedArgumentsEncoder(this.Builder);
    }

    /// <summary>Encodes Custom Attribute Signature blob.</summary>
    /// <param name="fixedArguments">Called first, to encode fixed arguments.</param>
    /// <param name="namedArguments">Called second, to encode named arguments.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="fixedArguments" /> or <paramref name="namedArguments" /> is null.</exception>
    public void CustomAttributeSignature(
      Action<FixedArgumentsEncoder> fixedArguments,
      Action<CustomAttributeNamedArgumentsEncoder> namedArguments)
    {
      if (fixedArguments == null)
        Throw.ArgumentNull(nameof (fixedArguments));
      if (namedArguments == null)
        Throw.ArgumentNull(nameof (namedArguments));
      FixedArgumentsEncoder fixedArguments1;
      CustomAttributeNamedArgumentsEncoder namedArguments1;
      this.CustomAttributeSignature(out fixedArguments1, out namedArguments1);
      fixedArguments(fixedArguments1);
      namedArguments(namedArguments1);
    }

    /// <summary>Encodes Local Variable Signature.</summary>
    /// <param name="variableCount">Number of local variables.</param>
    /// <returns>Encoder of a sequence of local variables.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="variableCount" /> is not in range [0, 0x1fffffff].</exception>
    public LocalVariablesEncoder LocalVariableSignature(int variableCount)
    {
      if ((uint) variableCount > 536870911U)
        Throw.ArgumentOutOfRange(nameof (variableCount));
      this.Builder.WriteByte((byte) 7);
      this.Builder.WriteCompressedInteger(variableCount);
      return new LocalVariablesEncoder(this.Builder);
    }

    /// <summary>Encodes Type Specification Signature.</summary>
    /// <returns>
    /// Type encoder of the structured type represented by the Type Specification (it shall not encode a primitive type).
    /// </returns>
    public SignatureTypeEncoder TypeSpecificationSignature() => new SignatureTypeEncoder(this.Builder);

    /// <summary>Encodes a Permission Set blob.</summary>
    /// <param name="attributeCount">Number of attributes in the set.</param>
    /// <returns>Permission Set encoder.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="attributeCount" /> is not in range [0, 0x1fffffff].</exception>
    public PermissionSetEncoder PermissionSetBlob(int attributeCount)
    {
      if ((uint) attributeCount > 536870911U)
        Throw.ArgumentOutOfRange(nameof (attributeCount));
      this.Builder.WriteByte((byte) 46);
      this.Builder.WriteCompressedInteger(attributeCount);
      return new PermissionSetEncoder(this.Builder);
    }

    /// <summary>Encodes Permission Set arguments.</summary>
    /// <param name="argumentCount">Number of arguments in the set.</param>
    /// <returns>Encoder of the arguments of the set.</returns>
    public NamedArgumentsEncoder PermissionSetArguments(int argumentCount)
    {
      if ((uint) argumentCount > 536870911U)
        Throw.ArgumentOutOfRange(nameof (argumentCount));
      this.Builder.WriteCompressedInteger(argumentCount);
      return new NamedArgumentsEncoder(this.Builder);
    }
  }
}

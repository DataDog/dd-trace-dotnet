// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.SignatureHeader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Text;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    /// <summary>
    /// Represents the signature characteristics specified by the leading byte of signature blobs.
    /// </summary>
    /// <remarks>
    /// This header byte is present in all method definition, method reference, standalone method, field,
    /// property, and local variable signatures, but not in type specification signatures.
    /// </remarks>
    public struct SignatureHeader : IEquatable<SignatureHeader>
  {
    private readonly byte _rawValue;
    public const byte CallingConventionOrKindMask = 15;
    private const byte maxCallingConvention = 5;

    public SignatureHeader(byte rawValue) => this._rawValue = rawValue;

    public SignatureHeader(
      SignatureKind kind,
      SignatureCallingConvention convention,
      SignatureAttributes attributes)
      : this((byte) (kind | (SignatureKind) convention | (SignatureKind) attributes))
    {
    }

    public byte RawValue => this._rawValue;

    public SignatureCallingConvention CallingConvention
    {
      get
      {
        int num = (int) this._rawValue & 15;
        return num > 5 && num != 9 ? SignatureCallingConvention.Default : (SignatureCallingConvention) num;
      }
    }

    public SignatureKind Kind
    {
      get
      {
        int num = (int) this._rawValue & 15;
        return num <= 5 || num == 9 ? SignatureKind.Method : (SignatureKind) num;
      }
    }

    public SignatureAttributes Attributes => (SignatureAttributes) ((uint) this._rawValue & 4294967280U);

    public bool HasExplicitThis => ((uint) this._rawValue & 64U) > 0U;

    public bool IsInstance => ((uint) this._rawValue & 32U) > 0U;

    public bool IsGeneric => ((uint) this._rawValue & 16U) > 0U;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is SignatureHeader other && this.Equals(other);

    public bool Equals(SignatureHeader other) => (int) this._rawValue == (int) other._rawValue;

    public override int GetHashCode() => (int) this._rawValue;

    public static bool operator ==(SignatureHeader left, SignatureHeader right) => (int) left._rawValue == (int) right._rawValue;

    public static bool operator !=(SignatureHeader left, SignatureHeader right) => (int) left._rawValue != (int) right._rawValue;

    public override string ToString()
    {
      StringBuilder stringBuilder = new StringBuilder();
      stringBuilder.Append(this.Kind.ToString());
      if (this.Kind == SignatureKind.Method)
      {
        stringBuilder.Append(',');
        stringBuilder.Append(this.CallingConvention.ToString());
      }
      if (this.Attributes != SignatureAttributes.None)
      {
        stringBuilder.Append(',');
        stringBuilder.Append(this.Attributes.ToString());
      }
      return stringBuilder.ToString();
    }
  }
}

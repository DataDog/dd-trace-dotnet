// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CustomAttributeElementTypeEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct CustomAttributeElementTypeEncoder
  {
    public BlobBuilder Builder { get; }

    public CustomAttributeElementTypeEncoder(BlobBuilder builder) => this.Builder = builder;

    private void WriteTypeCode(SerializationTypeCode value) => this.Builder.WriteByte((byte) value);

    public void Boolean() => this.WriteTypeCode(SerializationTypeCode.Boolean);

    public void Char() => this.WriteTypeCode(SerializationTypeCode.Char);

    public void SByte() => this.WriteTypeCode(SerializationTypeCode.SByte);

    public void Byte() => this.WriteTypeCode(SerializationTypeCode.Byte);

    public void Int16() => this.WriteTypeCode(SerializationTypeCode.Int16);

    public void UInt16() => this.WriteTypeCode(SerializationTypeCode.UInt16);

    public void Int32() => this.WriteTypeCode(SerializationTypeCode.Int32);

    public void UInt32() => this.WriteTypeCode(SerializationTypeCode.UInt32);

    public void Int64() => this.WriteTypeCode(SerializationTypeCode.Int64);

    public void UInt64() => this.WriteTypeCode(SerializationTypeCode.UInt64);

    public void Single() => this.WriteTypeCode(SerializationTypeCode.Single);

    public void Double() => this.WriteTypeCode(SerializationTypeCode.Double);

    public void String() => this.WriteTypeCode(SerializationTypeCode.String);

    public void PrimitiveType(PrimitiveSerializationTypeCode type)
    {
      if ((uint) (type - (byte) 2) <= 12U)
        this.WriteTypeCode((SerializationTypeCode) type);
      else
        Throw.ArgumentOutOfRange(nameof (type));
    }

    public void SystemType() => this.WriteTypeCode(SerializationTypeCode.Type);

    public void Enum(string enumTypeName)
    {
      if (enumTypeName == null)
        Throw.ArgumentNull(nameof (enumTypeName));
      if (enumTypeName.Length == 0)
        Throw.ArgumentEmptyString(nameof (enumTypeName));
      this.WriteTypeCode(SerializationTypeCode.Enum);
      this.Builder.WriteSerializedString(enumTypeName);
    }
  }
}

// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CustomModifiersEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct CustomModifiersEncoder
  {
    public BlobBuilder Builder { get; }

    public CustomModifiersEncoder(BlobBuilder builder) => this.Builder = builder;

    /// <summary>Encodes a custom modifier.</summary>
    /// <param name="type"><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />.</param>
    /// <param name="isOptional">Is optional modifier.</param>
    /// <returns>Encoder of subsequent modifiers.</returns>
    /// <exception cref="T:System.ArgumentException"><paramref name="type" /> is nil or of an unexpected kind.</exception>
    public CustomModifiersEncoder AddModifier(EntityHandle type, bool isOptional)
    {
      if (type.IsNil)
        Throw.InvalidArgument_Handle(nameof (type));
      if (isOptional)
        this.Builder.WriteByte((byte) 32);
      else
        this.Builder.WriteByte((byte) 31);
      this.Builder.WriteCompressedInteger(CodedIndex.TypeDefOrRefOrSpec(type));
      return this;
    }
  }
}

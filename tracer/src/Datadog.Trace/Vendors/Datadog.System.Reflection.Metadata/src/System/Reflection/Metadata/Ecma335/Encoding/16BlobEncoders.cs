// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.NamedArgumentTypeEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct NamedArgumentTypeEncoder
  {
    public BlobBuilder Builder { get; }

    public NamedArgumentTypeEncoder(BlobBuilder builder) => this.Builder = builder;

    public CustomAttributeElementTypeEncoder ScalarType() => new CustomAttributeElementTypeEncoder(this.Builder);

    public void Object() => this.Builder.WriteByte((byte) 81);

    public CustomAttributeArrayTypeEncoder SZArray() => new CustomAttributeArrayTypeEncoder(this.Builder);
  }
}

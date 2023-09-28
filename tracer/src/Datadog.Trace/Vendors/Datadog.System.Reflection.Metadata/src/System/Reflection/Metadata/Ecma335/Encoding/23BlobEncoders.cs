// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ParametersEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct ParametersEncoder
  {
    public BlobBuilder Builder { get; }

    public bool HasVarArgs { get; }

    public ParametersEncoder(BlobBuilder builder, bool hasVarArgs = false)
    {
      this.Builder = builder;
      this.HasVarArgs = hasVarArgs;
    }

    public ParameterTypeEncoder AddParameter() => new ParameterTypeEncoder(this.Builder);

    public ParametersEncoder StartVarArgs()
    {
      if (!this.HasVarArgs)
        Throw.SignatureNotVarArg();
      this.Builder.WriteByte((byte) 65);
      return new ParametersEncoder(this.Builder);
    }
  }
}

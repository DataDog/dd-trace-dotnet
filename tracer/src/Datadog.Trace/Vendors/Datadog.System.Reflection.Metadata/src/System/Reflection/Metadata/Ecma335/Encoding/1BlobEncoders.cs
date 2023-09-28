// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MethodSignatureEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct MethodSignatureEncoder
  {
    public BlobBuilder Builder { get; }

    public bool HasVarArgs { get; }

    public MethodSignatureEncoder(BlobBuilder builder, bool hasVarArgs)
    {
      this.Builder = builder;
      this.HasVarArgs = hasVarArgs;
    }

    /// <summary>
    /// Encodes return type and parameters.
    /// Returns a pair of encoders that must be used in the order they appear in the parameter list.
    /// </summary>
    /// <param name="parameterCount">Number of parameters.</param>
    /// <param name="returnType">Use first, to encode the return types.</param>
    /// <param name="parameters">Use second, to encode the actual parameters.</param>
    public void Parameters(
      int parameterCount,
      out ReturnTypeEncoder returnType,
      out ParametersEncoder parameters)
    {
      if ((uint) parameterCount > 536870911U)
        Throw.ArgumentOutOfRange(nameof (parameterCount));
      this.Builder.WriteCompressedInteger(parameterCount);
      returnType = new ReturnTypeEncoder(this.Builder);
      parameters = new ParametersEncoder(this.Builder, this.HasVarArgs);
    }

    /// <summary>Encodes return type and parameters.</summary>
    /// <param name="parameterCount">Number of parameters.</param>
    /// <param name="returnType">Called first, to encode the return type.</param>
    /// <param name="parameters">Called second, to encode the actual parameters.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="returnType" /> or <paramref name="parameters" /> is null.</exception>
    public void Parameters(
      int parameterCount,
      Action<ReturnTypeEncoder> returnType,
      Action<ParametersEncoder> parameters)
    {
      if (returnType == null)
        Throw.ArgumentNull(nameof (returnType));
      if (parameters == null)
        Throw.ArgumentNull(nameof (parameters));
      ReturnTypeEncoder returnType1;
      ParametersEncoder parameters1;
      this.Parameters(parameterCount, out returnType1, out parameters1);
      returnType(returnType1);
      parameters(parameters1);
    }
  }
}

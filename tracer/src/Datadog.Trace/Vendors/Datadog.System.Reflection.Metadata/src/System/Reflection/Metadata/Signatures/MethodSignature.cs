// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodSignature`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Represents a method (definition, reference, or standalone) or property signature.
  /// In the case of properties, the signature matches that of a getter with a distinguishing <see cref="T:System.Reflection.Metadata.SignatureHeader" />.
  /// </summary>
  public readonly struct MethodSignature<TType>
  {
    /// <summary>
    /// Represents the information in the leading byte of the signature (kind, calling convention, flags).
    /// </summary>
    public SignatureHeader Header { get; }

    /// <summary>Gets the method's return type.</summary>
    public TType ReturnType { get; }

    /// <summary>
    /// Gets the number of parameters that are required. Will be equal to the length <see cref="P:System.Reflection.Metadata.MethodSignature`1.ParameterTypes" /> of
    /// unless this signature represents the standalone call site of a vararg method, in which case the entries
    /// extra entries in <see cref="P:System.Reflection.Metadata.MethodSignature`1.ParameterTypes" /> are the types used for the optional parameters.
    /// </summary>
    public int RequiredParameterCount { get; }

    /// <summary>
    /// Gets the number of generic type parameters of the method. Will be 0 for non-generic methods.
    /// </summary>
    public int GenericParameterCount { get; }

    /// <summary>Gets the method's parameter types.</summary>
    public ImmutableArray<TType> ParameterTypes { get; }

    public MethodSignature(
      SignatureHeader header,
      TType returnType,
      int requiredParameterCount,
      int genericParameterCount,
      ImmutableArray<TType> parameterTypes)
    {
      this.Header = header;
      this.ReturnType = returnType;
      this.GenericParameterCount = genericParameterCount;
      this.RequiredParameterCount = requiredParameterCount;
      this.ParameterTypes = parameterTypes;
    }
  }
}

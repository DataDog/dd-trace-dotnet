// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.CustomAttributeValue`1
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct CustomAttributeValue<TType>
  {
    public ImmutableArray<CustomAttributeTypedArgument<TType>> FixedArguments { get; }

    public ImmutableArray<CustomAttributeNamedArgument<TType>> NamedArguments { get; }

    public CustomAttributeValue(
      ImmutableArray<CustomAttributeTypedArgument<TType>> fixedArguments,
      ImmutableArray<CustomAttributeNamedArgument<TType>> namedArguments)
    {
      this.FixedArguments = fixedArguments;
      this.NamedArguments = namedArguments;
    }
  }
}

// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PropertyAccessors
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct PropertyAccessors
  {
    private readonly int _getterRowId;
    private readonly int _setterRowId;
    private readonly ImmutableArray<MethodDefinitionHandle> _others;

    public MethodDefinitionHandle Getter => MethodDefinitionHandle.FromRowId(this._getterRowId);

    public MethodDefinitionHandle Setter => MethodDefinitionHandle.FromRowId(this._setterRowId);

    public ImmutableArray<MethodDefinitionHandle> Others => this._others;

    internal PropertyAccessors(
      int getterRowId,
      int setterRowId,
      ImmutableArray<MethodDefinitionHandle> others)
    {
      this._getterRowId = getterRowId;
      this._setterRowId = setterRowId;
      this._others = others;
    }
  }
}

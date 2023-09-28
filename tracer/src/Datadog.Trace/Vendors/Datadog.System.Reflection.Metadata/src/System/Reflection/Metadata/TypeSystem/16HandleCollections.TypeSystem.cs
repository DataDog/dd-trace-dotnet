// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.EventAccessors
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct EventAccessors
  {
    private readonly int _adderRowId;
    private readonly int _removerRowId;
    private readonly int _raiserRowId;
    private readonly ImmutableArray<MethodDefinitionHandle> _others;

    public MethodDefinitionHandle Adder => MethodDefinitionHandle.FromRowId(this._adderRowId);

    public MethodDefinitionHandle Remover => MethodDefinitionHandle.FromRowId(this._removerRowId);

    public MethodDefinitionHandle Raiser => MethodDefinitionHandle.FromRowId(this._raiserRowId);

    public ImmutableArray<MethodDefinitionHandle> Others => this._others;

    internal EventAccessors(
      int adderRowId,
      int removerRowId,
      int raiserRowId,
      ImmutableArray<MethodDefinitionHandle> others)
    {
      this._adderRowId = adderRowId;
      this._removerRowId = removerRowId;
      this._raiserRowId = raiserRowId;
      this._others = others;
    }
  }
}

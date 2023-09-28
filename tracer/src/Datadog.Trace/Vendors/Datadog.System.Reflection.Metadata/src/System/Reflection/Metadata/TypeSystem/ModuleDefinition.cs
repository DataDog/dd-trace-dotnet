// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ModuleDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ModuleDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;


    #nullable enable
    internal ModuleDefinition(MetadataReader reader) => this._reader = reader;

    public int Generation => (int) this._reader.ModuleTable.GetGeneration();

    public StringHandle Name => this._reader.ModuleTable.GetName();

    public GuidHandle Mvid => this._reader.ModuleTable.GetMvid();

    public GuidHandle GenerationId => this._reader.ModuleTable.GetEncId();

    public GuidHandle BaseGenerationId => this._reader.ModuleTable.GetEncBaseId();

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) EntityHandle.ModuleDefinition);
  }
}

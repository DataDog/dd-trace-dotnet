// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodImport
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodImport
  {
    private readonly MethodImportAttributes _attributes;
    private readonly StringHandle _name;
    private readonly ModuleReferenceHandle _module;

    internal MethodImport(
      MethodImportAttributes attributes,
      StringHandle name,
      ModuleReferenceHandle module)
    {
      this._attributes = attributes;
      this._name = name;
      this._module = module;
    }

    public MethodImportAttributes Attributes => this._attributes;

    public StringHandle Name => this._name;

    public ModuleReferenceHandle Module => this._module;
  }
}

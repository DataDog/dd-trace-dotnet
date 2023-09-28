// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodImplementation
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodImplementation
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal MethodImplementation(MetadataReader reader, MethodImplementationHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private MethodImplementationHandle Handle => MethodImplementationHandle.FromRowId(this._rowId);

    public TypeDefinitionHandle Type => this._reader.MethodImplTable.GetClass(this.Handle);

    public EntityHandle MethodBody => this._reader.MethodImplTable.GetMethodBody(this.Handle);

    public EntityHandle MethodDeclaration => this._reader.MethodImplTable.GetMethodDeclaration(this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

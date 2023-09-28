// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.InterfaceImplementation
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct InterfaceImplementation
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal InterfaceImplementation(MetadataReader reader, InterfaceImplementationHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private InterfaceImplementationHandle Handle => InterfaceImplementationHandle.FromRowId(this._rowId);

    /// <summary>
    /// The interface that is implemented
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />, or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />
    /// </summary>
    public EntityHandle Interface => this._reader.InterfaceImplTable.GetInterface(this._rowId);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

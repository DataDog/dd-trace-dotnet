// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ManifestResource
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ManifestResource
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal ManifestResource(MetadataReader reader, ManifestResourceHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private ManifestResourceHandle Handle => ManifestResourceHandle.FromRowId(this._rowId);

    /// <summary>
    /// Specifies the byte offset within the referenced file at which this resource record begins.
    /// </summary>
    /// <remarks>
    /// Corresponds to Offset field of ManifestResource table in ECMA-335 Standard.
    /// </remarks>
    public long Offset => (long) this._reader.ManifestResourceTable.GetOffset(this.Handle);

    /// <summary>Resource attributes.</summary>
    /// <remarks>
    /// Corresponds to Flags field of ManifestResource table in ECMA-335 Standard.
    /// </remarks>
    public ManifestResourceAttributes Attributes => this._reader.ManifestResourceTable.GetFlags(this.Handle);

    /// <summary>Name of the resource.</summary>
    /// <remarks>
    /// Corresponds to Name field of ManifestResource table in ECMA-335 Standard.
    /// </remarks>
    public StringHandle Name => this._reader.ManifestResourceTable.GetName(this.Handle);

    /// <summary>
    /// <see cref="T:System.Reflection.Metadata.AssemblyFileHandle" />, <see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" />, or nil handle.
    /// </summary>
    /// <remarks>
    /// Corresponds to Implementation field of ManifestResource table in ECMA-335 Standard.
    /// 
    /// If nil then <see cref="P:System.Reflection.Metadata.ManifestResource.Offset" /> is an offset in the PE image that contains the metadata,
    /// starting from the Resource entry in the CLI header.
    /// </remarks>
    public EntityHandle Implementation => this._reader.ManifestResourceTable.GetImplementation(this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

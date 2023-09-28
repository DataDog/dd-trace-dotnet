// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyFile
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct AssemblyFile
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal AssemblyFile(MetadataReader reader, AssemblyFileHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private AssemblyFileHandle Handle => AssemblyFileHandle.FromRowId(this._rowId);

    /// <summary>True if the file contains metadata.</summary>
    /// <remarks>
    /// Corresponds to Flags field of File table in ECMA-335 Standard.
    /// </remarks>
    public bool ContainsMetadata => this._reader.FileTable.GetFlags(this.Handle) == 0U;

    /// <summary>File name with extension.</summary>
    /// <remarks>
    /// Corresponds to Name field of File table in ECMA-335 Standard.
    /// </remarks>
    public StringHandle Name => this._reader.FileTable.GetName(this.Handle);

    /// <summary>
    /// Hash value of the file content calculated using <see cref="P:System.Reflection.Metadata.AssemblyDefinition.HashAlgorithm" />.
    /// </summary>
    /// <remarks>
    /// Corresponds to HashValue field of File table in ECMA-335 Standard.
    /// </remarks>
    public BlobHandle HashValue => this._reader.FileTable.GetHashValue(this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

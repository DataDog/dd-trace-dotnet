// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ImportScope
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Lexical scope within which a group of imports are available. Stored in debug metadata.
  /// </summary>
  /// <remarks>
  /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#importscope-table-0x35
  /// </remarks>
  public readonly struct ImportScope
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal ImportScope(MetadataReader reader, ImportScopeHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private ImportScopeHandle Handle => ImportScopeHandle.FromRowId(this._rowId);

    public ImportScopeHandle Parent => this._reader.ImportScopeTable.GetParent(this.Handle);

    public BlobHandle ImportsBlob => this._reader.ImportScopeTable.GetImports(this.Handle);

    public ImportDefinitionCollection GetImports() => new ImportDefinitionCollection(this._reader.BlobHeap.GetMemoryBlock(this.ImportsBlob));
  }
}

// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalScope
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// Scope of local variables and constants. Stored in debug metadata.
  /// </summary>
  /// <remarks>
  /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localscope-table-0x32.
  /// </remarks>
  public readonly struct LocalScope
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal LocalScope(MetadataReader reader, LocalScopeHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private LocalScopeHandle Handle => LocalScopeHandle.FromRowId(this._rowId);

    public MethodDefinitionHandle Method => this._reader.LocalScopeTable.GetMethod(this._rowId);

    public ImportScopeHandle ImportScope => this._reader.LocalScopeTable.GetImportScope(this.Handle);

    public int StartOffset => this._reader.LocalScopeTable.GetStartOffset(this._rowId);

    public int Length => this._reader.LocalScopeTable.GetLength(this._rowId);

    public int EndOffset => this._reader.LocalScopeTable.GetEndOffset(this._rowId);

    public LocalVariableHandleCollection GetLocalVariables() => new LocalVariableHandleCollection(this._reader, this.Handle);

    public LocalConstantHandleCollection GetLocalConstants() => new LocalConstantHandleCollection(this._reader, this.Handle);

    public LocalScopeHandleCollection.ChildrenEnumerator GetChildren() => new LocalScopeHandleCollection.ChildrenEnumerator(this._reader, this._rowId);
  }
}

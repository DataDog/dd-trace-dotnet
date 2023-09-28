// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalVariable
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>Local variable. Stored in debug metadata.</summary>
  /// <remarks>
  /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localvariable-table-0x33.
  /// </remarks>
  public readonly struct LocalVariable
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal LocalVariable(MetadataReader reader, LocalVariableHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private LocalVariableHandle Handle => LocalVariableHandle.FromRowId(this._rowId);

    public LocalVariableAttributes Attributes => this._reader.LocalVariableTable.GetAttributes(this.Handle);

    public int Index => (int) this._reader.LocalVariableTable.GetIndex(this.Handle);

    public StringHandle Name => this._reader.LocalVariableTable.GetName(this.Handle);
  }
}

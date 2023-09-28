// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.LocalConstant
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>Local constant. Stored in debug metadata.</summary>
  /// <remarks>
  /// See https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#localconstant-table-0x34.
  /// </remarks>
  public readonly struct LocalConstant
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal LocalConstant(MetadataReader reader, LocalConstantHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private LocalConstantHandle Handle => LocalConstantHandle.FromRowId(this._rowId);

    public StringHandle Name => this._reader.LocalConstantTable.GetName(this.Handle);

    /// <summary>The constant signature.</summary>
    public BlobHandle Signature => this._reader.LocalConstantTable.GetSignature(this.Handle);
  }
}

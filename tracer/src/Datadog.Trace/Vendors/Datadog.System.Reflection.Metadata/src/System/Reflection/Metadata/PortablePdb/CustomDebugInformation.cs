// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.CustomDebugInformation
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct CustomDebugInformation
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal CustomDebugInformation(MetadataReader reader, CustomDebugInformationHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private CustomDebugInformationHandle Handle => CustomDebugInformationHandle.FromRowId(this._rowId);

    public EntityHandle Parent => this._reader.CustomDebugInformationTable.GetParent(this.Handle);

    public GuidHandle Kind => this._reader.CustomDebugInformationTable.GetKind(this.Handle);

    public BlobHandle Value => this._reader.CustomDebugInformationTable.GetValue(this.Handle);
  }
}

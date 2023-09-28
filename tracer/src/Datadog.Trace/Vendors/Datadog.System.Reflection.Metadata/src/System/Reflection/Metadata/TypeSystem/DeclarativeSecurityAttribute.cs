// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.DeclarativeSecurityAttribute
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct DeclarativeSecurityAttribute
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal DeclarativeSecurityAttribute(MetadataReader reader, int rowId)
    {
      this._reader = reader;
      this._rowId = rowId;
    }

    public DeclarativeSecurityAction Action => this._reader.DeclSecurityTable.GetAction(this._rowId);

    public EntityHandle Parent => this._reader.DeclSecurityTable.GetParent(this._rowId);

    public BlobHandle PermissionSet => this._reader.DeclSecurityTable.GetPermissionSet(this._rowId);
  }
}

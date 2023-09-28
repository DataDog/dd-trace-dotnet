// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Parameter
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System.Reflection;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct Parameter
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal Parameter(MetadataReader reader, ParameterHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private ParameterHandle Handle => ParameterHandle.FromRowId(this._rowId);

    public ParameterAttributes Attributes => this._reader.ParamTable.GetFlags(this.Handle);

    public int SequenceNumber => (int) this._reader.ParamTable.GetSequence(this.Handle);

    public StringHandle Name => this._reader.ParamTable.GetName(this.Handle);

    public ConstantHandle GetDefaultValue() => this._reader.ConstantTable.FindConstant((EntityHandle) this.Handle);

    public BlobHandle GetMarshallingDescriptor()
    {
      int fieldMarshalRowId = this._reader.FieldMarshalTable.FindFieldMarshalRowId((EntityHandle) this.Handle);
      return fieldMarshalRowId == 0 ? new BlobHandle() : this._reader.FieldMarshalTable.GetNativeType(fieldMarshalRowId);
    }

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

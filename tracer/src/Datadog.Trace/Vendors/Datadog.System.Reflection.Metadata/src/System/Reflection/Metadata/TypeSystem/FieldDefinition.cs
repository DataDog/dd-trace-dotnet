// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.FieldDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct FieldDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;


    #nullable enable
    internal FieldDefinition(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private FieldDefTreatment Treatment => (FieldDefTreatment) (this._treatmentAndRowId >> 24);

    private FieldDefinitionHandle Handle => FieldDefinitionHandle.FromRowId(this.RowId);

    public StringHandle Name => this.Treatment == FieldDefTreatment.None ? this._reader.FieldTable.GetName(this.Handle) : this.GetProjectedName();

    public FieldAttributes Attributes => this.Treatment == FieldDefTreatment.None ? this._reader.FieldTable.GetFlags(this.Handle) : this.GetProjectedFlags();

    public BlobHandle Signature => this.Treatment == FieldDefTreatment.None ? this._reader.FieldTable.GetSignature(this.Handle) : this.GetProjectedSignature();

    public TType DecodeSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeFieldSignature(ref blobReader);
    }

    public TypeDefinitionHandle GetDeclaringType() => this._reader.GetDeclaringType(this.Handle);

    public ConstantHandle GetDefaultValue() => this._reader.ConstantTable.FindConstant((EntityHandle) this.Handle);

    public int GetRelativeVirtualAddress()
    {
      int fieldRvaRowId = this._reader.FieldRvaTable.FindFieldRvaRowId(this.Handle.RowId);
      return fieldRvaRowId == 0 ? 0 : this._reader.FieldRvaTable.GetRva(fieldRvaRowId);
    }

    /// <summary>Returns field layout offset, or -1 if not available.</summary>
    public int GetOffset()
    {
      int fieldLayoutRowId = this._reader.FieldLayoutTable.FindFieldLayoutRowId(this.Handle);
      if (fieldLayoutRowId == 0)
        return -1;
      uint offset = this._reader.FieldLayoutTable.GetOffset(fieldLayoutRowId);
      return offset > (uint) int.MaxValue ? -1 : (int) offset;
    }

    public BlobHandle GetMarshallingDescriptor()
    {
      int fieldMarshalRowId = this._reader.FieldMarshalTable.FindFieldMarshalRowId((EntityHandle) this.Handle);
      return fieldMarshalRowId == 0 ? new BlobHandle() : this._reader.FieldMarshalTable.GetNativeType(fieldMarshalRowId);
    }

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    private StringHandle GetProjectedName() => this._reader.FieldTable.GetName(this.Handle);

    private FieldAttributes GetProjectedFlags()
    {
      FieldAttributes flags = this._reader.FieldTable.GetFlags(this.Handle);
      return this.Treatment == FieldDefTreatment.EnumValue ? flags & ~FieldAttributes.FieldAccessMask | FieldAttributes.Public : flags;
    }

    private BlobHandle GetProjectedSignature() => this._reader.FieldTable.GetSignature(this.Handle);
  }
}

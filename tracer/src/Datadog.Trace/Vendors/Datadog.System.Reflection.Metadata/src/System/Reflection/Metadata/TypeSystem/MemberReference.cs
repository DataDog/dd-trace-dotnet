// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MemberReference
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MemberReference
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;


    #nullable enable
    internal MemberReference(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private MemberRefTreatment Treatment => (MemberRefTreatment) (this._treatmentAndRowId >> 24);

    private MemberReferenceHandle Handle => MemberReferenceHandle.FromRowId(this.RowId);

    /// <summary>
    /// MethodDef, ModuleRef,TypeDef, TypeRef, or TypeSpec handle.
    /// </summary>
    public EntityHandle Parent => this.Treatment == MemberRefTreatment.None ? this._reader.MemberRefTable.GetClass(this.Handle) : this.GetProjectedParent();

    public StringHandle Name => this.Treatment == MemberRefTreatment.None ? this._reader.MemberRefTable.GetName(this.Handle) : this.GetProjectedName();

    /// <summary>Gets a handle to the signature blob.</summary>
    public BlobHandle Signature => this.Treatment == MemberRefTreatment.None ? this._reader.MemberRefTable.GetSignature(this.Handle) : this.GetProjectedSignature();

    public TType DecodeFieldSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeFieldSignature(ref blobReader);
    }

    public MethodSignature<TType> DecodeMethodSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeMethodSignature(ref blobReader);
    }

    /// <summary>
    /// Determines if the member reference is to a method or field.
    /// </summary>
    /// <exception cref="T:System.BadImageFormatException">The member reference signature is invalid.</exception>
    public MemberReferenceKind GetKind()
    {
      switch (this._reader.GetBlobReader(this.Signature).ReadSignatureHeader().Kind)
      {
        case SignatureKind.Method:
          return MemberReferenceKind.Method;
        case SignatureKind.Field:
          return MemberReferenceKind.Field;
        default:
          throw new BadImageFormatException();
      }
    }

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    private EntityHandle GetProjectedParent() => this._reader.MemberRefTable.GetClass(this.Handle);

    private StringHandle GetProjectedName() => this.Treatment == MemberRefTreatment.Dispose ? StringHandle.FromVirtualIndex(StringHandle.VirtualIndex.Dispose) : this._reader.MemberRefTable.GetName(this.Handle);

    private BlobHandle GetProjectedSignature() => this._reader.MemberRefTable.GetSignature(this.Handle);
  }
}

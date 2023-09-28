// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodSpecification
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodSpecification
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal MethodSpecification(MetadataReader reader, MethodSpecificationHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private MethodSpecificationHandle Handle => MethodSpecificationHandle.FromRowId(this._rowId);

    /// <summary>
    /// MethodDef or MemberRef handle specifying to which generic method this <see cref="T:System.Reflection.Metadata.MethodSpecification" /> refers,
    /// that is which generic method is it an instantiation of.
    /// </summary>
    public EntityHandle Method => this._reader.MethodSpecTable.GetMethod(this.Handle);

    /// <summary>Gets a handle to the signature blob.</summary>
    public BlobHandle Signature => this._reader.MethodSpecTable.GetInstantiation(this.Handle);

    public ImmutableArray<TType> DecodeSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeMethodSpecificationSignature(ref blobReader);
    }

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

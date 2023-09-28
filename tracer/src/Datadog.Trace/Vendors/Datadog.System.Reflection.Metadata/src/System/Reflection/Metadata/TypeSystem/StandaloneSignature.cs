// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.StandaloneSignature
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct StandaloneSignature
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal StandaloneSignature(MetadataReader reader, StandaloneSignatureHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private StandaloneSignatureHandle Handle => StandaloneSignatureHandle.FromRowId(this._rowId);

    /// <summary>Gets a handle to the signature blob.</summary>
    public BlobHandle Signature => this._reader.StandAloneSigTable.GetSignature(this._rowId);

    public MethodSignature<TType> DecodeMethodSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeMethodSignature(ref blobReader);
    }

    public ImmutableArray<TType> DecodeLocalSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeLocalSignature(ref blobReader);
    }

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    /// <summary>
    /// Determines the kind of signature, which can be <see cref="F:System.Reflection.Metadata.SignatureKind.Method" /> or <see cref="F:System.Reflection.Metadata.SignatureKind.LocalVariables" />
    /// </summary>
    /// <exception cref="T:System.BadImageFormatException">The signature is invalid.</exception>
    public StandaloneSignatureKind GetKind()
    {
      switch (this._reader.GetBlobReader(this.Signature).ReadSignatureHeader().Kind)
      {
        case SignatureKind.Method:
          return StandaloneSignatureKind.Method;
        case SignatureKind.LocalVariables:
          return StandaloneSignatureKind.LocalVariables;
        default:
          throw new BadImageFormatException();
      }
    }
  }
}

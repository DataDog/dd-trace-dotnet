// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct MethodDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;


    #nullable enable
    internal MethodDefinition(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private MethodDefTreatment Treatment => (MethodDefTreatment) (this._treatmentAndRowId >> 24);

    public MethodDefinitionHandle Handle => MethodDefinitionHandle.FromRowId(this.RowId);

    public StringHandle Name => this.Treatment == MethodDefTreatment.None ? this._reader.MethodDefTable.GetName(this.Handle) : this.GetProjectedName();

    public BlobHandle Signature => this.Treatment == MethodDefTreatment.None ? this._reader.MethodDefTable.GetSignature(this.Handle) : this.GetProjectedSignature();

    public MethodSignature<TType> DecodeSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeMethodSignature(ref blobReader);
    }

    public int RelativeVirtualAddress => this.Treatment == MethodDefTreatment.None ? this._reader.MethodDefTable.GetRva(this.Handle) : MethodDefinition.GetProjectedRelativeVirtualAddress();

    public MethodAttributes Attributes => this.Treatment == MethodDefTreatment.None ? this._reader.MethodDefTable.GetFlags(this.Handle) : this.GetProjectedFlags();

    public MethodImplAttributes ImplAttributes => this.Treatment == MethodDefTreatment.None ? this._reader.MethodDefTable.GetImplFlags(this.Handle) : this.GetProjectedImplFlags();

    public TypeDefinitionHandle GetDeclaringType() => this._reader.GetDeclaringType(this.Handle);

    public ParameterHandleCollection GetParameters() => new ParameterHandleCollection(this._reader, this.Handle);

    public GenericParameterHandleCollection GetGenericParameters() => this._reader.GenericParamTable.FindGenericParametersForMethod(this.Handle);

    public MethodImport GetImport()
    {
      int implForMethod = this._reader.ImplMapTable.FindImplForMethod(this.Handle);
      return implForMethod == 0 ? new MethodImport() : this._reader.ImplMapTable.GetImport(implForMethod);
    }

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    public DeclarativeSecurityAttributeHandleCollection GetDeclarativeSecurityAttributes() => new DeclarativeSecurityAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    private StringHandle GetProjectedName() => (this.Treatment & MethodDefTreatment.KindMask) == MethodDefTreatment.DisposeMethod ? StringHandle.FromVirtualIndex(StringHandle.VirtualIndex.Dispose) : this._reader.MethodDefTable.GetName(this.Handle);

    private MethodAttributes GetProjectedFlags()
    {
      MethodAttributes methodAttributes = this._reader.MethodDefTable.GetFlags(this.Handle);
      MethodDefTreatment treatment = this.Treatment;
      if ((treatment & MethodDefTreatment.KindMask) == MethodDefTreatment.HiddenInterfaceImplementation)
        methodAttributes = methodAttributes & ~MethodAttributes.MemberAccessMask | MethodAttributes.Private;
      if ((treatment & MethodDefTreatment.MarkAbstractFlag) != MethodDefTreatment.None)
        methodAttributes |= MethodAttributes.Abstract;
      if ((treatment & MethodDefTreatment.MarkPublicFlag) != MethodDefTreatment.None)
        methodAttributes = methodAttributes & ~MethodAttributes.MemberAccessMask | MethodAttributes.Public;
      return methodAttributes | MethodAttributes.HideBySig;
    }

    private MethodImplAttributes GetProjectedImplFlags()
    {
      MethodImplAttributes implFlags = this._reader.MethodDefTable.GetImplFlags(this.Handle);
      switch (this.Treatment & MethodDefTreatment.KindMask)
      {
        case MethodDefTreatment.Other:
        case MethodDefTreatment.AttributeMethod:
        case MethodDefTreatment.InterfaceMethod:
        case MethodDefTreatment.HiddenInterfaceImplementation:
        case MethodDefTreatment.DisposeMethod:
          implFlags |= MethodImplAttributes.CodeTypeMask | MethodImplAttributes.InternalCall;
          break;
        case MethodDefTreatment.DelegateMethod:
          implFlags |= MethodImplAttributes.CodeTypeMask;
          break;
      }
      return implFlags;
    }

    private BlobHandle GetProjectedSignature() => this._reader.MethodDefTable.GetSignature(this.Handle);

    private static int GetProjectedRelativeVirtualAddress() => 0;
  }
}

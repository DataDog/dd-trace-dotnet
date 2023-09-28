// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.PropertyDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Reflection;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct PropertyDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal PropertyDefinition(MetadataReader reader, PropertyDefinitionHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private PropertyDefinitionHandle Handle => PropertyDefinitionHandle.FromRowId(this._rowId);

    public StringHandle Name => this._reader.PropertyTable.GetName(this.Handle);

    public PropertyAttributes Attributes => this._reader.PropertyTable.GetFlags(this.Handle);

    public BlobHandle Signature => this._reader.PropertyTable.GetSignature(this.Handle);

    public MethodSignature<TType> DecodeSignature<TType, TGenericContext>(
      ISignatureTypeProvider<TType, TGenericContext> provider,
      TGenericContext genericContext)
    {
      SignatureDecoder<TType, TGenericContext> signatureDecoder = new SignatureDecoder<TType, TGenericContext>(provider, this._reader, genericContext);
      BlobReader blobReader = this._reader.GetBlobReader(this.Signature);
      return signatureDecoder.DecodeMethodSignature(ref blobReader);
    }

    public ConstantHandle GetDefaultValue() => this._reader.ConstantTable.FindConstant((EntityHandle) this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    public PropertyAccessors GetAccessors()
    {
      int getterRowId = 0;
      int setterRowId = 0;
      ImmutableArray<MethodDefinitionHandle>.Builder builder = (ImmutableArray<MethodDefinitionHandle>.Builder) null;
      ushort methodCount;
      int methodsForProperty = this._reader.MethodSemanticsTable.FindSemanticMethodsForProperty(this.Handle, out methodCount);
      for (ushort index = 0; (int) index < (int) methodCount; ++index)
      {
        int rowId = methodsForProperty + (int) index;
        MethodDefinitionHandle method;
        switch (this._reader.MethodSemanticsTable.GetSemantics(rowId))
        {
          case MethodSemanticsAttributes.Setter:
            method = this._reader.MethodSemanticsTable.GetMethod(rowId);
            setterRowId = method.RowId;
            break;
          case MethodSemanticsAttributes.Getter:
            method = this._reader.MethodSemanticsTable.GetMethod(rowId);
            getterRowId = method.RowId;
            break;
          case MethodSemanticsAttributes.Other:
            if (builder == null)
              builder = ImmutableArray.CreateBuilder<MethodDefinitionHandle>();
            builder.Add(this._reader.MethodSemanticsTable.GetMethod(rowId));
            break;
        }
      }
      ImmutableArray<MethodDefinitionHandle> others = builder != null ? builder.ToImmutable() : ImmutableArray<MethodDefinitionHandle>.Empty;
      return new PropertyAccessors(getterRowId, setterRowId, others);
    }
  }
}

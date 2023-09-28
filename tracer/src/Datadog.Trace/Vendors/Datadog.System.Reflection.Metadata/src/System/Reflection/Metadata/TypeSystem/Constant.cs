// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Constant
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct Constant
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal Constant(MetadataReader reader, int rowId)
    {
      this._reader = reader;
      this._rowId = rowId;
    }

    private ConstantHandle Handle => ConstantHandle.FromRowId(this._rowId);

    /// <summary>The type of the constant value.</summary>
    /// <remarks>
    /// Corresponds to Type field of Constant table in ECMA-335 Standard.
    /// </remarks>
    public ConstantTypeCode TypeCode => this._reader.ConstantTable.GetType(this.Handle);

    /// <summary>The constant value.</summary>
    /// <remarks>
    /// Corresponds to Value field of Constant table in ECMA-335 Standard.
    /// </remarks>
    public BlobHandle Value => this._reader.ConstantTable.GetValue(this.Handle);

    /// <summary>
    /// The parent handle (<see cref="T:System.Reflection.Metadata.ParameterHandle" />, <see cref="T:System.Reflection.Metadata.FieldDefinitionHandle" />, or <see cref="T:System.Reflection.Metadata.PropertyDefinitionHandle" />).
    /// </summary>
    /// <remarks>
    /// Corresponds to Parent field of Constant table in ECMA-335 Standard.
    /// </remarks>
    public EntityHandle Parent => this._reader.ConstantTable.GetParent(this.Handle);
  }
}

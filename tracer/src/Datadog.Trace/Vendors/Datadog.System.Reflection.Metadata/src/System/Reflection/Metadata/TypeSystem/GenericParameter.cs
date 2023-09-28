// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.GenericParameter
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System.Reflection;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct GenericParameter
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal GenericParameter(MetadataReader reader, GenericParameterHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private GenericParameterHandle Handle => GenericParameterHandle.FromRowId(this._rowId);

    /// <summary>
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.MethodDefinitionHandle" />.
    /// </summary>
    /// <remarks>
    /// Corresponds to Owner field of GenericParam table in ECMA-335 Standard.
    /// </remarks>
    public EntityHandle Parent => this._reader.GenericParamTable.GetOwner(this.Handle);

    /// <summary>Attributes specifying variance and constraints.</summary>
    /// <remarks>
    /// Corresponds to Flags field of GenericParam table in ECMA-335 Standard.
    /// </remarks>
    public GenericParameterAttributes Attributes => this._reader.GenericParamTable.GetFlags(this.Handle);

    /// <summary>
    /// Zero-based index of the parameter within the declaring generic type or method declaration.
    /// </summary>
    /// <remarks>
    /// Corresponds to Number field of GenericParam table in ECMA-335 Standard.
    /// </remarks>
    public int Index => (int) this._reader.GenericParamTable.GetNumber(this.Handle);

    /// <summary>The name of the generic parameter.</summary>
    /// <remarks>
    /// Corresponds to Name field of GenericParam table in ECMA-335 Standard.
    /// </remarks>
    public StringHandle Name => this._reader.GenericParamTable.GetName(this.Handle);

    public GenericParameterConstraintHandleCollection GetConstraints() => this._reader.GenericParamConstraintTable.FindConstraintsForGenericParam(this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

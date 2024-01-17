﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.GenericParameterConstraint
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct GenericParameterConstraint
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal GenericParameterConstraint(
      MetadataReader reader,
      GenericParameterConstraintHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private GenericParameterConstraintHandle Handle => GenericParameterConstraintHandle.FromRowId(this._rowId);

    /// <summary>
    /// The constrained <see cref="T:System.Reflection.Metadata.GenericParameterHandle" />.
    /// </summary>
    /// <remarks>
    /// Corresponds to Owner field of GenericParamConstraint table in ECMA-335 Standard.
    /// </remarks>
    public GenericParameterHandle Parameter => this._reader.GenericParamConstraintTable.GetOwner(this.Handle);

    /// <summary>
    /// Handle (<see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" />, or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />)
    /// specifying from which type this generic parameter is constrained to derive,
    /// or which interface this generic parameter is constrained to implement.
    /// </summary>
    /// <remarks>
    /// Corresponds to Constraint field of GenericParamConstraint table in ECMA-335 Standard.
    /// </remarks>
    public EntityHandle Type => this._reader.GenericParamConstraintTable.GetConstraint(this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);
  }
}

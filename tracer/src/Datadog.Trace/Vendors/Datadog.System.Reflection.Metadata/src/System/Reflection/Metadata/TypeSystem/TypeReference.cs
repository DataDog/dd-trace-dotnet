﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.TypeReference
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct TypeReference
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;


    #nullable enable
    internal TypeReference(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private TypeRefTreatment Treatment => (TypeRefTreatment) (this._treatmentAndRowId >> 24);

    private TypeReferenceHandle Handle => TypeReferenceHandle.FromRowId(this.RowId);

    /// <summary>
    /// Resolution scope in which the target type is defined and is uniquely identified by the specified <see cref="P:System.Reflection.Metadata.TypeReference.Namespace" /> and <see cref="P:System.Reflection.Metadata.TypeReference.Name" />.
    /// </summary>
    /// <remarks>
    /// Resolution scope can be one of the following handles:
    /// <list type="bullet">
    /// <item><description><see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> of the enclosing type, if the target type is a nested type.</description></item>
    /// <item><description><see cref="T:System.Reflection.Metadata.ModuleReferenceHandle" />, if the target type is defined in another module within the same assembly as this one.</description></item>
    /// <item><description><see cref="F:System.Reflection.Metadata.EntityHandle.ModuleDefinition" />, if the target type is defined in the current module. This should not occur in a CLI compressed metadata module.</description></item>
    /// <item><description><see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" />, if the target type is defined in a different assembly from the current module.</description></item>
    /// <item><description>Nil handle if the target type must be resolved by searching the <see cref="P:System.Reflection.Metadata.MetadataReader.ExportedTypes" /> for a matching <see cref="P:System.Reflection.Metadata.TypeReference.Namespace" /> and <see cref="P:System.Reflection.Metadata.TypeReference.Name" />.</description></item>
    /// </list>
    /// </remarks>
    public EntityHandle ResolutionScope => this.Treatment == TypeRefTreatment.None ? this._reader.TypeRefTable.GetResolutionScope(this.Handle) : this.GetProjectedResolutionScope();

    /// <summary>Name of the target type.</summary>
    public StringHandle Name => this.Treatment == TypeRefTreatment.None ? this._reader.TypeRefTable.GetName(this.Handle) : this.GetProjectedName();

    /// <summary>
    /// Full name of the namespace Datadog.where the target type is defined, or nil if the type is nested or defined in a root namespace.
    /// </summary>
    public StringHandle Namespace => this.Treatment == TypeRefTreatment.None ? this._reader.TypeRefTable.GetNamespace(this.Handle) : this.GetProjectedNamespace();

    private EntityHandle GetProjectedResolutionScope()
    {
      switch (this.Treatment)
      {
        case TypeRefTreatment.SystemDelegate:
        case TypeRefTreatment.SystemAttribute:
          return (EntityHandle) AssemblyReferenceHandle.FromVirtualIndex(AssemblyReferenceHandle.VirtualIndex.System_Runtime);
        case TypeRefTreatment.UseProjectionInfo:
          return (EntityHandle) MetadataReader.GetProjectedAssemblyRef(this.RowId);
        default:
          return (EntityHandle) new AssemblyReferenceHandle();
      }
    }

    private StringHandle GetProjectedName() => this.Treatment == TypeRefTreatment.UseProjectionInfo ? MetadataReader.GetProjectedName(this.RowId) : this._reader.TypeRefTable.GetName(this.Handle);

    private StringHandle GetProjectedNamespace()
    {
      switch (this.Treatment)
      {
        case TypeRefTreatment.SystemDelegate:
        case TypeRefTreatment.SystemAttribute:
          return StringHandle.FromVirtualIndex(StringHandle.VirtualIndex.System);
        case TypeRefTreatment.UseProjectionInfo:
          return MetadataReader.GetProjectedNamespace(this.RowId);
        default:
          return new StringHandle();
      }
    }

    internal TypeRefSignatureTreatment SignatureTreatment => this.Treatment == TypeRefTreatment.None ? TypeRefSignatureTreatment.None : this.GetProjectedSignatureTreatment();

    private TypeRefSignatureTreatment GetProjectedSignatureTreatment() => this.Treatment == TypeRefTreatment.UseProjectionInfo ? MetadataReader.GetProjectedSignatureTreatment(this.RowId) : TypeRefSignatureTreatment.None;
  }
}

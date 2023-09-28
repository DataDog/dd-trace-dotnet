// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.TypeDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Reflection;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public readonly struct TypeDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;


    #nullable enable
    internal TypeDefinition(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private TypeDefTreatment Treatment => (TypeDefTreatment) (this._treatmentAndRowId >> 24);

    private TypeDefinitionHandle Handle => TypeDefinitionHandle.FromRowId(this.RowId);

    public TypeAttributes Attributes => this.Treatment == TypeDefTreatment.None ? this._reader.TypeDefTable.GetFlags(this.Handle) : this.GetProjectedFlags();

    /// <summary>Indicates whether this is a nested type.</summary>
    public bool IsNested => this.Attributes.IsNested();

    /// <summary>Name of the type.</summary>
    public StringHandle Name => this.Treatment == TypeDefTreatment.None ? this._reader.TypeDefTable.GetName(this.Handle) : this.GetProjectedName();

    /// <summary>
    /// Full name of the namespace Datadog.where the type is defined, or nil if the type is nested or defined in a root namespace.
    /// </summary>
    public StringHandle Namespace => this.Treatment == TypeDefTreatment.None ? this._reader.TypeDefTable.GetNamespace(this.Handle) : this.GetProjectedNamespaceString();

    /// <summary>
    /// The definition handle of the namespace Datadog.where the type is defined, or nil if the type is nested or defined in a root namespace.
    /// </summary>
    public NamespaceDefinitionHandle NamespaceDefinition => this.Treatment == TypeDefTreatment.None ? this._reader.TypeDefTable.GetNamespaceDefinition(this.Handle) : this.GetProjectedNamespace();

    /// <summary>
    /// The base type of the type definition: either
    /// <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />.
    /// </summary>
    public EntityHandle BaseType => this.Treatment == TypeDefTreatment.None ? this._reader.TypeDefTable.GetExtends(this.Handle) : this.GetProjectedBaseType();

    public TypeLayout GetLayout()
    {
      int row = this._reader.ClassLayoutTable.FindRow(this.Handle);
      if (row == 0)
        return new TypeLayout();
      uint classSize = this._reader.ClassLayoutTable.GetClassSize(row);
      if ((long) (int) classSize != (long) classSize)
        throw new BadImageFormatException();
      int packingSize = (int) this._reader.ClassLayoutTable.GetPackingSize(row);
      return new TypeLayout((int) classSize, packingSize);
    }

    /// <summary>
    /// Returns the enclosing type of a specified nested type or nil handle if the type is not nested.
    /// </summary>
    public TypeDefinitionHandle GetDeclaringType() => this._reader.NestedClassTable.FindEnclosingType(this.Handle);

    public GenericParameterHandleCollection GetGenericParameters() => this._reader.GenericParamTable.FindGenericParametersForType(this.Handle);

    public MethodDefinitionHandleCollection GetMethods() => new MethodDefinitionHandleCollection(this._reader, this.Handle);

    public FieldDefinitionHandleCollection GetFields() => new FieldDefinitionHandleCollection(this._reader, this.Handle);

    public PropertyDefinitionHandleCollection GetProperties() => new PropertyDefinitionHandleCollection(this._reader, this.Handle);

    public EventDefinitionHandleCollection GetEvents() => new EventDefinitionHandleCollection(this._reader, this.Handle);

    /// <summary>
    /// Returns an array of types nested in the specified type.
    /// </summary>
    public ImmutableArray<TypeDefinitionHandle> GetNestedTypes() => this._reader.GetNestedTypes(this.Handle);

    public MethodImplementationHandleCollection GetMethodImplementations() => new MethodImplementationHandleCollection(this._reader, this.Handle);

    public InterfaceImplementationHandleCollection GetInterfaceImplementations() => new InterfaceImplementationHandleCollection(this._reader, this.Handle);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    public DeclarativeSecurityAttributeHandleCollection GetDeclarativeSecurityAttributes() => new DeclarativeSecurityAttributeHandleCollection(this._reader, (EntityHandle) this.Handle);

    private TypeAttributes GetProjectedFlags()
    {
      TypeAttributes projectedFlags = this._reader.TypeDefTable.GetFlags(this.Handle);
      TypeDefTreatment treatment = this.Treatment;
      switch (treatment & TypeDefTreatment.KindMask)
      {
        case TypeDefTreatment.NormalNonAttribute:
          projectedFlags |= TypeAttributes.Import | TypeAttributes.WindowsRuntime;
          break;
        case TypeDefTreatment.NormalAttribute:
          projectedFlags |= TypeAttributes.Sealed | TypeAttributes.WindowsRuntime;
          break;
        case TypeDefTreatment.UnmangleWinRTName:
          projectedFlags = projectedFlags & ~TypeAttributes.SpecialName | TypeAttributes.Public;
          break;
        case TypeDefTreatment.PrefixWinRTName:
          projectedFlags = projectedFlags & ~TypeAttributes.Public | TypeAttributes.Import;
          break;
        case TypeDefTreatment.RedirectedToClrType:
          projectedFlags = projectedFlags & ~TypeAttributes.Public | TypeAttributes.Import;
          break;
        case TypeDefTreatment.RedirectedToClrAttribute:
          projectedFlags &= ~TypeAttributes.Public;
          break;
      }
      if ((treatment & TypeDefTreatment.MarkAbstractFlag) != TypeDefTreatment.None)
        projectedFlags |= TypeAttributes.Abstract;
      if ((treatment & TypeDefTreatment.MarkInternalFlag) != TypeDefTreatment.None)
        projectedFlags &= ~TypeAttributes.Public;
      return projectedFlags;
    }

    private StringHandle GetProjectedName()
    {
      StringHandle name = this._reader.TypeDefTable.GetName(this.Handle);
      StringHandle projectedName;
      switch (this.Treatment & TypeDefTreatment.KindMask)
      {
        case TypeDefTreatment.UnmangleWinRTName:
          projectedName = name.SuffixRaw("<CLR>".Length);
          break;
        case TypeDefTreatment.PrefixWinRTName:
          projectedName = name.WithWinRTPrefix();
          break;
        default:
          projectedName = name;
          break;
      }
      return projectedName;
    }

    private NamespaceDefinitionHandle GetProjectedNamespace() => this._reader.TypeDefTable.GetNamespaceDefinition(this.Handle);

    private StringHandle GetProjectedNamespaceString() => this._reader.TypeDefTable.GetNamespace(this.Handle);

    private EntityHandle GetProjectedBaseType() => this._reader.TypeDefTable.GetExtends(this.Handle);
  }
}

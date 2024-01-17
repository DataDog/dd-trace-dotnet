﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.ExportedType
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System.Reflection;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct ExportedType
  {
    internal readonly MetadataReader reader;
    internal readonly int rowId;

    internal ExportedType(MetadataReader reader, int rowId)
    {
      this.reader = reader;
      this.rowId = rowId;
    }

    private ExportedTypeHandle Handle => ExportedTypeHandle.FromRowId(this.rowId);

    public TypeAttributes Attributes => this.reader.ExportedTypeTable.GetFlags(this.rowId);

    public bool IsForwarder => this.Attributes.IsForwarder() && this.Implementation.Kind == HandleKind.AssemblyReference;

    /// <summary>
    /// Name of the target type, or nil if the type is nested or defined in a root namespace.
    /// </summary>
    public StringHandle Name => this.reader.ExportedTypeTable.GetTypeName(this.rowId);

    /// <summary>
    /// Full name of the namespace Datadog.where the target type, or nil if the type is nested or defined in a root namespace.
    /// </summary>
    public StringHandle Namespace => this.reader.ExportedTypeTable.GetTypeNamespaceString(this.rowId);

    /// <summary>
    /// The definition handle of the namespace Datadog.where the target type is defined, or nil if the type is nested or defined in a root namespace.
    /// </summary>
    public NamespaceDefinitionHandle NamespaceDefinition => this.reader.ExportedTypeTable.GetTypeNamespace(this.rowId);

    /// <summary>
    /// Handle to resolve the implementation of the target type.
    /// </summary>
    /// <returns>
    /// <list type="bullet">
    /// <item><description><see cref="T:System.Reflection.Metadata.AssemblyFileHandle" /> representing another module in the assembly.</description></item>
    /// <item><description><see cref="T:System.Reflection.Metadata.AssemblyReferenceHandle" /> representing another assembly if <see cref="P:System.Reflection.Metadata.ExportedType.IsForwarder" /> is true.</description></item>
    /// <item><description><see cref="T:System.Reflection.Metadata.ExportedTypeHandle" /> representing the declaring exported type in which this was is nested.</description></item>
    /// </list>
    /// </returns>
    public EntityHandle Implementation => this.reader.ExportedTypeTable.GetImplementation(this.rowId);

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this.reader, (EntityHandle) this.Handle);
  }
}

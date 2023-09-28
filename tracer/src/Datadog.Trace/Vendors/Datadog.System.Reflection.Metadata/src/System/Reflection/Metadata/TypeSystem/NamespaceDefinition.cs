// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.NamespaceDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public struct NamespaceDefinition
  {

    #nullable disable
    private readonly NamespaceData _data;


    #nullable enable
    internal NamespaceDefinition(NamespaceData data) => this._data = data;

    /// <summary>Gets the unqualified name of the NamespaceDefinition.</summary>
    public StringHandle Name => this._data.Name;

    /// <summary>Gets the parent namespace.</summary>
    public NamespaceDefinitionHandle Parent => this._data.Parent;

    /// <summary>
    /// Gets the namespace Datadog.definitions that are direct children of the current
    /// namespace Datadog.definition.
    /// 
    /// System.Collections and System.Linq are direct children of System.
    /// System.Collections.Generic is a direct child of System.Collections.
    /// System.Collections.Generic is *not* a direct child of System.
    /// </summary>
    public ImmutableArray<NamespaceDefinitionHandle> NamespaceDefinitions => this._data.NamespaceDefinitions;

    /// <summary>
    /// Gets all type definitions that reside directly in a namespace.
    /// </summary>
    public ImmutableArray<TypeDefinitionHandle> TypeDefinitions => this._data.TypeDefinitions;

    /// <summary>
    /// Gets all exported types that reside directly in a namespace.
    /// </summary>
    public ImmutableArray<ExportedTypeHandle> ExportedTypes => this._data.ExportedTypes;
  }
}

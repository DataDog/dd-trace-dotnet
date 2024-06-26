//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.NamespaceDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata
{
  internal struct NamespaceDefinition
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
    /// Gets the namespace definitions that are direct children of the current
    /// namespace definition.
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

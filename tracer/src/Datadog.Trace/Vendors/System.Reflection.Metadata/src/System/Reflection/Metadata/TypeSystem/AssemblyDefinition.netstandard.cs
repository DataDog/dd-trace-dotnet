//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72


#nullable enable
using System;
using System.Reflection;

namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata
{
  internal readonly struct AssemblyDefinition
  {

    #nullable disable
    private readonly MetadataReader _reader;


    #nullable enable
    public AssemblyName GetAssemblyName()
    {
      AssemblyFlags flags = this.Flags;
      if (!this.PublicKey.IsNil)
        flags |= AssemblyFlags.PublicKey;
      return this._reader.GetAssemblyName(this.Name, this.Version, this.Culture, this.PublicKey, this.HashAlgorithm, flags);
    }

    internal AssemblyDefinition(MetadataReader reader) => this._reader = reader;

    public AssemblyHashAlgorithm HashAlgorithm => this._reader.AssemblyTable.GetHashAlgorithm();

    public Version Version => this._reader.AssemblyTable.GetVersion();

    public AssemblyFlags Flags => this._reader.AssemblyTable.GetFlags();

    public StringHandle Name => this._reader.AssemblyTable.GetName();

    public StringHandle Culture => this._reader.AssemblyTable.GetCulture();

    public BlobHandle PublicKey => this._reader.AssemblyTable.GetPublicKey();

    public CustomAttributeHandleCollection GetCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) EntityHandle.AssemblyDefinition);

    public DeclarativeSecurityAttributeHandleCollection GetDeclarativeSecurityAttributes() => new DeclarativeSecurityAttributeHandleCollection(this._reader, (EntityHandle) EntityHandle.AssemblyDefinition);
  }
}

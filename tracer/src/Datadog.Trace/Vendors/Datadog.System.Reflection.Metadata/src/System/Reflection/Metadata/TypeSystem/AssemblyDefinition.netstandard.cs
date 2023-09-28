// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyDefinition
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;
using System.Reflection;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct AssemblyDefinition
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

﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.AssemblyReference
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;
using System.Reflection;

namespace Datadog.System.Reflection.Metadata
{
  public readonly struct AssemblyReference
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly uint _treatmentAndRowId;
    private static readonly Version s_version_4_0_0_0 = new Version(4, 0, 0, 0);


    #nullable enable
    public AssemblyName GetAssemblyName() => this._reader.GetAssemblyName(this.Name, this.Version, this.Culture, this.PublicKeyOrToken, AssemblyHashAlgorithm.None, this.Flags);

    internal AssemblyReference(MetadataReader reader, uint treatmentAndRowId)
    {
      this._reader = reader;
      this._treatmentAndRowId = treatmentAndRowId;
    }

    private int RowId => (int) this._treatmentAndRowId & 16777215;

    private bool IsVirtual => (this._treatmentAndRowId & 2147483648U) > 0U;

    public Version Version
    {
      get
      {
        if (this.IsVirtual)
          return AssemblyReference.GetVirtualVersion();
        return this.RowId == this._reader.WinMDMscorlibRef ? AssemblyReference.s_version_4_0_0_0 : this._reader.AssemblyRefTable.GetVersion(this.RowId);
      }
    }

    public AssemblyFlags Flags => this.IsVirtual ? this.GetVirtualFlags() : this._reader.AssemblyRefTable.GetFlags(this.RowId);

    public StringHandle Name => this.IsVirtual ? this.GetVirtualName() : this._reader.AssemblyRefTable.GetName(this.RowId);

    public StringHandle Culture => this.IsVirtual ? AssemblyReference.GetVirtualCulture() : this._reader.AssemblyRefTable.GetCulture(this.RowId);

    public BlobHandle PublicKeyOrToken => this.IsVirtual ? this.GetVirtualPublicKeyOrToken() : this._reader.AssemblyRefTable.GetPublicKeyOrToken(this.RowId);

    public BlobHandle HashValue => this.IsVirtual ? AssemblyReference.GetVirtualHashValue() : this._reader.AssemblyRefTable.GetHashValue(this.RowId);

    public CustomAttributeHandleCollection GetCustomAttributes() => this.IsVirtual ? this.GetVirtualCustomAttributes() : new CustomAttributeHandleCollection(this._reader, (EntityHandle) AssemblyReferenceHandle.FromRowId(this.RowId));


    #nullable disable
    private static Version GetVirtualVersion() => AssemblyReference.s_version_4_0_0_0;

    private AssemblyFlags GetVirtualFlags() => this._reader.AssemblyRefTable.GetFlags(this._reader.WinMDMscorlibRef);

    private StringHandle GetVirtualName() => StringHandle.FromVirtualIndex(AssemblyReference.GetVirtualNameIndex((AssemblyReferenceHandle.VirtualIndex) this.RowId));

    private static StringHandle.VirtualIndex GetVirtualNameIndex(
      AssemblyReferenceHandle.VirtualIndex index)
    {
      switch (index)
      {
        case AssemblyReferenceHandle.VirtualIndex.System_Runtime:
          return StringHandle.VirtualIndex.System_Runtime;
        case AssemblyReferenceHandle.VirtualIndex.System_Runtime_InteropServices_WindowsRuntime:
          return StringHandle.VirtualIndex.System_Runtime_InteropServices_WindowsRuntime;
        case AssemblyReferenceHandle.VirtualIndex.System_ObjectModel:
          return StringHandle.VirtualIndex.System_ObjectModel;
        case AssemblyReferenceHandle.VirtualIndex.System_Runtime_WindowsRuntime:
          return StringHandle.VirtualIndex.System_Runtime_WindowsRuntime;
        case AssemblyReferenceHandle.VirtualIndex.System_Runtime_WindowsRuntime_UI_Xaml:
          return StringHandle.VirtualIndex.System_Runtime_WindowsRuntime_UI_Xaml;
        case AssemblyReferenceHandle.VirtualIndex.System_Numerics_Vectors:
          return StringHandle.VirtualIndex.System_Numerics_Vectors;
        default:
          return StringHandle.VirtualIndex.System_Runtime_WindowsRuntime;
      }
    }

    private static StringHandle GetVirtualCulture() => new StringHandle();

    private BlobHandle GetVirtualPublicKeyOrToken()
    {
      switch (this.RowId)
      {
        case 3:
        case 4:
          return this._reader.AssemblyRefTable.GetPublicKeyOrToken(this._reader.WinMDMscorlibRef);
        default:
          return BlobHandle.FromVirtualIndex((this._reader.AssemblyRefTable.GetFlags(this._reader.WinMDMscorlibRef) & AssemblyFlags.PublicKey) != 0 ? BlobHandle.VirtualIndex.ContractPublicKey : BlobHandle.VirtualIndex.ContractPublicKeyToken, (ushort) 0);
      }
    }

    private static BlobHandle GetVirtualHashValue() => new BlobHandle();

    private CustomAttributeHandleCollection GetVirtualCustomAttributes() => new CustomAttributeHandleCollection(this._reader, (EntityHandle) AssemblyReferenceHandle.FromRowId(this._reader.WinMDMscorlibRef));
  }
}

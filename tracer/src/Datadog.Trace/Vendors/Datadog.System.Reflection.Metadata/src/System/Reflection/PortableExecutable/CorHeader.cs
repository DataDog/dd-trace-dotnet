// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.CorHeader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.PortableExecutable
{
  public sealed class CorHeader
  {
    public ushort MajorRuntimeVersion { get; }

    public ushort MinorRuntimeVersion { get; }

    public DirectoryEntry MetadataDirectory { get; }

    public CorFlags Flags { get; }

    public int EntryPointTokenOrRelativeVirtualAddress { get; }

    public DirectoryEntry ResourcesDirectory { get; }

    public DirectoryEntry StrongNameSignatureDirectory { get; }

    public DirectoryEntry CodeManagerTableDirectory { get; }

    public DirectoryEntry VtableFixupsDirectory { get; }

    public DirectoryEntry ExportAddressTableJumpsDirectory { get; }

    public DirectoryEntry ManagedNativeHeaderDirectory { get; }

    internal CorHeader(ref PEBinaryReader reader)
    {
      reader.ReadInt32();
      this.MajorRuntimeVersion = reader.ReadUInt16();
      this.MinorRuntimeVersion = reader.ReadUInt16();
      this.MetadataDirectory = new DirectoryEntry(ref reader);
      this.Flags = (CorFlags) reader.ReadUInt32();
      this.EntryPointTokenOrRelativeVirtualAddress = reader.ReadInt32();
      this.ResourcesDirectory = new DirectoryEntry(ref reader);
      this.StrongNameSignatureDirectory = new DirectoryEntry(ref reader);
      this.CodeManagerTableDirectory = new DirectoryEntry(ref reader);
      this.VtableFixupsDirectory = new DirectoryEntry(ref reader);
      this.ExportAddressTableJumpsDirectory = new DirectoryEntry(ref reader);
      this.ManagedNativeHeaderDirectory = new DirectoryEntry(ref reader);
    }
  }
}

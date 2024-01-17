﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.DebugDirectoryEntry
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.PortableExecutable
{
  /// <summary>
  /// Identifies the location, size and format of a block of debug information.
  /// </summary>
  public readonly struct DebugDirectoryEntry
  {
    internal const int Size = 28;

    /// <summary>
    /// The time and date that the debug data was created if the PE/COFF file is not deterministic,
    /// otherwise a value based on the hash of the content.
    /// </summary>
    /// <remarks>
    /// The algorithm used to calculate this value is an implementation
    /// detail of the tool that produced the file.
    /// </remarks>
    public uint Stamp { get; }

    /// <summary>The major version number of the debug data format.</summary>
    public ushort MajorVersion { get; }

    /// <summary>The minor version number of the debug data format.</summary>
    public ushort MinorVersion { get; }

    /// <summary>The format of debugging information.</summary>
    public DebugDirectoryEntryType Type { get; }

    /// <summary>
    /// The size of the debug data (not including the debug directory itself).
    /// </summary>
    public int DataSize { get; }

    /// <summary>
    /// The address of the debug data when loaded, relative to the image base.
    /// </summary>
    public int DataRelativeVirtualAddress { get; }

    /// <summary>The file pointer to the debug data.</summary>
    public int DataPointer { get; }

    /// <summary>
    /// True if the entry is a <see cref="F:System.Reflection.PortableExecutable.DebugDirectoryEntryType.CodeView" /> entry pointing to a Portable PDB.
    /// </summary>
    public bool IsPortableCodeView => this.MinorVersion == (ushort) 20557;

    public DebugDirectoryEntry(
      uint stamp,
      ushort majorVersion,
      ushort minorVersion,
      DebugDirectoryEntryType type,
      int dataSize,
      int dataRelativeVirtualAddress,
      int dataPointer)
    {
      this.Stamp = stamp;
      this.MajorVersion = majorVersion;
      this.MinorVersion = minorVersion;
      this.Type = type;
      this.DataSize = dataSize;
      this.DataRelativeVirtualAddress = dataRelativeVirtualAddress;
      this.DataPointer = dataPointer;
    }
  }
}

﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEHeaderBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
  public sealed class PEHeaderBuilder
  {
    public Machine Machine { get; }

    public Characteristics ImageCharacteristics { get; }

    public byte MajorLinkerVersion { get; }

    public byte MinorLinkerVersion { get; }

    public ulong ImageBase { get; }

    public int SectionAlignment { get; }

    public int FileAlignment { get; }

    public ushort MajorOperatingSystemVersion { get; }

    public ushort MinorOperatingSystemVersion { get; }

    public ushort MajorImageVersion { get; }

    public ushort MinorImageVersion { get; }

    public ushort MajorSubsystemVersion { get; }

    public ushort MinorSubsystemVersion { get; }

    public Subsystem Subsystem { get; }

    public DllCharacteristics DllCharacteristics { get; }

    public ulong SizeOfStackReserve { get; }

    public ulong SizeOfStackCommit { get; }

    public ulong SizeOfHeapReserve { get; }

    public ulong SizeOfHeapCommit { get; }

    /// <summary>Creates PE header builder.</summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="fileAlignment" /> is not power of 2 between 512 and 64K, or
    /// <paramref name="sectionAlignment" /> not power of 2 or it's less than <paramref name="fileAlignment" />.
    /// </exception>
    public PEHeaderBuilder(
      Machine machine = Machine.Unknown,
      int sectionAlignment = 8192,
      int fileAlignment = 512,
      ulong imageBase = 4194304,
      byte majorLinkerVersion = 48,
      byte minorLinkerVersion = 0,
      ushort majorOperatingSystemVersion = 4,
      ushort minorOperatingSystemVersion = 0,
      ushort majorImageVersion = 0,
      ushort minorImageVersion = 0,
      ushort majorSubsystemVersion = 4,
      ushort minorSubsystemVersion = 0,
      Subsystem subsystem = Subsystem.WindowsCui,
      DllCharacteristics dllCharacteristics = DllCharacteristics.DynamicBase | DllCharacteristics.NxCompatible | DllCharacteristics.NoSeh | DllCharacteristics.TerminalServerAware,
      Characteristics imageCharacteristics = Characteristics.Dll,
      ulong sizeOfStackReserve = 1048576,
      ulong sizeOfStackCommit = 4096,
      ulong sizeOfHeapReserve = 1048576,
      ulong sizeOfHeapCommit = 4096)
    {
      if (fileAlignment < 512 || fileAlignment > 65536 || BitArithmetic.CountBits(fileAlignment) != 1)
        Throw.ArgumentOutOfRange(nameof (fileAlignment));
      if (sectionAlignment < fileAlignment || BitArithmetic.CountBits(sectionAlignment) != 1)
        Throw.ArgumentOutOfRange(nameof (sectionAlignment));
      this.Machine = machine;
      this.SectionAlignment = sectionAlignment;
      this.FileAlignment = fileAlignment;
      this.ImageBase = imageBase;
      this.MajorLinkerVersion = majorLinkerVersion;
      this.MinorLinkerVersion = minorLinkerVersion;
      this.MajorOperatingSystemVersion = majorOperatingSystemVersion;
      this.MinorOperatingSystemVersion = minorOperatingSystemVersion;
      this.MajorImageVersion = majorImageVersion;
      this.MinorImageVersion = minorImageVersion;
      this.MajorSubsystemVersion = majorSubsystemVersion;
      this.MinorSubsystemVersion = minorSubsystemVersion;
      this.Subsystem = subsystem;
      this.DllCharacteristics = dllCharacteristics;
      this.ImageCharacteristics = imageCharacteristics;
      this.SizeOfStackReserve = sizeOfStackReserve;
      this.SizeOfStackCommit = sizeOfStackCommit;
      this.SizeOfHeapReserve = sizeOfHeapReserve;
      this.SizeOfHeapCommit = sizeOfHeapCommit;
    }

    public static PEHeaderBuilder CreateExecutableHeader() => new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage);

    public static PEHeaderBuilder CreateLibraryHeader() => new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll);

    internal bool Is32Bit => this.Machine != Machine.Amd64 && this.Machine != Machine.IA64 && this.Machine != Machine.Arm64;

    internal int ComputeSizeOfPEHeaders(int sectionCount) => 152 + PEHeader.Size(this.Is32Bit) + 40 * sectionCount;
  }
}

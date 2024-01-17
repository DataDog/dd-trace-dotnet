﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.SectionHeader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
  public readonly struct SectionHeader
  {
    internal const int NameSize = 8;
    internal const int Size = 40;

    /// <summary>The name of the section.</summary>
    public string Name { get; }

    /// <summary>
    /// The total size of the section when loaded into memory.
    /// If this value is greater than <see cref="P:System.Reflection.PortableExecutable.SectionHeader.SizeOfRawData" />, the section is zero-padded.
    /// This field is valid only for PE images and should be set to zero for object files.
    /// </summary>
    public int VirtualSize { get; }

    /// <summary>
    /// For PE images, the address of the first byte of the section relative to the image base when the
    /// section is loaded into memory. For object files, this field is the address of the first byte before
    /// relocation is applied; for simplicity, compilers should set this to zero. Otherwise,
    /// it is an arbitrary value that is subtracted from offsets during relocation.
    /// </summary>
    public int VirtualAddress { get; }

    /// <summary>
    /// The size of the section (for object files) or the size of the initialized data on disk (for image files).
    /// For PE images, this must be a multiple of <see cref="P:System.Reflection.PortableExecutable.PEHeader.FileAlignment" />.
    /// If this is less than <see cref="P:System.Reflection.PortableExecutable.SectionHeader.VirtualSize" />, the remainder of the section is zero-filled.
    /// Because the <see cref="P:System.Reflection.PortableExecutable.SectionHeader.SizeOfRawData" /> field is rounded but the <see cref="P:System.Reflection.PortableExecutable.SectionHeader.VirtualSize" /> field is not,
    /// it is possible for <see cref="P:System.Reflection.PortableExecutable.SectionHeader.SizeOfRawData" /> to be greater than <see cref="P:System.Reflection.PortableExecutable.SectionHeader.VirtualSize" /> as well.
    ///  When a section contains only uninitialized data, this field should be zero.
    /// </summary>
    public int SizeOfRawData { get; }

    /// <summary>
    /// The file pointer to the first page of the section within the COFF file.
    /// For PE images, this must be a multiple of <see cref="P:System.Reflection.PortableExecutable.PEHeader.FileAlignment" />.
    /// For object files, the value should be aligned on a 4 byte boundary for best performance.
    /// When a section contains only uninitialized data, this field should be zero.
    /// </summary>
    public int PointerToRawData { get; }

    /// <summary>
    /// The file pointer to the beginning of relocation entries for the section.
    /// This is set to zero for PE images or if there are no relocations.
    /// </summary>
    public int PointerToRelocations { get; }

    /// <summary>
    /// The file pointer to the beginning of line-number entries for the section.
    /// This is set to zero if there are no COFF line numbers.
    /// This value should be zero for an image because COFF debugging information is deprecated.
    /// </summary>
    public int PointerToLineNumbers { get; }

    /// <summary>
    /// The number of relocation entries for the section. This is set to zero for PE images.
    /// </summary>
    public ushort NumberOfRelocations { get; }

    /// <summary>
    /// The number of line-number entries for the section.
    ///  This value should be zero for an image because COFF debugging information is deprecated.
    /// </summary>
    public ushort NumberOfLineNumbers { get; }

    /// <summary>
    /// The flags that describe the characteristics of the section.
    /// </summary>
    public SectionCharacteristics SectionCharacteristics { get; }

    internal SectionHeader(ref PEBinaryReader reader)
    {
      this.Name = reader.ReadNullPaddedUTF8(8);
      this.VirtualSize = reader.ReadInt32();
      this.VirtualAddress = reader.ReadInt32();
      this.SizeOfRawData = reader.ReadInt32();
      this.PointerToRawData = reader.ReadInt32();
      this.PointerToRelocations = reader.ReadInt32();
      this.PointerToLineNumbers = reader.ReadInt32();
      this.NumberOfRelocations = reader.ReadUInt16();
      this.NumberOfLineNumbers = reader.ReadUInt16();
      this.SectionCharacteristics = (SectionCharacteristics) reader.ReadUInt32();
    }
  }
}

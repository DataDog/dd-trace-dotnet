﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEHeader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;

namespace Datadog.System.Reflection.PortableExecutable
{
  public sealed class PEHeader
  {
    internal const int OffsetOfChecksum = 64;

    /// <summary>Identifies the format of the image file.</summary>
    public PEMagic Magic { get; }

    /// <summary>The linker major version number.</summary>
    public byte MajorLinkerVersion { get; }

    /// <summary>The linker minor version number.</summary>
    public byte MinorLinkerVersion { get; }

    /// <summary>
    /// The size of the code (text) section, or the sum of all code sections if there are multiple sections.
    /// </summary>
    public int SizeOfCode { get; }

    /// <summary>
    /// The size of the initialized data section, or the sum of all such sections if there are multiple data sections.
    /// </summary>
    public int SizeOfInitializedData { get; }

    /// <summary>
    /// The size of the uninitialized data section (BSS), or the sum of all such sections if there are multiple BSS sections.
    /// </summary>
    public int SizeOfUninitializedData { get; }

    /// <summary>
    /// The address of the entry point relative to the image base when the PE file is loaded into memory.
    /// For program images, this is the starting address. For device drivers, this is the address of the initialization function.
    /// An entry point is optional for DLLs. When no entry point is present, this field must be zero.
    /// </summary>
    public int AddressOfEntryPoint { get; }

    /// <summary>
    /// The address that is relative to the image base of the beginning-of-code section when it is loaded into memory.
    /// </summary>
    public int BaseOfCode { get; }

    /// <summary>
    /// The address that is relative to the image base of the beginning-of-data section when it is loaded into memory.
    /// </summary>
    public int BaseOfData { get; }

    /// <summary>
    /// The preferred address of the first byte of image when loaded into memory;
    /// must be a multiple of 64K.
    /// </summary>
    public ulong ImageBase { get; }

    /// <summary>
    /// The alignment (in bytes) of sections when they are loaded into memory. It must be greater than or equal to <see cref="P:System.Reflection.PortableExecutable.PEHeader.FileAlignment" />.
    /// The default is the page size for the architecture.
    /// </summary>
    public int SectionAlignment { get; }

    /// <summary>
    /// The alignment factor (in bytes) that is used to align the raw data of sections in the image file.
    /// The value should be a power of 2 between 512 and 64K, inclusive. The default is 512.
    /// If the <see cref="P:System.Reflection.PortableExecutable.PEHeader.SectionAlignment" /> is less than the architecture's page size,
    /// then <see cref="P:System.Reflection.PortableExecutable.PEHeader.FileAlignment" /> must match <see cref="P:System.Reflection.PortableExecutable.PEHeader.SectionAlignment" />.
    /// </summary>
    public int FileAlignment { get; }

    /// <summary>
    /// The major version number of the required operating system.
    /// </summary>
    public ushort MajorOperatingSystemVersion { get; }

    /// <summary>
    /// The minor version number of the required operating system.
    /// </summary>
    public ushort MinorOperatingSystemVersion { get; }

    /// <summary>The major version number of the image.</summary>
    public ushort MajorImageVersion { get; }

    /// <summary>The minor version number of the image.</summary>
    public ushort MinorImageVersion { get; }

    /// <summary>The major version number of the subsystem.</summary>
    public ushort MajorSubsystemVersion { get; }

    /// <summary>The minor version number of the subsystem.</summary>
    public ushort MinorSubsystemVersion { get; }

    /// <summary>
    /// The size (in bytes) of the image, including all headers, as the image is loaded in memory.
    /// It must be a multiple of <see cref="P:System.Reflection.PortableExecutable.PEHeader.SectionAlignment" />.
    /// </summary>
    public int SizeOfImage { get; }

    /// <summary>
    /// The combined size of an MS DOS stub, PE header, and section headers rounded up to a multiple of FileAlignment.
    /// </summary>
    public int SizeOfHeaders { get; }

    /// <summary>The image file checksum.</summary>
    public uint CheckSum { get; }

    /// <summary>The subsystem that is required to run this image.</summary>
    public Subsystem Subsystem { get; }

    public DllCharacteristics DllCharacteristics { get; }

    /// <summary>
    /// The size of the stack to reserve. Only <see cref="P:System.Reflection.PortableExecutable.PEHeader.SizeOfStackCommit" /> is committed;
    /// the rest is made available one page at a time until the reserve size is reached.
    /// </summary>
    public ulong SizeOfStackReserve { get; }

    /// <summary>The size of the stack to commit.</summary>
    public ulong SizeOfStackCommit { get; }

    /// <summary>
    /// The size of the local heap space to reserve. Only <see cref="P:System.Reflection.PortableExecutable.PEHeader.SizeOfHeapCommit" /> is committed;
    /// the rest is made available one page at a time until the reserve size is reached.
    /// </summary>
    public ulong SizeOfHeapReserve { get; }

    /// <summary>The size of the local heap space to commit.</summary>
    public ulong SizeOfHeapCommit { get; }

    /// <summary>
    /// The number of data-directory entries in the remainder of the <see cref="T:System.Reflection.PortableExecutable.PEHeader" />. Each describes a location and size.
    /// </summary>
    public int NumberOfRvaAndSizes { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_EXPORT.</remarks>
    public DirectoryEntry ExportTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_IMPORT.</remarks>
    public DirectoryEntry ImportTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_RESOURCE.</remarks>
    public DirectoryEntry ResourceTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_EXCEPTION.</remarks>
    public DirectoryEntry ExceptionTableDirectory { get; }

    /// <summary>
    /// The Certificate Table entry points to a table of attribute certificates.
    /// </summary>
    /// <remarks>
    /// These certificates are not loaded into memory as part of the image.
    /// As such, the first field of this entry, which is normally an RVA, is a file pointer instead.
    /// 
    /// Aka IMAGE_DIRECTORY_ENTRY_SECURITY.
    /// </remarks>
    public DirectoryEntry CertificateTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_BASERELOC.</remarks>
    public DirectoryEntry BaseRelocationTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_DEBUG.</remarks>
    public DirectoryEntry DebugTableDirectory { get; }

    /// <remarks>
    /// Aka IMAGE_DIRECTORY_ENTRY_COPYRIGHT or IMAGE_DIRECTORY_ENTRY_ARCHITECTURE.
    /// </remarks>
    public DirectoryEntry CopyrightTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_GLOBALPTR.</remarks>
    public DirectoryEntry GlobalPointerTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_TLS.</remarks>
    public DirectoryEntry ThreadLocalStorageTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG.</remarks>
    public DirectoryEntry LoadConfigTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT.</remarks>
    public DirectoryEntry BoundImportTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_IAT.</remarks>
    public DirectoryEntry ImportAddressTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT.</remarks>
    public DirectoryEntry DelayImportTableDirectory { get; }

    /// <remarks>Aka IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR.</remarks>
    public DirectoryEntry CorHeaderTableDirectory { get; }

    internal static int Size(bool is32Bit) => 72 + 4 * (is32Bit ? 4 : 8) + 4 + 4 + 128;

    internal PEHeader(ref PEBinaryReader reader)
    {
      PEMagic peMagic = (PEMagic) reader.ReadUInt16();
      switch (peMagic)
      {
        case PEMagic.PE32:
        case PEMagic.PE32Plus:
          this.Magic = peMagic;
          this.MajorLinkerVersion = reader.ReadByte();
          this.MinorLinkerVersion = reader.ReadByte();
          this.SizeOfCode = reader.ReadInt32();
          this.SizeOfInitializedData = reader.ReadInt32();
          this.SizeOfUninitializedData = reader.ReadInt32();
          this.AddressOfEntryPoint = reader.ReadInt32();
          this.BaseOfCode = reader.ReadInt32();
          this.BaseOfData = peMagic != PEMagic.PE32Plus ? reader.ReadInt32() : 0;
          this.ImageBase = peMagic != PEMagic.PE32Plus ? (ulong) reader.ReadUInt32() : reader.ReadUInt64();
          this.SectionAlignment = reader.ReadInt32();
          this.FileAlignment = reader.ReadInt32();
          this.MajorOperatingSystemVersion = reader.ReadUInt16();
          this.MinorOperatingSystemVersion = reader.ReadUInt16();
          this.MajorImageVersion = reader.ReadUInt16();
          this.MinorImageVersion = reader.ReadUInt16();
          this.MajorSubsystemVersion = reader.ReadUInt16();
          this.MinorSubsystemVersion = reader.ReadUInt16();
          int num1 = (int) reader.ReadUInt32();
          this.SizeOfImage = reader.ReadInt32();
          this.SizeOfHeaders = reader.ReadInt32();
          this.CheckSum = reader.ReadUInt32();
          this.Subsystem = (Subsystem) reader.ReadUInt16();
          this.DllCharacteristics = (DllCharacteristics) reader.ReadUInt16();
          if (peMagic == PEMagic.PE32Plus)
          {
            this.SizeOfStackReserve = reader.ReadUInt64();
            this.SizeOfStackCommit = reader.ReadUInt64();
            this.SizeOfHeapReserve = reader.ReadUInt64();
            this.SizeOfHeapCommit = reader.ReadUInt64();
          }
          else
          {
            this.SizeOfStackReserve = (ulong) reader.ReadUInt32();
            this.SizeOfStackCommit = (ulong) reader.ReadUInt32();
            this.SizeOfHeapReserve = (ulong) reader.ReadUInt32();
            this.SizeOfHeapCommit = (ulong) reader.ReadUInt32();
          }
          int num2 = (int) reader.ReadUInt32();
          this.NumberOfRvaAndSizes = reader.ReadInt32();
          this.ExportTableDirectory = new DirectoryEntry(ref reader);
          this.ImportTableDirectory = new DirectoryEntry(ref reader);
          this.ResourceTableDirectory = new DirectoryEntry(ref reader);
          this.ExceptionTableDirectory = new DirectoryEntry(ref reader);
          this.CertificateTableDirectory = new DirectoryEntry(ref reader);
          this.BaseRelocationTableDirectory = new DirectoryEntry(ref reader);
          this.DebugTableDirectory = new DirectoryEntry(ref reader);
          this.CopyrightTableDirectory = new DirectoryEntry(ref reader);
          this.GlobalPointerTableDirectory = new DirectoryEntry(ref reader);
          this.ThreadLocalStorageTableDirectory = new DirectoryEntry(ref reader);
          this.LoadConfigTableDirectory = new DirectoryEntry(ref reader);
          this.BoundImportTableDirectory = new DirectoryEntry(ref reader);
          this.ImportAddressTableDirectory = new DirectoryEntry(ref reader);
          this.DelayImportTableDirectory = new DirectoryEntry(ref reader);
          this.CorHeaderTableDirectory = new DirectoryEntry(ref reader);
          DirectoryEntry directoryEntry = new DirectoryEntry(ref reader);
          break;
        default:
          throw new BadImageFormatException(SR.UnknownPEMagicValue);
      }
    }
  }
}

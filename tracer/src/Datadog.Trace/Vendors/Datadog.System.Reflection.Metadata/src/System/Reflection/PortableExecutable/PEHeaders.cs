﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEHeaders
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
    /// <summary>
    /// An object used to read PE (Portable Executable) and COFF (Common Object File Format) headers from a stream.
    /// </summary>
    public sealed class PEHeaders
  {

    #nullable disable
    private readonly CoffHeader _coffHeader;
    private readonly PEHeader _peHeader;
    private readonly ImmutableArray<SectionHeader> _sectionHeaders;
    private readonly CorHeader _corHeader;
    private readonly bool _isLoadedImage;
    private readonly int _metadataStartOffset = -1;
    private readonly int _metadataSize;
    private readonly int _coffHeaderStartOffset = -1;
    private readonly int _corHeaderStartOffset = -1;
    private readonly int _peHeaderStartOffset = -1;
    internal const ushort DosSignature = 23117;
    internal const int PESignatureOffsetLocation = 60;
    internal const uint PESignature = 17744;
    internal const int PESignatureSize = 4;


    #nullable enable
    /// <summary>
    /// Reads PE headers from the current location in the stream.
    /// </summary>
    /// <param name="peStream">Stream containing PE image starting at the stream's current position and ending at the end of the stream.</param>
    /// <exception cref="T:System.BadImageFormatException">The data read from stream have invalid format.</exception>
    /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
    /// <exception cref="T:System.ArgumentException">The stream doesn't support seek operations.</exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peStream" /> is null.</exception>
    public PEHeaders(Stream peStream)
      : this(peStream, 0)
    {
    }

    /// <summary>
    /// Reads PE headers from the current location in the stream.
    /// </summary>
    /// <param name="peStream">Stream containing PE image of the given size starting at its current position.</param>
    /// <param name="size">Size of the PE image.</param>
    /// <exception cref="T:System.BadImageFormatException">The data read from stream have invalid format.</exception>
    /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
    /// <exception cref="T:System.ArgumentException">The stream doesn't support seek operations.</exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peStream" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Size is negative or extends past the end of the stream.</exception>
    public PEHeaders(Stream peStream, int size)
      : this(peStream, size, false)
    {
    }

    /// <summary>
    /// Reads PE headers from the current location in the stream.
    /// </summary>
    /// <param name="peStream">Stream containing PE image of the given size starting at its current position.</param>
    /// <param name="size">Size of the PE image.</param>
    /// <param name="isLoadedImage">True if the PE image has been loaded into memory by the OS loader.</param>
    /// <exception cref="T:System.BadImageFormatException">The data read from stream have invalid format.</exception>
    /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
    /// <exception cref="T:System.ArgumentException">The stream doesn't support seek operations.</exception>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="peStream" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Size is negative or extends past the end of the stream.</exception>
    public PEHeaders(Stream peStream, int size, bool isLoadedImage)
    {
      if (peStream == null)
        Throw.ArgumentNull(nameof (peStream));
      if (!peStream.CanRead || !peStream.CanSeek)
        throw new ArgumentException(SR.StreamMustSupportReadAndSeek, nameof (peStream));
      this._isLoadedImage = isLoadedImage;
      int andValidateSize = StreamExtensions.GetAndValidateSize(peStream, size, nameof (peStream));
      PEBinaryReader reader = new PEBinaryReader(peStream, andValidateSize);
      bool isCOFFOnly;
      PEHeaders.SkipDosHeader(ref reader, out isCOFFOnly);
      this._coffHeaderStartOffset = reader.CurrentOffset;
      this._coffHeader = new CoffHeader(ref reader);
      if (!isCOFFOnly)
      {
        this._peHeaderStartOffset = reader.CurrentOffset;
        this._peHeader = new PEHeader(ref reader);
      }
      this._sectionHeaders = this.ReadSectionHeaders(ref reader);
      int startOffset;
      if (!isCOFFOnly && this.TryCalculateCorHeaderOffset((long) andValidateSize, out startOffset))
      {
        this._corHeaderStartOffset = startOffset;
        reader.Seek(startOffset);
        this._corHeader = new CorHeader(ref reader);
      }
      this.CalculateMetadataLocation((long) andValidateSize, out this._metadataStartOffset, out this._metadataSize);
    }

    /// <summary>
    /// Gets the offset (in bytes) from the start of the PE image to the start of the CLI metadata.
    /// or -1 if the image does not contain metadata.
    /// </summary>
    public int MetadataStartOffset => this._metadataStartOffset;

    /// <summary>
    /// Gets the size of the CLI metadata 0 if the image does not contain metadata.)
    /// </summary>
    public int MetadataSize => this._metadataSize;

    /// <summary>Gets the COFF header of the image.</summary>
    public CoffHeader CoffHeader => this._coffHeader;

    /// <summary>
    /// Gets the byte offset from the start of the PE image to the start of the COFF header.
    /// </summary>
    public int CoffHeaderStartOffset => this._coffHeaderStartOffset;

    /// <summary>Determines if the image is Coff only.</summary>
    public bool IsCoffOnly => this._peHeader == null;

    /// <summary>
    /// Gets the PE header of the image or null if the image is COFF only.
    /// </summary>
    public PEHeader? PEHeader => this._peHeader;

    /// <summary>Gets the byte offset from the start of the image to</summary>
    public int PEHeaderStartOffset => this._peHeaderStartOffset;

    /// <summary>Gets the PE section headers.</summary>
    public ImmutableArray<SectionHeader> SectionHeaders => this._sectionHeaders;

    /// <summary>
    /// Gets the CLI header or null if the image does not have one.
    /// </summary>
    public CorHeader? CorHeader => this._corHeader;

    /// <summary>
    /// Gets the byte offset from the start of the image to the COR header or -1 if the image does not have one.
    /// </summary>
    public int CorHeaderStartOffset => this._corHeaderStartOffset;

    /// <summary>
    /// Determines if the image represents a Windows console application.
    /// </summary>
    public bool IsConsoleApplication => this._peHeader != null && this._peHeader.Subsystem == Subsystem.WindowsCui;

    /// <summary>
    /// Determines if the image represents a dynamically linked library.
    /// </summary>
    public bool IsDll => (this._coffHeader.Characteristics & Characteristics.Dll) != 0;

    /// <summary>Determines if the image represents an executable.</summary>
    public bool IsExe => (this._coffHeader.Characteristics & Characteristics.Dll) == (Characteristics) 0;


    #nullable disable
    private bool TryCalculateCorHeaderOffset(long peStreamSize, out int startOffset)
    {
      if (!this.TryGetDirectoryOffset(this._peHeader.CorHeaderTableDirectory, out startOffset, false))
      {
        startOffset = -1;
        return false;
      }
      if (this._peHeader.CorHeaderTableDirectory.Size < 72)
        throw new BadImageFormatException(SR.InvalidCorHeaderSize);
      return true;
    }

    private static void SkipDosHeader(ref PEBinaryReader reader, out bool isCOFFOnly)
    {
      switch (reader.ReadUInt16())
      {
        case 0:
          if (reader.ReadUInt16() == ushort.MaxValue)
            throw new BadImageFormatException(SR.UnknownFileFormat);
          goto default;
        case 23117:
          isCOFFOnly = false;
          break;
        default:
          isCOFFOnly = true;
          reader.Seek(0);
          break;
      }
      if (isCOFFOnly)
        return;
      reader.Seek(60);
      int offset = reader.ReadInt32();
      reader.Seek(offset);
      if (reader.ReadUInt32() != 17744U)
        throw new BadImageFormatException(SR.InvalidPESignature);
    }

    private ImmutableArray<SectionHeader> ReadSectionHeaders(ref PEBinaryReader reader)
    {
      int numberOfSections = (int) this._coffHeader.NumberOfSections;
      ImmutableArray<SectionHeader>.Builder builder = numberOfSections >= 0 ? ImmutableArray.CreateBuilder<SectionHeader>(numberOfSections) : throw new BadImageFormatException(SR.InvalidNumberOfSections);
      for (int index = 0; index < numberOfSections; ++index)
        builder.Add(new SectionHeader(ref reader));
      return builder.MoveToImmutable();
    }


    #nullable enable
    /// <summary>
    /// Gets the offset (in bytes) from the start of the image to the given directory data.
    /// </summary>
    /// <param name="directory">PE directory entry</param>
    /// <param name="offset">Offset from the start of the image to the given directory data</param>
    /// <returns>True if the directory data is found, false otherwise.</returns>
    public bool TryGetDirectoryOffset(DirectoryEntry directory, out int offset) => this.TryGetDirectoryOffset(directory, out offset, true);

    internal bool TryGetDirectoryOffset(
      DirectoryEntry directory,
      out int offset,
      bool canCrossSectionBoundary)
    {
      int containingSectionIndex = this.GetContainingSectionIndex(directory.RelativeVirtualAddress);
      if (containingSectionIndex < 0)
      {
        offset = -1;
        return false;
      }
      int relativeVirtualAddress = directory.RelativeVirtualAddress;
      SectionHeader sectionHeader = this._sectionHeaders[containingSectionIndex];
      int virtualAddress = sectionHeader.VirtualAddress;
      int num1 = relativeVirtualAddress - virtualAddress;
      if (!canCrossSectionBoundary)
      {
        int size = directory.Size;
        sectionHeader = this._sectionHeaders[containingSectionIndex];
        int num2 = sectionHeader.VirtualSize - num1;
        if (size > num2)
          throw new BadImageFormatException(SR.SectionTooSmall);
      }

      offset = 0;
      ref int local = ref offset;
      int num3;
      if (!this._isLoadedImage)
      {
        sectionHeader = this._sectionHeaders[containingSectionIndex];
        num3 = sectionHeader.PointerToRawData + num1;
      }
      else
        num3 = directory.RelativeVirtualAddress;
      local = num3;
      return true;
    }

    /// <summary>
    /// Searches sections of the PE image for the one that contains specified Relative Virtual Address.
    /// </summary>
    /// <param name="relativeVirtualAddress">Address.</param>
    /// <returns>
    /// Index of the section that contains <paramref name="relativeVirtualAddress" />,
    /// or -1 if there is none.
    /// </returns>
    public int GetContainingSectionIndex(int relativeVirtualAddress)
    {
      for (int index = 0; index < this._sectionHeaders.Length; ++index)
      {
        SectionHeader sectionHeader = this._sectionHeaders[index];
        if (sectionHeader.VirtualAddress <= relativeVirtualAddress)
        {
          int num1 = relativeVirtualAddress;
          sectionHeader = this._sectionHeaders[index];
          int virtualAddress = sectionHeader.VirtualAddress;
          sectionHeader = this._sectionHeaders[index];
          int virtualSize = sectionHeader.VirtualSize;
          int num2 = virtualAddress + virtualSize;
          if (num1 < num2)
            return index;
        }
      }
      return -1;
    }

    internal int IndexOfSection(string name)
    {
      for (int index = 0; index < this.SectionHeaders.Length; ++index)
      {
        if (this.SectionHeaders[index].Name.Equals(name, StringComparison.Ordinal))
          return index;
      }
      return -1;
    }


    #nullable disable
    private void CalculateMetadataLocation(long peImageSize, out int start, out int size)
    {
      if (this.IsCoffOnly)
      {
        int index = this.IndexOfSection(".cormeta");
        if (index == -1)
        {
          start = -1;
          size = 0;
          return;
        }
        if (this._isLoadedImage)
        {
          start = this.SectionHeaders[index].VirtualAddress;
          size = this.SectionHeaders[index].VirtualSize;
        }
        else
        {
          start = this.SectionHeaders[index].PointerToRawData;
          size = this.SectionHeaders[index].SizeOfRawData;
        }
      }
      else
      {
        if (this._corHeader == null)
        {
          start = 0;
          size = 0;
          return;
        }
        if (!this.TryGetDirectoryOffset(this._corHeader.MetadataDirectory, out start, false))
          throw new BadImageFormatException(SR.MissingDataDirectory);
        size = this._corHeader.MetadataDirectory.Size;
      }
      if (start < 0 || (long) start >= peImageSize || size <= 0 || (long) start > peImageSize - (long) size)
        throw new BadImageFormatException(SR.InvalidMetadataSectionSpan);
    }
  }
}

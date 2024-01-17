﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;
using Datadog.System.Reflection.Metadata;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
    public abstract class PEBuilder
  {

    #nullable disable
    private readonly Lazy<ImmutableArray<PEBuilder.Section>> _lazySections;
    private Blob _lazyChecksum;
    internal const int DosHeaderSize = 128;


    #nullable enable
    public PEHeaderBuilder Header { get; }

    public Func<IEnumerable<Blob>, BlobContentId> IdProvider { get; }

    public bool IsDeterministic { get; }

    protected PEBuilder(
      PEHeaderBuilder header,
      Func<IEnumerable<Blob>, BlobContentId>? deterministicIdProvider)
    {
      if (header == null)
        Throw.ArgumentNull(nameof (header));
      this.IdProvider = deterministicIdProvider ?? BlobContentId.GetTimeBasedProvider();
      this.IsDeterministic = deterministicIdProvider != null;
      this.Header = header;
      this._lazySections = new Lazy<ImmutableArray<PEBuilder.Section>>(new Func<ImmutableArray<PEBuilder.Section>>(this.CreateSections));
    }

    protected ImmutableArray<PEBuilder.Section> GetSections()
    {
      ImmutableArray<PEBuilder.Section> immutableArray = this._lazySections.Value;
      return !immutableArray.IsDefault ? immutableArray : throw new InvalidOperationException(SR.Format(SR.MustNotReturnNull, (object) "CreateSections"));
    }

    protected abstract ImmutableArray<PEBuilder.Section> CreateSections();

    protected abstract BlobBuilder SerializeSection(string name, SectionLocation location);

    protected internal abstract PEDirectoriesBuilder GetDirectories();

    public BlobContentId Serialize(BlobBuilder builder)
    {
      ImmutableArray<PEBuilder.SerializedSection> immutableArray = this.SerializeSections();
      PEDirectoriesBuilder directories = this.GetDirectories();
      PEBuilder.WritePESignature(builder);
      Blob stampFixup;
      this.WriteCoffHeader(builder, immutableArray, out stampFixup);
      this.WritePEHeader(builder, directories, immutableArray);
      PEBuilder.WriteSectionHeaders(builder, immutableArray);
      builder.Align(this.Header.FileAlignment);
      foreach (PEBuilder.SerializedSection serializedSection in immutableArray)
      {
        builder.LinkSuffix(serializedSection.Builder);
        builder.Align(this.Header.FileAlignment);
      }
      BlobContentId blobContentId = this.IdProvider((IEnumerable<Blob>) builder.GetBlobs());
      new BlobWriter(stampFixup).WriteUInt32(blobContentId.Stamp);
      return blobContentId;
    }


    #nullable disable
    private ImmutableArray<PEBuilder.SerializedSection> SerializeSections()
    {
      ImmutableArray<PEBuilder.Section> sections = this.GetSections();
      ImmutableArray<PEBuilder.SerializedSection>.Builder builder1 = ImmutableArray.CreateBuilder<PEBuilder.SerializedSection>(sections.Length);
      int sizeOfPeHeaders = this.Header.ComputeSizeOfPEHeaders(sections.Length);
      int relativeVirtualAddress = BitArithmetic.Align(sizeOfPeHeaders, this.Header.SectionAlignment);
      int pointerToRawData = BitArithmetic.Align(sizeOfPeHeaders, this.Header.FileAlignment);
      foreach (PEBuilder.Section section in sections)
      {
        BlobBuilder builder2 = this.SerializeSection(section.Name, new SectionLocation(relativeVirtualAddress, pointerToRawData));
        PEBuilder.SerializedSection serializedSection = new PEBuilder.SerializedSection(builder2, section.Name, section.Characteristics, relativeVirtualAddress, BitArithmetic.Align(builder2.Count, this.Header.FileAlignment), pointerToRawData);
        builder1.Add(serializedSection);
        relativeVirtualAddress = BitArithmetic.Align(serializedSection.RelativeVirtualAddress + serializedSection.VirtualSize, this.Header.SectionAlignment);
        pointerToRawData = serializedSection.PointerToRawData + serializedSection.SizeOfRawData;
      }
      return builder1.MoveToImmutable();
    }

    private static unsafe void WritePESignature(BlobBuilder builder)
    {
      ReadOnlySpan<byte> dosHeader = PEBuilder.DosHeader;
      fixed (byte* buffer = &dosHeader.GetPinnableReference())
        builder.WriteBytes(buffer, dosHeader.Length);
      builder.WriteUInt32(17744U);
    }


    #nullable enable
      private static ReadOnlySpan<byte> DosHeader => new byte[DosHeaderSize]
      {
          0x4d, 0x5a, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00,
          0x04, 0x00, 0x00, 0x00, 0xff, 0xff, 0x00, 0x00,
          0xb8, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
          0x40, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
          0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
          0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
          0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
          0x00, 0x00, 0x00, 0x00,

          0x80, 0x00, 0x00, 0x00, // NT Header offset (0x80 == DosHeader.Length)

          0x0e, 0x1f, 0xba, 0x0e, 0x00, 0xb4, 0x09, 0xcd,
          0x21, 0xb8, 0x01, 0x4c, 0xcd, 0x21, 0x54, 0x68,
          0x69, 0x73, 0x20, 0x70, 0x72, 0x6f, 0x67, 0x72,
          0x61, 0x6d, 0x20, 0x63, 0x61, 0x6e, 0x6e, 0x6f,
          0x74, 0x20, 0x62, 0x65, 0x20, 0x72, 0x75, 0x6e,
          0x20, 0x69, 0x6e, 0x20, 0x44, 0x4f, 0x53, 0x20,
          0x6d, 0x6f, 0x64, 0x65, 0x2e, 0x0d, 0x0d, 0x0a,
          0x24, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
      };

    #nullable disable
    private void WriteCoffHeader(
      BlobBuilder builder,
      ImmutableArray<PEBuilder.SerializedSection> sections,
      out Blob stampFixup)
    {
      builder.WriteUInt16(this.Header.Machine == Machine.Unknown ? (ushort) 332 : (ushort) this.Header.Machine);
      builder.WriteUInt16((ushort) sections.Length);
      stampFixup = builder.ReserveBytes(4);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt16((ushort) PEHeader.Size(this.Header.Is32Bit));
      builder.WriteUInt16((ushort) this.Header.ImageCharacteristics);
    }

    private void WritePEHeader(
      BlobBuilder builder,
      PEDirectoriesBuilder directories,
      ImmutableArray<PEBuilder.SerializedSection> sections)
    {
      builder.WriteUInt16(this.Header.Is32Bit ? (ushort) 267 : (ushort) 523);
      builder.WriteByte(this.Header.MajorLinkerVersion);
      builder.WriteByte(this.Header.MinorLinkerVersion);
      builder.WriteUInt32((uint) PEBuilder.SumRawDataSizes(sections, SectionCharacteristics.ContainsCode));
      builder.WriteUInt32((uint) PEBuilder.SumRawDataSizes(sections, SectionCharacteristics.ContainsInitializedData));
      builder.WriteUInt32((uint) PEBuilder.SumRawDataSizes(sections, SectionCharacteristics.ContainsUninitializedData));
      builder.WriteUInt32((uint) directories.AddressOfEntryPoint);
      int index1 = PEBuilder.IndexOfSection(sections, SectionCharacteristics.ContainsCode);
      builder.WriteUInt32(index1 != -1 ? (uint) sections[index1].RelativeVirtualAddress : 0U);
      if (this.Header.Is32Bit)
      {
        int index2 = PEBuilder.IndexOfSection(sections, SectionCharacteristics.ContainsInitializedData);
        builder.WriteUInt32(index2 != -1 ? (uint) sections[index2].RelativeVirtualAddress : 0U);
        builder.WriteUInt32((uint) this.Header.ImageBase);
      }
      else
        builder.WriteUInt64(this.Header.ImageBase);
      builder.WriteUInt32((uint) this.Header.SectionAlignment);
      builder.WriteUInt32((uint) this.Header.FileAlignment);
      builder.WriteUInt16(this.Header.MajorOperatingSystemVersion);
      builder.WriteUInt16(this.Header.MinorOperatingSystemVersion);
      builder.WriteUInt16(this.Header.MajorImageVersion);
      builder.WriteUInt16(this.Header.MinorImageVersion);
      builder.WriteUInt16(this.Header.MajorSubsystemVersion);
      builder.WriteUInt16(this.Header.MinorSubsystemVersion);
      builder.WriteUInt32(0U);
      PEBuilder.SerializedSection section = sections[sections.Length - 1];
      builder.WriteUInt32((uint) BitArithmetic.Align(section.RelativeVirtualAddress + section.VirtualSize, this.Header.SectionAlignment));
      builder.WriteUInt32((uint) BitArithmetic.Align(this.Header.ComputeSizeOfPEHeaders(sections.Length), this.Header.FileAlignment));
      this._lazyChecksum = builder.ReserveBytes(4);
      new BlobWriter(this._lazyChecksum).WriteUInt32(0U);
      builder.WriteUInt16((ushort) this.Header.Subsystem);
      builder.WriteUInt16((ushort) this.Header.DllCharacteristics);
      if (this.Header.Is32Bit)
      {
        builder.WriteUInt32((uint) this.Header.SizeOfStackReserve);
        builder.WriteUInt32((uint) this.Header.SizeOfStackCommit);
        builder.WriteUInt32((uint) this.Header.SizeOfHeapReserve);
        builder.WriteUInt32((uint) this.Header.SizeOfHeapCommit);
      }
      else
      {
        builder.WriteUInt64(this.Header.SizeOfStackReserve);
        builder.WriteUInt64(this.Header.SizeOfStackCommit);
        builder.WriteUInt64(this.Header.SizeOfHeapReserve);
        builder.WriteUInt64(this.Header.SizeOfHeapCommit);
      }
      builder.WriteUInt32(0U);
      builder.WriteUInt32(16U);
      builder.WriteUInt32((uint) directories.ExportTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.ExportTable.Size);
      builder.WriteUInt32((uint) directories.ImportTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.ImportTable.Size);
      builder.WriteUInt32((uint) directories.ResourceTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.ResourceTable.Size);
      builder.WriteUInt32((uint) directories.ExceptionTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.ExceptionTable.Size);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32((uint) directories.BaseRelocationTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.BaseRelocationTable.Size);
      builder.WriteUInt32((uint) directories.DebugTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.DebugTable.Size);
      builder.WriteUInt32((uint) directories.CopyrightTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.CopyrightTable.Size);
      builder.WriteUInt32((uint) directories.GlobalPointerTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.GlobalPointerTable.Size);
      builder.WriteUInt32((uint) directories.ThreadLocalStorageTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.ThreadLocalStorageTable.Size);
      builder.WriteUInt32((uint) directories.LoadConfigTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.LoadConfigTable.Size);
      builder.WriteUInt32((uint) directories.BoundImportTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.BoundImportTable.Size);
      builder.WriteUInt32((uint) directories.ImportAddressTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.ImportAddressTable.Size);
      builder.WriteUInt32((uint) directories.DelayImportTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.DelayImportTable.Size);
      builder.WriteUInt32((uint) directories.CorHeaderTable.RelativeVirtualAddress);
      builder.WriteUInt32((uint) directories.CorHeaderTable.Size);
      builder.WriteUInt64(0UL);
    }

    private static void WriteSectionHeaders(
      BlobBuilder builder,
      ImmutableArray<PEBuilder.SerializedSection> serializedSections)
    {
      foreach (PEBuilder.SerializedSection serializedSection in serializedSections)
        PEBuilder.WriteSectionHeader(builder, serializedSection);
    }

    private static void WriteSectionHeader(
      BlobBuilder builder,
      PEBuilder.SerializedSection serializedSection)
    {
      if (serializedSection.VirtualSize == 0)
        return;
      int index = 0;
      int length = serializedSection.Name.Length;
      for (; index < 8; ++index)
      {
        if (index < length)
          builder.WriteByte((byte) serializedSection.Name[index]);
        else
          builder.WriteByte((byte) 0);
      }
      builder.WriteUInt32((uint) serializedSection.VirtualSize);
      builder.WriteUInt32((uint) serializedSection.RelativeVirtualAddress);
      builder.WriteUInt32((uint) serializedSection.SizeOfRawData);
      builder.WriteUInt32((uint) serializedSection.PointerToRawData);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt16((ushort) 0);
      builder.WriteUInt16((ushort) 0);
      builder.WriteUInt32((uint) serializedSection.Characteristics);
    }

    private static int IndexOfSection(
      ImmutableArray<PEBuilder.SerializedSection> sections,
      SectionCharacteristics characteristics)
    {
      for (int index = 0; index < sections.Length; ++index)
      {
        if ((sections[index].Characteristics & characteristics) == characteristics)
          return index;
      }
      return -1;
    }

    private static int SumRawDataSizes(
      ImmutableArray<PEBuilder.SerializedSection> sections,
      SectionCharacteristics characteristics)
    {
      int num = 0;
      for (int index = 0; index < sections.Length; ++index)
      {
        if ((sections[index].Characteristics & characteristics) == characteristics)
          num += sections[index].SizeOfRawData;
      }
      return num;
    }


    #nullable enable
    internal static IEnumerable<Blob> GetContentToSign(
      BlobBuilder peImage,
      int peHeadersSize,
      int peHeaderAlignment,
      Blob strongNameSignatureFixup)
    {
      int remainingHeaderToSign = peHeadersSize;
      int remainingHeader = BitArithmetic.Align(peHeadersSize, peHeaderAlignment);
      foreach (Blob blob1 in peImage.GetBlobs())
      {
        Blob blob = blob1;
        int blobStart = blob.Start;
        int length;
        for (int blobLength = blob.Length; blobLength > 0; blobLength -= length)
        {
          if (remainingHeader > 0)
          {
            if (remainingHeaderToSign > 0)
            {
              length = Math.Min(remainingHeaderToSign, blobLength);
              yield return new Blob(blob.Buffer, blobStart, length);
              remainingHeaderToSign -= length;
            }
            else
              length = Math.Min(remainingHeader, blobLength);
            remainingHeader -= length;
            blobStart += length;
          }
          else
          {
            if (blob.Buffer == strongNameSignatureFixup.Buffer)
            {
              yield return PEBuilder.GetPrefixBlob(new Blob(blob.Buffer, blobStart, blobLength), strongNameSignatureFixup);
              yield return PEBuilder.GetSuffixBlob(new Blob(blob.Buffer, blobStart, blobLength), strongNameSignatureFixup);
              break;
            }
            yield return new Blob(blob.Buffer, blobStart, blobLength);
            break;
          }
        }
        blob = new Blob();
      }
    }

    internal static Blob GetPrefixBlob(Blob container, Blob blob) => new Blob(container.Buffer, container.Start, blob.Start - container.Start);

    internal static Blob GetSuffixBlob(Blob container, Blob blob) => new Blob(container.Buffer, blob.Start + blob.Length, container.Start + container.Length - blob.Start - blob.Length);

    internal static IEnumerable<Blob> GetContentToChecksum(BlobBuilder peImage, Blob checksumFixup)
    {
      foreach (Blob blob1 in peImage.GetBlobs())
      {
        Blob blob = blob1;
        if (blob.Buffer == checksumFixup.Buffer)
        {
          yield return PEBuilder.GetPrefixBlob(blob, checksumFixup);
          yield return PEBuilder.GetSuffixBlob(blob, checksumFixup);
        }
        else
          yield return blob;
        blob = new Blob();
      }
    }

    internal void Sign(
      BlobBuilder peImage,
      Blob strongNameSignatureFixup,
      Func<IEnumerable<Blob>, byte[]> signatureProvider)
    {
      int sizeOfPeHeaders = this.Header.ComputeSizeOfPEHeaders(this.GetSections().Length);
      byte[] buffer = signatureProvider(PEBuilder.GetContentToSign(peImage, sizeOfPeHeaders, this.Header.FileAlignment, strongNameSignatureFixup));
      if (buffer == null || buffer.Length > strongNameSignatureFixup.Length)
        throw new InvalidOperationException(SR.SignatureProviderReturnedInvalidSignature);
      new BlobWriter(strongNameSignatureFixup).WriteBytes(buffer);
      new BlobWriter(this._lazyChecksum).WriteUInt32(PEBuilder.CalculateChecksum(peImage, this._lazyChecksum));
    }

    internal static uint CalculateChecksum(BlobBuilder peImage, Blob checksumFixup) => PEBuilder.CalculateChecksum(PEBuilder.GetContentToChecksum(peImage, checksumFixup)) + (uint) peImage.Count;


    #nullable disable
    private static unsafe uint CalculateChecksum(IEnumerable<Blob> blobs)
    {
      uint checksum = 0;
      int num = -1;
      foreach (Blob blob in blobs)
      {
        ArraySegment<byte> bytes = blob.GetBytes();
        fixed (byte* numPtr1 = bytes.Array)
        {
          byte* numPtr2 = numPtr1 + bytes.Offset;
          byte* numPtr3 = numPtr2 + bytes.Count;
          if (num >= 0)
          {
            checksum = PEBuilder.AggregateChecksum(checksum, (ushort) ((int) *numPtr2 << 8 | num));
            ++numPtr2;
          }
          if ((numPtr3 - numPtr2) % 2L != 0L)
          {
            --numPtr3;
            num = (int) *numPtr3;
          }
          else
            num = -1;
          for (; numPtr2 < numPtr3; numPtr2 += 2)
            checksum = PEBuilder.AggregateChecksum(checksum, (ushort) ((uint) numPtr2[1] << 8 | (uint) *numPtr2));
        }
      }
      if (num >= 0)
        checksum = PEBuilder.AggregateChecksum(checksum, (ushort) num);
      return checksum;
    }

    private static uint AggregateChecksum(uint checksum, ushort value)
    {
      uint num = checksum + (uint) value;
      return (num >> 16) + (uint) (ushort) num;
    }


    #nullable enable
    protected readonly struct Section
    {
      public readonly string Name;
      public readonly SectionCharacteristics Characteristics;

      public Section(string name, SectionCharacteristics characteristics)
      {
        if (name == null)
          Throw.ArgumentNull(nameof (name));
        this.Name = name;
        this.Characteristics = characteristics;
      }
    }


    #nullable disable
    private readonly struct SerializedSection
    {
      public readonly BlobBuilder Builder;
      public readonly string Name;
      public readonly SectionCharacteristics Characteristics;
      public readonly int RelativeVirtualAddress;
      public readonly int SizeOfRawData;
      public readonly int PointerToRawData;

      public SerializedSection(
        BlobBuilder builder,
        string name,
        SectionCharacteristics characteristics,
        int relativeVirtualAddress,
        int sizeOfRawData,
        int pointerToRawData)
      {
        this.Name = name;
        this.Characteristics = characteristics;
        this.Builder = builder;
        this.RelativeVirtualAddress = relativeVirtualAddress;
        this.SizeOfRawData = sizeOfRawData;
        this.PointerToRawData = pointerToRawData;
      }

      public int VirtualSize => this.Builder.Count;
    }
  }
}

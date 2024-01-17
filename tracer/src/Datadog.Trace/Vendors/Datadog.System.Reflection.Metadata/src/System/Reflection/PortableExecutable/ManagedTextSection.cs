﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.ManagedTextSection
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Reflection.Internal;
using Datadog.System.Reflection.Metadata;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
  /// <summary>Managed .text PE section.</summary>
  /// <remarks>
  /// Contains in the following order:
  /// - Import Address Table
  /// - COR Header
  /// - IL
  /// - Metadata
  /// - Managed Resource Data
  /// - Strong Name Signature
  /// - Debug Data (directory and extra info)
  /// - Import Table
  /// - Name Table
  /// - Runtime Startup Stub
  /// - Mapped Field Data
  /// </remarks>
  internal sealed class ManagedTextSection
  {
    public const int ManagedResourcesDataAlignment = 8;

    #nullable disable
    private const string CorEntryPointDll = "mscoree.dll";
    public const int MappedFieldDataAlignment = 8;
    private const int CorHeaderSize = 72;

    public Characteristics ImageCharacteristics { get; }

    public Machine Machine { get; }

    /// <summary>The size of IL stream (unaligned).</summary>
    public int ILStreamSize { get; }

    /// <summary>Total size of metadata (header and all streams).</summary>
    public int MetadataSize { get; }

    /// <summary>
    /// The size of managed resource data stream.
    /// Aligned to <see cref="F:System.Reflection.PortableExecutable.ManagedTextSection.ManagedResourcesDataAlignment" />.
    /// </summary>
    public int ResourceDataSize { get; }

    /// <summary>Size of strong name hash.</summary>
    public int StrongNameSignatureSize { get; }

    /// <summary>Size of Debug data.</summary>
    public int DebugDataSize { get; }

    /// <summary>
    /// The size of mapped field data stream.
    /// Aligned to <see cref="F:System.Reflection.PortableExecutable.ManagedTextSection.MappedFieldDataAlignment" />.
    /// </summary>
    public int MappedFieldDataSize { get; }

    public ManagedTextSection(
      Characteristics imageCharacteristics,
      Machine machine,
      int ilStreamSize,
      int metadataSize,
      int resourceDataSize,
      int strongNameSignatureSize,
      int debugDataSize,
      int mappedFieldDataSize)
    {
      this.MetadataSize = metadataSize;
      this.ResourceDataSize = resourceDataSize;
      this.ILStreamSize = ilStreamSize;
      this.MappedFieldDataSize = mappedFieldDataSize;
      this.StrongNameSignatureSize = strongNameSignatureSize;
      this.ImageCharacteristics = imageCharacteristics;
      this.Machine = machine;
      this.DebugDataSize = debugDataSize;
    }

    /// <summary>
    /// If set, the module must include a machine code stub that transfers control to the virtual execution system.
    /// </summary>
    internal bool RequiresStartupStub => this.Machine == Machine.I386 || this.Machine == Machine.Unknown;

    /// <summary>
    /// If set, the module contains instructions that assume a 64 bit instruction set. For example it may depend on an address being 64 bits.
    /// This may be true even if the module contains only IL instructions because of PlatformInvoke and COM interop.
    /// </summary>
    internal bool Requires64bits => this.Machine == Machine.Amd64 || this.Machine == Machine.IA64 || this.Machine == Machine.Arm64;

    public bool Is32Bit => !this.Requires64bits;


    #nullable enable
    private string CorEntryPointName => (this.ImageCharacteristics & Characteristics.Dll) == (Characteristics) 0 ? "_CorExeMain" : "_CorDllMain";

    private int SizeOfImportAddressTable
    {
      get
      {
        if (!this.RequiresStartupStub)
          return 0;
        return !this.Is32Bit ? 16 : 8;
      }
    }

    private int SizeOfImportTable => 40 + (this.Is32Bit ? 12 : 16) + 2 + this.CorEntryPointName.Length + 1;

    private static int SizeOfNameTable => "mscoree.dll".Length + 1 + 2;

    private int SizeOfRuntimeStartupStub => !this.Is32Bit ? 16 : 8;

    internal int CalculateOffsetToMappedFieldDataStreamUnaligned()
    {
      int dataStreamUnaligned = this.ComputeOffsetToImportTable();
      if (this.RequiresStartupStub)
        dataStreamUnaligned = BitArithmetic.Align(dataStreamUnaligned + (this.SizeOfImportTable + ManagedTextSection.SizeOfNameTable), this.Is32Bit ? 4 : 8) + this.SizeOfRuntimeStartupStub;
      return dataStreamUnaligned;
    }

    public int CalculateOffsetToMappedFieldDataStream()
    {
      int position = this.CalculateOffsetToMappedFieldDataStreamUnaligned();
      if (this.MappedFieldDataSize != 0)
        position = BitArithmetic.Align(position, 8);
      return position;
    }

    internal int ComputeOffsetToDebugDirectory() => this.ComputeOffsetToMetadata() + this.MetadataSize + this.ResourceDataSize + this.StrongNameSignatureSize;

    private int ComputeOffsetToImportTable() => this.ComputeOffsetToDebugDirectory() + this.DebugDataSize;

    public int OffsetToILStream => this.SizeOfImportAddressTable + 72;

    private int ComputeOffsetToMetadata() => this.OffsetToILStream + BitArithmetic.Align(this.ILStreamSize, 4);

    public int ComputeSizeOfTextSection() => this.CalculateOffsetToMappedFieldDataStream() + this.MappedFieldDataSize;

    public int GetEntryPointAddress(int rva) => !this.RequiresStartupStub ? 0 : rva + this.CalculateOffsetToMappedFieldDataStreamUnaligned() - (this.Is32Bit ? 6 : 10);

    public DirectoryEntry GetImportAddressTableDirectoryEntry(int rva) => !this.RequiresStartupStub ? new DirectoryEntry() : new DirectoryEntry(rva, this.SizeOfImportAddressTable);

    public DirectoryEntry GetImportTableDirectoryEntry(int rva) => !this.RequiresStartupStub ? new DirectoryEntry() : new DirectoryEntry(rva + this.ComputeOffsetToImportTable(), (this.Is32Bit ? 66 : 70) + 13);

    public DirectoryEntry GetCorHeaderDirectoryEntry(int rva) => new DirectoryEntry(rva + this.SizeOfImportAddressTable, 72);

    /// <summary>
    /// Serializes .text section data into a specified <paramref name="builder" />.
    /// </summary>
    /// <param name="builder">An empty builder to serialize section data to.</param>
    /// <param name="relativeVirtualAddess">Relative virtual address of the section within the containing PE file.</param>
    /// <param name="entryPointTokenOrRelativeVirtualAddress">Entry point token or RVA (<see cref="P:System.Reflection.PortableExecutable.CorHeader.EntryPointTokenOrRelativeVirtualAddress" />)</param>
    /// <param name="corFlags">COR Flags (<see cref="P:System.Reflection.PortableExecutable.CorHeader.Flags" />).</param>
    /// <param name="baseAddress">Base address of the PE image.</param>
    /// <param name="metadataBuilder"><see cref="T:System.Reflection.Metadata.BlobBuilder" /> containing metadata. Must be populated with data. Linked into the <paramref name="builder" /> and can't be expanded afterwards.</param>
    /// <param name="ilBuilder"><see cref="T:System.Reflection.Metadata.BlobBuilder" /> containing IL stream. Must be populated with data. Linked into the <paramref name="builder" /> and can't be expanded afterwards.</param>
    /// <param name="mappedFieldDataBuilderOpt"><see cref="T:System.Reflection.Metadata.BlobBuilder" /> containing mapped field data. Must be populated with data. Linked into the <paramref name="builder" /> and can't be expanded afterwards.</param>
    /// <param name="resourceBuilderOpt"><see cref="T:System.Reflection.Metadata.BlobBuilder" /> containing managed resource data. Must be populated with data. Linked into the <paramref name="builder" /> and can't be expanded afterwards.</param>
    /// <param name="debugDataBuilderOpt"><see cref="T:System.Reflection.Metadata.BlobBuilder" /> containing PE debug table and data. Must be populated with data. Linked into the <paramref name="builder" /> and can't be expanded afterwards.</param>
    /// <param name="strongNameSignature">Blob reserved in the <paramref name="builder" /> for strong name signature.</param>
    public void Serialize(
      BlobBuilder builder,
      int relativeVirtualAddess,
      int entryPointTokenOrRelativeVirtualAddress,
      CorFlags corFlags,
      ulong baseAddress,
      BlobBuilder metadataBuilder,
      BlobBuilder ilBuilder,
      BlobBuilder? mappedFieldDataBuilderOpt,
      BlobBuilder? resourceBuilderOpt,
      BlobBuilder? debugDataBuilderOpt,
      out Blob strongNameSignature)
    {
      int relativeVirtualAddress1 = this.GetImportTableDirectoryEntry(relativeVirtualAddess).RelativeVirtualAddress;
      int relativeVirtualAddress2 = this.GetImportAddressTableDirectoryEntry(relativeVirtualAddess).RelativeVirtualAddress;
      if (this.RequiresStartupStub)
        this.WriteImportAddressTable(builder, relativeVirtualAddress1);
      this.WriteCorHeader(builder, relativeVirtualAddess, entryPointTokenOrRelativeVirtualAddress, corFlags);
      ilBuilder.Align(4);
      builder.LinkSuffix(ilBuilder);
      builder.LinkSuffix(metadataBuilder);
      if (resourceBuilderOpt != null)
        builder.LinkSuffix(resourceBuilderOpt);
      strongNameSignature = builder.ReserveBytes(this.StrongNameSignatureSize);
      new BlobWriter(strongNameSignature).WriteBytes((byte) 0, this.StrongNameSignatureSize);
      if (debugDataBuilderOpt != null)
        builder.LinkSuffix(debugDataBuilderOpt);
      if (this.RequiresStartupStub)
      {
        this.WriteImportTable(builder, relativeVirtualAddress1, relativeVirtualAddress2);
        ManagedTextSection.WriteNameTable(builder);
        this.WriteRuntimeStartupStub(builder, relativeVirtualAddress2, baseAddress);
      }
      if (mappedFieldDataBuilderOpt == null)
        return;
      if (mappedFieldDataBuilderOpt.Count != 0)
        builder.Align(8);
      builder.LinkSuffix(mappedFieldDataBuilderOpt);
    }


    #nullable disable
    private void WriteImportAddressTable(BlobBuilder builder, int importTableRva)
    {
      int count = builder.Count;
      int num = importTableRva + 40 + (this.Is32Bit ? 12 : 16);
      if (this.Is32Bit)
      {
        builder.WriteUInt32((uint) num);
        builder.WriteUInt32(0U);
      }
      else
      {
        builder.WriteUInt64((ulong) (uint) num);
        builder.WriteUInt64(0UL);
      }
    }

    private void WriteImportTable(
      BlobBuilder builder,
      int importTableRva,
      int importAddressTableRva)
    {
      int count = builder.Count;
      int num1 = importTableRva + 40;
      int num2 = num1 + (this.Is32Bit ? 12 : 16);
      int num3 = num2 + 12 + 2;
      builder.WriteUInt32((uint) num1);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32((uint) num3);
      builder.WriteUInt32((uint) importAddressTableRva);
      builder.WriteBytes((byte) 0, 20);
      if (this.Is32Bit)
      {
        builder.WriteUInt32((uint) num2);
        builder.WriteUInt32(0U);
        builder.WriteUInt32(0U);
      }
      else
      {
        builder.WriteUInt64((ulong) (uint) num2);
        builder.WriteUInt64(0UL);
      }
      builder.WriteUInt16((ushort) 0);
      foreach (char ch in this.CorEntryPointName)
        builder.WriteByte((byte) ch);
      builder.WriteByte((byte) 0);
    }

    private static void WriteNameTable(BlobBuilder builder)
    {
      int count = builder.Count;
      foreach (char ch in "mscoree.dll")
        builder.WriteByte((byte) ch);
      builder.WriteByte((byte) 0);
      builder.WriteUInt16((ushort) 0);
    }

    private void WriteCorHeader(
      BlobBuilder builder,
      int textSectionRva,
      int entryPointTokenOrRva,
      CorFlags corFlags)
    {
      int num1 = textSectionRva + this.ComputeOffsetToMetadata();
      int num2 = num1 + this.MetadataSize;
      int num3 = num2 + this.ResourceDataSize;
      int count = builder.Count;
      builder.WriteUInt32(72U);
      builder.WriteUInt16((ushort) 2);
      builder.WriteUInt16((ushort) 5);
      builder.WriteUInt32((uint) num1);
      builder.WriteUInt32((uint) this.MetadataSize);
      builder.WriteUInt32((uint) corFlags);
      builder.WriteUInt32((uint) entryPointTokenOrRva);
      builder.WriteUInt32(this.ResourceDataSize == 0 ? 0U : (uint) num2);
      builder.WriteUInt32((uint) this.ResourceDataSize);
      builder.WriteUInt32(this.StrongNameSignatureSize == 0 ? 0U : (uint) num3);
      builder.WriteUInt32((uint) this.StrongNameSignatureSize);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
      builder.WriteUInt32(0U);
    }

    private void WriteRuntimeStartupStub(
      BlobBuilder sectionBuilder,
      int importAddressTableRva,
      ulong baseAddress)
    {
      if (this.Is32Bit)
      {
        sectionBuilder.Align(4);
        sectionBuilder.WriteUInt16((ushort) 0);
        sectionBuilder.WriteByte(byte.MaxValue);
        sectionBuilder.WriteByte((byte) 37);
        sectionBuilder.WriteUInt32((uint) importAddressTableRva + (uint) baseAddress);
      }
      else
      {
        sectionBuilder.Align(8);
        sectionBuilder.WriteUInt32(0U);
        sectionBuilder.WriteUInt16((ushort) 0);
        sectionBuilder.WriteByte(byte.MaxValue);
        sectionBuilder.WriteByte((byte) 37);
        sectionBuilder.WriteUInt64((ulong) importAddressTableRva + baseAddress);
      }
    }
  }
}

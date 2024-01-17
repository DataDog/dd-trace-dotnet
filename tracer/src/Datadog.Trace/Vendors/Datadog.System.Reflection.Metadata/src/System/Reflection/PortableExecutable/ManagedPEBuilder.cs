﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.ManagedPEBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
    public class ManagedPEBuilder : PEBuilder
  {
    public const int ManagedResourcesDataAlignment = 8;
    public const int MappedFieldDataAlignment = 8;
    private const int DefaultStrongNameSignatureSize = 128;

    #nullable disable
    private const string TextSectionName = ".text";
    private const string ResourceSectionName = ".rsrc";
    private const string RelocationSectionName = ".reloc";
    private readonly PEDirectoriesBuilder _peDirectoriesBuilder;
    private readonly MetadataRootBuilder _metadataRootBuilder;
    private readonly BlobBuilder _ilStream;
    private readonly BlobBuilder _mappedFieldDataOpt;
    private readonly BlobBuilder _managedResourcesOpt;
    private readonly ResourceSectionBuilder _nativeResourcesOpt;
    private readonly int _strongNameSignatureSize;
    private readonly MethodDefinitionHandle _entryPointOpt;
    private readonly DebugDirectoryBuilder _debugDirectoryBuilderOpt;
    private readonly CorFlags _corFlags;
    private int _lazyEntryPointAddress;
    private Blob _lazyStrongNameSignature;


    #nullable enable
    public ManagedPEBuilder(
      PEHeaderBuilder header,
      MetadataRootBuilder metadataRootBuilder,
      BlobBuilder ilStream,
      BlobBuilder? mappedFieldData = null,
      BlobBuilder? managedResources = null,
      ResourceSectionBuilder? nativeResources = null,
      DebugDirectoryBuilder? debugDirectoryBuilder = null,
      int strongNameSignatureSize = 128,
      MethodDefinitionHandle entryPoint = default (MethodDefinitionHandle),
      CorFlags flags = CorFlags.ILOnly,
      Func<IEnumerable<Blob>, BlobContentId>? deterministicIdProvider = null)
      : base(header, deterministicIdProvider)
    {
      if (header == null)
        Throw.ArgumentNull(nameof (header));
      if (metadataRootBuilder == null)
        Throw.ArgumentNull(nameof (metadataRootBuilder));
      if (ilStream == null)
        Throw.ArgumentNull(nameof (ilStream));
      if (strongNameSignatureSize < 0)
        Throw.ArgumentOutOfRange(nameof (strongNameSignatureSize));
      this._metadataRootBuilder = metadataRootBuilder;
      this._ilStream = ilStream;
      this._mappedFieldDataOpt = mappedFieldData;
      this._managedResourcesOpt = managedResources;
      this._nativeResourcesOpt = nativeResources;
      this._strongNameSignatureSize = strongNameSignatureSize;
      this._entryPointOpt = entryPoint;
      this._debugDirectoryBuilderOpt = debugDirectoryBuilder ?? this.CreateDefaultDebugDirectoryBuilder();
      this._corFlags = flags;
      this._peDirectoriesBuilder = new PEDirectoriesBuilder();
    }


    #nullable disable
    private DebugDirectoryBuilder CreateDefaultDebugDirectoryBuilder()
    {
      if (!this.IsDeterministic)
        return (DebugDirectoryBuilder) null;
      DebugDirectoryBuilder directoryBuilder = new DebugDirectoryBuilder();
      directoryBuilder.AddReproducibleEntry();
      return directoryBuilder;
    }


    #nullable enable
    protected override ImmutableArray<PEBuilder.Section> CreateSections()
    {
      ImmutableArray<PEBuilder.Section>.Builder builder = ImmutableArray.CreateBuilder<PEBuilder.Section>(3);
      builder.Add(new PEBuilder.Section(".text", SectionCharacteristics.ContainsCode | SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead));
      if (this._nativeResourcesOpt != null)
        builder.Add(new PEBuilder.Section(".rsrc", SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemRead));
      if (this.Header.Machine == Machine.I386 || this.Header.Machine == Machine.Unknown)
        builder.Add(new PEBuilder.Section(".reloc", SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemDiscardable | SectionCharacteristics.MemRead));
      return builder.ToImmutable();
    }

    protected override BlobBuilder SerializeSection(string name, SectionLocation location)
    {
      switch (name)
      {
        case ".text":
          return this.SerializeTextSection(location);
        case ".rsrc":
          return this.SerializeResourceSection(location);
        case ".reloc":
          return this.SerializeRelocationSection(location);
        default:
          throw new ArgumentException(SR.Format(SR.UnknownSectionName, (object) name), nameof (name));
      }
    }


    #nullable disable
    private BlobBuilder SerializeTextSection(SectionLocation location)
    {
      BlobBuilder builder = new BlobBuilder();
      BlobBuilder blobBuilder1 = new BlobBuilder();
      MetadataSizes sizes = this._metadataRootBuilder.Sizes;
      int imageCharacteristics = (int) this.Header.ImageCharacteristics;
      int machine = (int) this.Header.Machine;
      int count1 = this._ilStream.Count;
      int metadataSize = sizes.MetadataSize;
      BlobBuilder managedResourcesOpt = this._managedResourcesOpt;
      int count2 = managedResourcesOpt != null ? managedResourcesOpt.Count : 0;
      int nameSignatureSize = this._strongNameSignatureSize;
      DebugDirectoryBuilder directoryBuilderOpt = this._debugDirectoryBuilderOpt;
      int size = directoryBuilderOpt != null ? directoryBuilderOpt.Size : 0;
      BlobBuilder mappedFieldDataOpt = this._mappedFieldDataOpt;
      int count3 = mappedFieldDataOpt != null ? mappedFieldDataOpt.Count : 0;
      ManagedTextSection managedTextSection = new ManagedTextSection((Characteristics) imageCharacteristics, (Machine) machine, count1, metadataSize, count2, nameSignatureSize, size, count3);
      int methodBodyStreamRva = location.RelativeVirtualAddress + managedTextSection.OffsetToILStream;
      int mappedFieldDataStreamRva = location.RelativeVirtualAddress + managedTextSection.CalculateOffsetToMappedFieldDataStream();
      this._metadataRootBuilder.Serialize(blobBuilder1, methodBodyStreamRva, mappedFieldDataStreamRva);
      BlobBuilder blobBuilder2;
      DirectoryEntry directoryEntry;
      if (this._debugDirectoryBuilderOpt != null)
      {
        int toDebugDirectory = managedTextSection.ComputeOffsetToDebugDirectory();
        blobBuilder2 = new BlobBuilder(this._debugDirectoryBuilderOpt.TableSize);
        this._debugDirectoryBuilderOpt.Serialize(blobBuilder2, location, toDebugDirectory);
        directoryEntry = new DirectoryEntry(location.RelativeVirtualAddress + toDebugDirectory, this._debugDirectoryBuilderOpt.TableSize);
      }
      else
      {
        blobBuilder2 = (BlobBuilder) null;
        directoryEntry = new DirectoryEntry();
      }
      this._lazyEntryPointAddress = managedTextSection.GetEntryPointAddress(location.RelativeVirtualAddress);
      managedTextSection.Serialize(builder, location.RelativeVirtualAddress, this._entryPointOpt.IsNil ? 0 : MetadataTokens.GetToken((EntityHandle) this._entryPointOpt), this._corFlags, this.Header.ImageBase, blobBuilder1, this._ilStream, this._mappedFieldDataOpt, this._managedResourcesOpt, blobBuilder2, out this._lazyStrongNameSignature);
      this._peDirectoriesBuilder.AddressOfEntryPoint = this._lazyEntryPointAddress;
      this._peDirectoriesBuilder.DebugTable = directoryEntry;
      this._peDirectoriesBuilder.ImportAddressTable = managedTextSection.GetImportAddressTableDirectoryEntry(location.RelativeVirtualAddress);
      this._peDirectoriesBuilder.ImportTable = managedTextSection.GetImportTableDirectoryEntry(location.RelativeVirtualAddress);
      this._peDirectoriesBuilder.CorHeaderTable = managedTextSection.GetCorHeaderDirectoryEntry(location.RelativeVirtualAddress);
      return builder;
    }

    private BlobBuilder SerializeResourceSection(SectionLocation location)
    {
      BlobBuilder builder = new BlobBuilder();
      this._nativeResourcesOpt.Serialize(builder, location);
      this._peDirectoriesBuilder.ResourceTable = new DirectoryEntry(location.RelativeVirtualAddress, builder.Count);
      return builder;
    }

    private BlobBuilder SerializeRelocationSection(SectionLocation location)
    {
      BlobBuilder builder = new BlobBuilder();
      ManagedPEBuilder.WriteRelocationSection(builder, this.Header.Machine, this._lazyEntryPointAddress);
      this._peDirectoriesBuilder.BaseRelocationTable = new DirectoryEntry(location.RelativeVirtualAddress, builder.Count);
      return builder;
    }

    private static void WriteRelocationSection(
      BlobBuilder builder,
      Machine machine,
      int entryPointAddress)
    {
      builder.WriteUInt32((uint) (entryPointAddress + 2) / 4096U * 4096U);
      builder.WriteUInt32(machine == Machine.IA64 ? 14U : 12U);
      uint num1 = (uint) (entryPointAddress + 2) % 4096U;
      uint num2 = machine == Machine.Amd64 || machine == Machine.IA64 || machine == Machine.Arm64 ? 10U : 3U;
      ushort num3 = (ushort) (num2 << 12 | num1);
      builder.WriteUInt16(num3);
      if (machine == Machine.IA64)
        builder.WriteUInt32(num2 << 12);
      builder.WriteUInt16((ushort) 0);
    }


    #nullable enable
    protected internal override PEDirectoriesBuilder GetDirectories() => this._peDirectoriesBuilder;

    public void Sign(BlobBuilder peImage, Func<IEnumerable<Blob>, byte[]> signatureProvider)
    {
      if (peImage == null)
        Throw.ArgumentNull(nameof (peImage));
      if (signatureProvider == null)
        Throw.ArgumentNull(nameof (signatureProvider));
      this.Sign(peImage, this._lazyStrongNameSignature, signatureProvider);
    }
  }
}

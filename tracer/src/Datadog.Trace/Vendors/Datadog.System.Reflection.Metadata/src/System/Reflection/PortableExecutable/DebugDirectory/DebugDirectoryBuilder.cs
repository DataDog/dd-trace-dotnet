﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.DebugDirectoryBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
    public sealed class DebugDirectoryBuilder
  {

    #nullable disable
    private readonly List<DebugDirectoryBuilder.Entry> _entries;
    private readonly BlobBuilder _dataBuilder;

    public DebugDirectoryBuilder()
    {
      this._entries = new List<DebugDirectoryBuilder.Entry>(3);
      this._dataBuilder = new BlobBuilder();
    }

    internal void AddEntry(DebugDirectoryEntryType type, uint version, uint stamp, int dataSize) => this._entries.Add(new DebugDirectoryBuilder.Entry()
    {
      Stamp = stamp,
      Version = version,
      Type = type,
      DataSize = dataSize
    });

    /// <summary>Adds an entry.</summary>
    /// <param name="type">Entry type.</param>
    /// <param name="version">Entry version.</param>
    /// <param name="stamp">Entry stamp.</param>
    public void AddEntry(DebugDirectoryEntryType type, uint version, uint stamp) => this.AddEntry(type, version, stamp, 0);


    #nullable enable
    /// <summary>Adds an entry.</summary>
    /// <typeparam name="TData">Type of data passed to <paramref name="dataSerializer" />.</typeparam>
    /// <param name="type">Entry type.</param>
    /// <param name="version">Entry version.</param>
    /// <param name="stamp">Entry stamp.</param>
    /// <param name="data">Data passed to <paramref name="dataSerializer" />.</param>
    /// <param name="dataSerializer">Serializes data to a <see cref="T:System.Reflection.Metadata.BlobBuilder" />.</param>
    public void AddEntry<TData>(
      DebugDirectoryEntryType type,
      uint version,
      uint stamp,
      TData data,
      Action<BlobBuilder, TData> dataSerializer)
    {
      if (dataSerializer == null)
        Throw.ArgumentNull(nameof (dataSerializer));
      int count = this._dataBuilder.Count;
      dataSerializer(this._dataBuilder, data);
      int dataSize = this._dataBuilder.Count - count;
      this.AddEntry(type, version, stamp, dataSize);
    }

    /// <summary>Adds a CodeView entry.</summary>
    /// <param name="pdbPath">Path to the PDB. Shall not be empty.</param>
    /// <param name="pdbContentId">Unique id of the PDB content.</param>
    /// <param name="portablePdbVersion">Version of Portable PDB format (e.g. 0x0100 for 1.0), or 0 if the PDB is not portable.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="pdbPath" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="pdbPath" /> contains NUL character.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="portablePdbVersion" /> is smaller than 0x0100.</exception>
    public void AddCodeViewEntry(
      string pdbPath,
      BlobContentId pdbContentId,
      ushort portablePdbVersion)
    {
      this.AddCodeViewEntry(pdbPath, pdbContentId, portablePdbVersion, 1);
    }

    /// <summary>Adds a CodeView entry.</summary>
    /// <param name="pdbPath">Path to the PDB. Shall not be empty.</param>
    /// <param name="pdbContentId">Unique id of the PDB content.</param>
    /// <param name="portablePdbVersion">Version of Portable PDB format (e.g. 0x0100 for 1.0), or 0 if the PDB is not portable.</param>
    /// <param name="age">Age (iteration) of the PDB. Shall be 1 for Portable PDBs.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="pdbPath" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="pdbPath" /> contains NUL character.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="age" /> is less than 1.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="portablePdbVersion" /> is smaller than 0x0100.</exception>
    public void AddCodeViewEntry(
      string pdbPath,
      BlobContentId pdbContentId,
      ushort portablePdbVersion,
      int age)
    {
      if (pdbPath == null)
        Throw.ArgumentNull(nameof (pdbPath));
      if (age < 1)
        Throw.ArgumentOutOfRange(nameof (age));
      if (pdbPath.Length == 0 || pdbPath.IndexOf(char.MinValue) == 0)
        Throw.InvalidArgument(SR.ExpectedNonEmptyString, nameof (pdbPath));
      if (portablePdbVersion > (ushort) 0 && portablePdbVersion < (ushort) 256)
        Throw.ArgumentOutOfRange(nameof (portablePdbVersion));
      int dataSize = DebugDirectoryBuilder.WriteCodeViewData(this._dataBuilder, pdbPath, pdbContentId.Guid, age);
      this.AddEntry(DebugDirectoryEntryType.CodeView, portablePdbVersion == (ushort) 0 ? 0U : PortablePdbVersions.DebugDirectoryEntryVersion(portablePdbVersion), pdbContentId.Stamp, dataSize);
    }

    /// <summary>Adds Reproducible entry.</summary>
    public void AddReproducibleEntry() => this.AddEntry(DebugDirectoryEntryType.Reproducible, 0U, 0U);


    #nullable disable
    private static int WriteCodeViewData(
      BlobBuilder builder,
      string pdbPath,
      Guid pdbGuid,
      int age)
    {
      int count = builder.Count;
      builder.WriteByte((byte) 82);
      builder.WriteByte((byte) 83);
      builder.WriteByte((byte) 68);
      builder.WriteByte((byte) 83);
      builder.WriteGuid(pdbGuid);
      builder.WriteInt32(age);
      builder.WriteUTF8(pdbPath);
      builder.WriteByte((byte) 0);
      return builder.Count - count;
    }


    #nullable enable
    /// <summary>Adds PDB checksum entry.</summary>
    /// <param name="algorithmName">Hash algorithm name (e.g. "SHA256").</param>
    /// <param name="checksum">Checksum.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="algorithmName" /> or <paramref name="checksum" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="algorithmName" /> or <paramref name="checksum" /> is empty.</exception>
    public void AddPdbChecksumEntry(string algorithmName, ImmutableArray<byte> checksum)
    {
      if (algorithmName == null)
        Throw.ArgumentNull(nameof (algorithmName));
      if (algorithmName.Length == 0)
        Throw.ArgumentEmptyString(nameof (algorithmName));
      if (checksum.IsDefault)
        Throw.ArgumentNull(nameof (checksum));
      if (checksum.Length == 0)
        Throw.ArgumentEmptyArray(nameof (checksum));
      this.AddEntry(DebugDirectoryEntryType.PdbChecksum, 1U, 0U, DebugDirectoryBuilder.WritePdbChecksumData(this._dataBuilder, algorithmName, checksum));
    }


    #nullable disable
    private static int WritePdbChecksumData(
      BlobBuilder builder,
      string algorithmName,
      ImmutableArray<byte> checksum)
    {
      int count = builder.Count;
      builder.WriteUTF8(algorithmName);
      builder.WriteByte((byte) 0);
      builder.WriteBytes(checksum);
      return builder.Count - count;
    }

    internal int TableSize => 28 * this._entries.Count;

    internal int Size
    {
      get
      {
        int tableSize = this.TableSize;
        int? count = this._dataBuilder?.Count;
        return (count.HasValue ? new int?(tableSize + count.GetValueOrDefault()) : new int?()).GetValueOrDefault();
      }
    }


    #nullable enable
    /// <summary>Serialize the Debug Table and Data.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="sectionLocation">The containing PE section location.</param>
    /// <param name="sectionOffset">Offset of the table within the containing section.</param>
    internal void Serialize(
      BlobBuilder builder,
      SectionLocation sectionLocation,
      int sectionOffset)
    {
      int num1 = sectionOffset + this.TableSize;
      foreach (DebugDirectoryBuilder.Entry entry in this._entries)
      {
        int num2;
        int num3;
        if (entry.DataSize > 0)
        {
          num2 = sectionLocation.RelativeVirtualAddress + num1;
          num3 = sectionLocation.PointerToRawData + num1;
        }
        else
        {
          num2 = 0;
          num3 = 0;
        }
        builder.WriteUInt32(0U);
        builder.WriteUInt32(entry.Stamp);
        builder.WriteUInt32(entry.Version);
        builder.WriteInt32((int) entry.Type);
        builder.WriteInt32(entry.DataSize);
        builder.WriteInt32(num2);
        builder.WriteInt32(num3);
        num1 += entry.DataSize;
      }
      builder.LinkSuffix(this._dataBuilder);
    }

    /// <summary>Adds Embedded Portable PDB entry.</summary>
    /// <param name="debugMetadata">Portable PDB metadata builder.</param>
    /// <param name="portablePdbVersion">Version of Portable PDB format (e.g. 0x0100 for 1.0).</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="debugMetadata" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="portablePdbVersion" /> is smaller than 0x0100.</exception>
    public void AddEmbeddedPortablePdbEntry(BlobBuilder debugMetadata, ushort portablePdbVersion)
    {
      if (debugMetadata == null)
        Throw.ArgumentNull(nameof (debugMetadata));
      if (portablePdbVersion < (ushort) 256)
        Throw.ArgumentOutOfRange(nameof (portablePdbVersion));
      int dataSize = DebugDirectoryBuilder.WriteEmbeddedPortablePdbData(this._dataBuilder, debugMetadata);
      this.AddEntry(DebugDirectoryEntryType.EmbeddedPortablePdb, PortablePdbVersions.DebugDirectoryEmbeddedVersion(portablePdbVersion), 0U, dataSize);
    }


    #nullable disable
    private static int WriteEmbeddedPortablePdbData(BlobBuilder builder, BlobBuilder debugMetadata)
    {
      int count = builder.Count;
      builder.WriteUInt32(1111773261U);
      builder.WriteInt32(debugMetadata.Count);
      MemoryStream memoryStream = new MemoryStream();
      using (DeflateStream deflateStream = new DeflateStream((Stream) memoryStream, CompressionLevel.Optimal, true))
      {
        foreach (Blob blob in debugMetadata.GetBlobs())
        {
          ArraySegment<byte> bytes = blob.GetBytes();
          deflateStream.Write(bytes.Array, bytes.Offset, bytes.Count);
        }
      }
      builder.WriteBytes(memoryStream.ToArray());
      return builder.Count - count;
    }

    private struct Entry
    {
      public uint Stamp;
      public uint Version;
      public DebugDirectoryEntryType Type;
      public int DataSize;
    }
  }
}

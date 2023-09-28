// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.PortablePdbBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    /// <summary>Builder of a Portable PDB image.</summary>
    public sealed class PortablePdbBuilder
  {
    private Blob _pdbIdBlob;
    private readonly MethodDefinitionHandle _entryPoint;

    #nullable disable
    private readonly MetadataBuilder _builder;
    private readonly SerializedMetadata _serializedMetadata;


    #nullable enable
    public string MetadataVersion => "PDB v1.0";

    public ushort FormatVersion => 256;

    public Func<IEnumerable<Blob>, BlobContentId> IdProvider { get; }

    /// <summary>Creates a builder of a Portable PDB image.</summary>
    /// <param name="tablesAndHeaps">
    /// Builder populated with debug metadata entities stored in tables and values stored in heaps.
    /// The entities and values will be enumerated when serializing the Portable PDB image.
    /// </param>
    /// <param name="typeSystemRowCounts">
    /// Row counts of all tables that the associated type-system metadata contain.
    /// Each slot in the array corresponds to a table (<see cref="T:System.Reflection.Metadata.Ecma335.TableIndex" />).
    /// The length of the array must be equal to <see cref="F:System.Reflection.Metadata.Ecma335.MetadataTokens.TableCount" />.
    /// </param>
    /// <param name="entryPoint">Entry point method definition handle.</param>
    /// <param name="idProvider">
    /// Function calculating id of content represented as a sequence of blobs.
    /// If not specified a default function that ignores the content and returns current time-based content id is used
    /// (<see cref="M:System.Reflection.Metadata.BlobContentId.GetTimeBasedProvider" />).
    /// You must specify a deterministic function to produce a deterministic Portable PDB image.
    /// </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="tablesAndHeaps" /> or <paramref name="typeSystemRowCounts" /> is null.</exception>
    public PortablePdbBuilder(
      MetadataBuilder tablesAndHeaps,
      ImmutableArray<int> typeSystemRowCounts,
      MethodDefinitionHandle entryPoint,
      Func<IEnumerable<Blob>, BlobContentId>? idProvider = null)
    {
      if (tablesAndHeaps == null)
        Throw.ArgumentNull(nameof (tablesAndHeaps));
      PortablePdbBuilder.ValidateTypeSystemRowCounts(typeSystemRowCounts);
      this._builder = tablesAndHeaps;
      this._entryPoint = entryPoint;
      this._serializedMetadata = tablesAndHeaps.GetSerializedMetadata(typeSystemRowCounts, this.MetadataVersion.Length, true);
      this.IdProvider = idProvider ?? BlobContentId.GetTimeBasedProvider();
    }


    #nullable disable
    private static void ValidateTypeSystemRowCounts(ImmutableArray<int> typeSystemRowCounts)
    {
      if (typeSystemRowCounts.IsDefault)
        Throw.ArgumentNull(nameof (typeSystemRowCounts));
      if (typeSystemRowCounts.Length != MetadataTokens.TableCount)
        throw new ArgumentException(SR.Format(SR.ExpectedArrayOfSize, (object) MetadataTokens.TableCount), nameof (typeSystemRowCounts));
      for (int index = 0; index < typeSystemRowCounts.Length; ++index)
      {
        if (typeSystemRowCounts[index] != 0)
        {
          if ((typeSystemRowCounts[index] & -16777216) != 0)
            throw new ArgumentOutOfRangeException(nameof (typeSystemRowCounts), SR.Format(SR.RowCountOutOfRange, (object) index));
          if ((1L << index & 34949217910615L) == 0L)
            throw new ArgumentException(SR.Format(SR.RowCountMustBeZero, (object) index), nameof (typeSystemRowCounts));
        }
      }
    }

    /// <summary>Serialized #Pdb stream.</summary>
    private void SerializeStandalonePdbStream(BlobBuilder builder)
    {
      int count1 = builder.Count;
      this._pdbIdBlob = builder.ReserveBytes(20);
      builder.WriteInt32(this._entryPoint.IsNil ? 0 : MetadataTokens.GetToken((EntityHandle) this._entryPoint));
      builder.WriteUInt64(this._serializedMetadata.Sizes.ExternalTablesMask);
      MetadataWriterUtilities.SerializeRowCounts(builder, this._serializedMetadata.Sizes.ExternalRowCounts);
      int count2 = builder.Count;
    }


    #nullable enable
    /// <summary>
    /// Serializes Portable PDB content into the given <see cref="T:System.Reflection.Metadata.BlobBuilder" />.
    /// </summary>
    /// <param name="builder">Builder to write to.</param>
    /// <returns>The id of the serialized content.</returns>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="builder" /> is null.</exception>
    public BlobContentId Serialize(BlobBuilder builder)
    {
      if (builder == null)
        Throw.ArgumentNull(nameof (builder));
      MetadataBuilder.SerializeMetadataHeader(builder, this.MetadataVersion, this._serializedMetadata.Sizes);
      this.SerializeStandalonePdbStream(builder);
      this._builder.SerializeMetadataTables(builder, this._serializedMetadata.Sizes, this._serializedMetadata.StringMap, 0, 0);
      this._builder.WriteHeapsTo(builder, this._serializedMetadata.StringHeap);
      BlobContentId blobContentId = this.IdProvider((IEnumerable<Blob>) builder.GetBlobs());
      BlobWriter blobWriter = new BlobWriter(this._pdbIdBlob);
      blobWriter.WriteGuid(blobContentId.Guid);
      blobWriter.WriteUInt32(blobContentId.Stamp);
      return blobContentId;
    }
  }
}

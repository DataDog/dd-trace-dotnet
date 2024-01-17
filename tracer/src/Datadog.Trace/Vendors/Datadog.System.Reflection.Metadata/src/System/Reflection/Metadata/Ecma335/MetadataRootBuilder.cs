﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataRootBuilder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  /// <summary>
  /// Builder of a Metadata Root to be embedded in a Portable Executable image.
  /// </summary>
  /// <remarks>
  /// Metadata root constitutes of a metadata header followed by metadata streams (#~, #Strings, #US, #Guid and #Blob).
  /// </remarks>
  public sealed class MetadataRootBuilder
  {

    #nullable disable
    private const string DefaultMetadataVersionString = "v4.0.30319";

    #nullable enable
    internal static readonly ImmutableArray<int> EmptyRowCounts = ImmutableArray.Create<int>(new int[MetadataTokens.TableCount]);

    #nullable disable
    private readonly MetadataBuilder _tablesAndHeaps;
    private readonly SerializedMetadata _serializedMetadata;


    #nullable enable
    /// <summary>Metadata version string.</summary>
    public string MetadataVersion { get; }

    /// <summary>
    /// True to suppresses basic validation of metadata tables.
    /// The validation verifies that entries in the tables were added in order required by the ECMA specification.
    /// It does not enforce all specification requirements on metadata tables.
    /// </summary>
    public bool SuppressValidation { get; }

    /// <summary>Creates a builder of a metadata root.</summary>
    /// <param name="tablesAndHeaps">
    /// Builder populated with metadata entities stored in tables and values stored in heaps.
    /// The entities and values will be enumerated when serializing the metadata root.
    /// </param>
    /// <param name="metadataVersion">
    /// The version string written to the metadata header. The default value is "v4.0.30319".
    /// </param>
    /// <param name="suppressValidation">
    /// True to suppresses basic validation of metadata tables during serialization.
    /// The validation verifies that entries in the tables were added in order required by the ECMA specification.
    /// It does not enforce all specification requirements on metadata tables.
    /// </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="tablesAndHeaps" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="metadataVersion" /> is too long (the number of bytes when UTF8-encoded must be less than 255).</exception>
    public MetadataRootBuilder(
      MetadataBuilder tablesAndHeaps,
      string? metadataVersion = null,
      bool suppressValidation = false)
    {
      if (tablesAndHeaps == null)
        Throw.ArgumentNull(nameof (tablesAndHeaps));
      int metadataVersionByteCount = metadataVersion != null ? BlobUtilities.GetUTF8ByteCount(metadataVersion) : "v4.0.30319".Length;
      if (metadataVersionByteCount > 254)
        Throw.InvalidArgument(SR.MetadataVersionTooLong, nameof (metadataVersion));
      this._tablesAndHeaps = tablesAndHeaps;
      this.MetadataVersion = metadataVersion ?? "v4.0.30319";
      this.SuppressValidation = suppressValidation;
      this._serializedMetadata = tablesAndHeaps.GetSerializedMetadata(MetadataRootBuilder.EmptyRowCounts, metadataVersionByteCount, false);
    }

    /// <summary>Returns sizes of various metadata structures.</summary>
    public MetadataSizes Sizes => this._serializedMetadata.Sizes;

    /// <summary>
    /// Serializes metadata root content into the given <see cref="T:System.Reflection.Metadata.BlobBuilder" />.
    /// </summary>
    /// <param name="builder">Builder to write to.</param>
    /// <param name="methodBodyStreamRva">
    /// The relative virtual address of the start of the method body stream.
    /// Used to calculate the final value of RVA fields of MethodDef table.
    /// </param>
    /// <param name="mappedFieldDataStreamRva">
    /// The relative virtual address of the start of the field init data stream.
    /// Used to calculate the final value of RVA fields of FieldRVA table.
    /// </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="builder" /> is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="methodBodyStreamRva" /> or <paramref name="mappedFieldDataStreamRva" /> is negative.</exception>
    /// <exception cref="T:System.InvalidOperationException">
    /// A metadata table is not ordered as required by the specification and <see cref="P:System.Reflection.Metadata.Ecma335.MetadataRootBuilder.SuppressValidation" /> is false.
    /// </exception>
    public void Serialize(
      BlobBuilder builder,
      int methodBodyStreamRva,
      int mappedFieldDataStreamRva)
    {
      if (builder == null)
        Throw.ArgumentNull(nameof (builder));
      if (methodBodyStreamRva < 0)
        Throw.ArgumentOutOfRange(nameof (methodBodyStreamRva));
      if (mappedFieldDataStreamRva < 0)
        Throw.ArgumentOutOfRange(nameof (mappedFieldDataStreamRva));
      if (!this.SuppressValidation)
        this._tablesAndHeaps.ValidateOrder();
      MetadataBuilder.SerializeMetadataHeader(builder, this.MetadataVersion, this._serializedMetadata.Sizes);
      this._tablesAndHeaps.SerializeMetadataTables(builder, this._serializedMetadata.Sizes, this._serializedMetadata.StringMap, methodBodyStreamRva, mappedFieldDataStreamRva);
      this._tablesAndHeaps.WriteHeapsTo(builder, this._serializedMetadata.StringHeap);
    }
  }
}

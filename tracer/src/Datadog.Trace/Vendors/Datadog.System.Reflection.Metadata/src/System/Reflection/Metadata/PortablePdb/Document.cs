﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Document
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>Source document in debug metadata.</summary>
  /// <remarks>
  /// See also https://github.com/dotnet/runtime/blob/main/src/libraries/System.Reflection.Metadata/specs/PortablePdb-Metadata.md#document-table-0x30.
  /// </remarks>
  public readonly struct Document
  {

    #nullable disable
    private readonly MetadataReader _reader;
    private readonly int _rowId;


    #nullable enable
    internal Document(MetadataReader reader, DocumentHandle handle)
    {
      this._reader = reader;
      this._rowId = handle.RowId;
    }

    private DocumentHandle Handle => DocumentHandle.FromRowId(this._rowId);

    /// <summary>Returns Document Name Blob.</summary>
    public DocumentNameBlobHandle Name => this._reader.DocumentTable.GetName(this.Handle);

    /// <summary>Source code language (C#, VB, F#, etc.)</summary>
    public GuidHandle Language => this._reader.DocumentTable.GetLanguage(this.Handle);

    /// <summary>
    /// Hash algorithm used to calculate <see cref="P:System.Reflection.Metadata.Document.Hash" /> (SHA1, SHA256, etc.)
    /// </summary>
    public GuidHandle HashAlgorithm => this._reader.DocumentTable.GetHashAlgorithm(this.Handle);

    /// <summary>Document content hash.</summary>
    /// <remarks>
    /// <see cref="P:System.Reflection.Metadata.Document.HashAlgorithm" /> determines the algorithm used to produce this hash.
    /// The source document is hashed in its binary form as stored in the file.
    /// </remarks>
    public BlobHandle Hash => this._reader.DocumentTable.GetHash(this.Handle);
  }
}

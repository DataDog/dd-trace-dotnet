// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.DocumentNameBlobHandle
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Diagnostics.CodeAnalysis;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  /// <summary>
  /// <see cref="T:System.Reflection.Metadata.BlobHandle" /> representing a blob on #Blob heap in Portable PDB
  /// structured as Document Name.
  /// </summary>
  /// <remarks>
  /// The kind of the handle is <see cref="F:System.Reflection.Metadata.HandleKind.Blob" />.
  /// The handle is a specialization of <see cref="T:System.Reflection.Metadata.BlobHandle" /> and doesn't have a distinct kind.
  /// </remarks>
  public readonly struct DocumentNameBlobHandle : IEquatable<DocumentNameBlobHandle>
  {
    private readonly int _heapOffset;

    private DocumentNameBlobHandle(int heapOffset) => this._heapOffset = heapOffset;

    internal static DocumentNameBlobHandle FromOffset(int heapOffset) => new DocumentNameBlobHandle(heapOffset);

    public static implicit operator BlobHandle(DocumentNameBlobHandle handle) => BlobHandle.FromOffset(handle._heapOffset);

    public static explicit operator DocumentNameBlobHandle(BlobHandle handle)
    {
      if (handle.IsVirtual)
        Throw.InvalidCast();
      return DocumentNameBlobHandle.FromOffset(handle.GetHeapOffset());
    }

    public bool IsNil => this._heapOffset == 0;

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is DocumentNameBlobHandle other && this.Equals(other);

    public bool Equals(DocumentNameBlobHandle other) => this._heapOffset == other._heapOffset;

    public override int GetHashCode() => this._heapOffset;

    public static bool operator ==(DocumentNameBlobHandle left, DocumentNameBlobHandle right) => left.Equals(right);

    public static bool operator !=(DocumentNameBlobHandle left, DocumentNameBlobHandle right) => !left.Equals(right);
  }
}

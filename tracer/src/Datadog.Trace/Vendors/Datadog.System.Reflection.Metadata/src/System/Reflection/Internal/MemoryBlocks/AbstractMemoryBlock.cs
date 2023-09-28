// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.AbstractMemoryBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Metadata;

namespace Datadog.System.Reflection.Internal
{
  /// <summary>
  /// Represents a disposable blob of memory accessed via unsafe pointer.
  /// </summary>
  internal abstract class AbstractMemoryBlock : IDisposable
  {
    /// <summary>
    /// Pointer to the underlying data (not valid after disposal).
    /// </summary>
    public abstract unsafe byte* Pointer { get; }

    /// <summary>Size of the block.</summary>
    public abstract int Size { get; }

    public unsafe BlobReader GetReader() => new BlobReader(this.Pointer, this.Size);

    /// <summary>Returns the content of the entire memory block.</summary>
    /// <remarks>
    /// Does not check bounds.
    /// 
    /// Only creates a copy of the data if they are not represented by a managed byte array,
    /// or if the specified range doesn't span the entire block.
    /// </remarks>
    public virtual unsafe ImmutableArray<byte> GetContentUnchecked(int start, int length)
    {
      ImmutableArray<byte> contentUnchecked = BlobUtilities.ReadImmutableBytes(this.Pointer + start, length);
      GC.KeepAlive((object) this);
      return contentUnchecked;
    }

    /// <summary>Disposes the block.</summary>
    /// <remarks>
    /// The operation is idempotent, but must not be called concurrently with any other operations on the block.
    /// 
    /// Using the block after dispose is an error in our code and therefore no effort is made to throw a tidy
    /// ObjectDisposedException and null ref or AV is possible.
    /// </remarks>
    public abstract void Dispose();
  }
}

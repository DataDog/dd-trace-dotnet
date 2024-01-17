﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEMemoryBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;
using Datadog.System.Reflection.Metadata;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
  public readonly struct PEMemoryBlock
  {

    #nullable disable
    private readonly AbstractMemoryBlock _block;
    private readonly int _offset;


    #nullable enable
    internal PEMemoryBlock(AbstractMemoryBlock block, int offset = 0)
    {
      this._block = block;
      this._offset = offset;
    }

    /// <summary>Pointer to the first byte of the block.</summary>
    public unsafe byte* Pointer => this._block == null ? (byte*) null : this._block.Pointer + this._offset;

    /// <summary>Length of the block.</summary>
    public int Length
    {
      get
      {
        int? size = this._block?.Size;
        int offset = this._offset;
        return (size.HasValue ? new int?(size.GetValueOrDefault() - offset) : new int?()).GetValueOrDefault();
      }
    }

    /// <summary>
    /// Creates <see cref="T:System.Reflection.Metadata.BlobReader" /> for a blob spanning the entire block.
    /// </summary>
    public unsafe BlobReader GetReader() => new BlobReader(this.Pointer, this.Length);

    /// <summary>
    /// Creates <see cref="T:System.Reflection.Metadata.BlobReader" /> for a blob spanning a part of the block.
    /// </summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Specified range is not contained within the block.</exception>
    public unsafe BlobReader GetReader(int start, int length)
    {
      BlobUtilities.ValidateRange(this.Length, start, length, nameof (length));
      return new BlobReader(this.Pointer + start, length);
    }

    /// <summary>Reads the content of the entire block into an array.</summary>
    public ImmutableArray<byte> GetContent()
    {
      AbstractMemoryBlock block = this._block;
      return block == null ? ImmutableArray<byte>.Empty : block.GetContentUnchecked(this._offset, this.Length);
    }

    /// <summary>
    /// Reads the content of a part of the block into an array.
    /// </summary>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Specified range is not contained within the block.</exception>
    public ImmutableArray<byte> GetContent(int start, int length)
    {
      BlobUtilities.ValidateRange(this.Length, start, length, nameof (length));
      AbstractMemoryBlock block = this._block;
      return block == null ? ImmutableArray<byte>.Empty : block.GetContentUnchecked(this._offset + start, length);
    }
  }
}

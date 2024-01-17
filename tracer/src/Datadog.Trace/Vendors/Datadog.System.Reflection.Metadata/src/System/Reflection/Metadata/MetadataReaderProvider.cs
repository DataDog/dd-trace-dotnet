﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MetadataReaderProvider
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;
using System.Threading;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    /// <summary>
    /// Provides a <see cref="T:System.Reflection.Metadata.MetadataReader" /> metadata stored in an array of bytes, a memory block, or a stream.
    /// </summary>
    /// <remarks>
    /// Supported formats:
    /// - ECMA-335 CLI (Common Language Infrastructure) metadata (<see cref="M:System.Reflection.Metadata.MetadataReaderProvider.FromMetadataImage(System.Byte*,System.Int32)" />)
    /// - Edit and Continue metadata delta (<see cref="M:System.Reflection.Metadata.MetadataReaderProvider.FromMetadataImage(System.Byte*,System.Int32)" />)
    /// - Portable PDB metadata (<see cref="M:System.Reflection.Metadata.MetadataReaderProvider.FromPortablePdbImage(System.Byte*,System.Int32)" />)
    /// </remarks>
    public sealed class MetadataReaderProvider : IDisposable
  {

    #nullable disable
    private MemoryBlockProvider _blockProviderOpt;
    private AbstractMemoryBlock _lazyMetadataBlock;
    private MetadataReader _lazyMetadataReader;
    private readonly object _metadataReaderGuard = new object();


    #nullable enable
    internal MetadataReaderProvider(AbstractMemoryBlock metadataBlock) => this._lazyMetadataBlock = metadataBlock;


    #nullable disable
    private MetadataReaderProvider(MemoryBlockProvider blockProvider) => this._blockProviderOpt = blockProvider;


    #nullable enable
    /// <summary>
    /// Creates a Portable PDB metadata provider over a blob stored in memory.
    /// </summary>
    /// <param name="start">Pointer to the start of the Portable PDB blob.</param>
    /// <param name="size">The size of the Portable PDB blob.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="start" /> is <see cref="F:System.IntPtr.Zero" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="size" /> is negative.</exception>
    /// <remarks>
    /// The memory is owned by the caller and not released on disposal of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />.
    /// The caller is responsible for keeping the memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />.
    /// The content of the blob is not read during the construction of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// </remarks>
    public static unsafe MetadataReaderProvider FromPortablePdbImage(byte* start, int size) => MetadataReaderProvider.FromMetadataImage(start, size);

    /// <summary>
    /// Creates a metadata provider over an image stored in memory.
    /// </summary>
    /// <param name="start">Pointer to the start of the metadata blob.</param>
    /// <param name="size">The size of the metadata blob.</param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="start" /> is <see cref="F:System.IntPtr.Zero" />.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="size" /> is negative.</exception>
    /// <remarks>
    /// The memory is owned by the caller and not released on disposal of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />.
    /// The caller is responsible for keeping the memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />.
    /// The content of the blob is not read during the construction of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// </remarks>
    public static unsafe MetadataReaderProvider FromMetadataImage(byte* start, int size)
    {
      if ((IntPtr) start == IntPtr.Zero)
        Throw.ArgumentNull(nameof (start));
      return size >= 0 ? new MetadataReaderProvider((MemoryBlockProvider) new ExternalMemoryBlockProvider(start, size)) : throw new ArgumentOutOfRangeException(nameof (size));
    }

    /// <summary>
    /// Creates a Portable PDB metadata provider over a byte array.
    /// </summary>
    /// <param name="image">Portable PDB image.</param>
    /// <remarks>
    /// The content of the image is not read during the construction of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="image" /> is null.</exception>
    public static MetadataReaderProvider FromPortablePdbImage(ImmutableArray<byte> image) => MetadataReaderProvider.FromMetadataImage(image);

    /// <summary>Creates a provider over a byte array.</summary>
    /// <param name="image">Metadata image.</param>
    /// <remarks>
    /// The content of the image is not read during the construction of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="image" /> is null.</exception>
    public static MetadataReaderProvider FromMetadataImage(ImmutableArray<byte> image)
    {
      if (image.IsDefault)
        Throw.ArgumentNull(nameof (image));
      return new MetadataReaderProvider((MemoryBlockProvider) new ByteArrayMemoryProvider(image));
    }

    /// <summary>
    /// Creates a provider for a stream of the specified size beginning at its current position.
    /// </summary>
    /// <param name="stream">Stream.</param>
    /// <param name="size">Size of the metadata blob in the stream. If not specified the metadata blob is assumed to span to the end of the stream.</param>
    /// <param name="options">
    /// Options specifying how sections of the image are read from the stream.
    /// 
    /// Unless <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.LeaveOpen" /> is specified, ownership of the stream is transferred to the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// upon successful argument validation. It will be disposed by the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" /> and the caller must not manipulate it.
    /// 
    /// Unless <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.PrefetchMetadata" /> is specified no data
    /// is read from the stream during the construction of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />. Furthermore, the stream must not be manipulated
    /// by caller while the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" /> is alive and undisposed.
    /// 
    /// If <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.PrefetchMetadata" />, the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// will have read all of the data requested during construction. As such, if <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.LeaveOpen" /> is also
    /// specified, the caller retains full ownership of the stream and is assured that it will not be manipulated by the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// after construction.
    /// </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="stream" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="stream" /> doesn't support read and seek operations.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Size is negative or extends past the end of the stream.</exception>
    public static MetadataReaderProvider FromPortablePdbStream(
      Stream stream,
      MetadataStreamOptions options = MetadataStreamOptions.Default,
      int size = 0)
    {
      return MetadataReaderProvider.FromMetadataStream(stream, options, size);
    }

    /// <summary>
    /// Creates a provider for a stream of the specified size beginning at its current position.
    /// </summary>
    /// <param name="stream">Stream.</param>
    /// <param name="size">Size of the metadata blob in the stream. If not specified the metadata blob is assumed to span to the end of the stream.</param>
    /// <param name="options">
    /// Options specifying how sections of the image are read from the stream.
    /// 
    /// Unless <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.LeaveOpen" /> is specified, ownership of the stream is transferred to the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// upon successful argument validation. It will be disposed by the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" /> and the caller must not manipulate it.
    /// 
    /// Unless <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.PrefetchMetadata" /> is specified no data
    /// is read from the stream during the construction of the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />. Furthermore, the stream must not be manipulated
    /// by caller while the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" /> is alive and undisposed.
    /// 
    /// If <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.PrefetchMetadata" />, the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// will have read all of the data requested during construction. As such, if <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.LeaveOpen" /> is also
    /// specified, the caller retains full ownership of the stream and is assured that it will not be manipulated by the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// after construction.
    /// </param>
    /// <exception cref="T:System.ArgumentNullException"><paramref name="stream" /> is null.</exception>
    /// <exception cref="T:System.ArgumentException"><paramref name="stream" /> doesn't support read and seek operations.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">Size is negative or extends past the end of the stream.</exception>
    /// <exception cref="T:System.IO.IOException">Error reading from the stream (only when <see cref="F:System.Reflection.Metadata.MetadataStreamOptions.PrefetchMetadata" /> is specified).</exception>
    public static MetadataReaderProvider FromMetadataStream(
      Stream stream,
      MetadataStreamOptions options = MetadataStreamOptions.Default,
      int size = 0)
    {
      if (stream == null)
        Throw.ArgumentNull(nameof (stream));
      if (!stream.CanRead || !stream.CanSeek)
        throw new ArgumentException(SR.StreamMustSupportReadAndSeek, nameof (stream));
      if (!options.IsValid())
        throw new ArgumentOutOfRangeException(nameof (options));
      long position = stream.Position;
      int andValidateSize = StreamExtensions.GetAndValidateSize(stream, size, nameof (stream));
      bool flag = true;
      MetadataReaderProvider metadataReaderProvider;
      try
      {
        if ((options & MetadataStreamOptions.PrefetchMetadata) == MetadataStreamOptions.Default)
        {
          metadataReaderProvider = new MetadataReaderProvider((MemoryBlockProvider) new StreamMemoryBlockProvider(stream, position, andValidateSize, (options & MetadataStreamOptions.LeaveOpen) != 0));
          flag = false;
        }
        else
          metadataReaderProvider = new MetadataReaderProvider((AbstractMemoryBlock) StreamMemoryBlockProvider.ReadMemoryBlockNoLock(stream, position, andValidateSize));
      }
      finally
      {
        if (flag && (options & MetadataStreamOptions.LeaveOpen) == MetadataStreamOptions.Default)
          stream.Dispose();
      }
      return metadataReaderProvider;
    }

    /// <summary>Disposes all memory allocated by the reader.</summary>
    /// <remarks>
    /// <see cref="M:System.Reflection.Metadata.MetadataReaderProvider.Dispose" />  can be called multiple times (but not in parallel).
    /// It is not safe to call <see cref="M:System.Reflection.Metadata.MetadataReaderProvider.Dispose" /> in parallel with any other operation on the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />
    /// or reading from the underlying memory.
    /// </remarks>
    public void Dispose()
    {
      this._blockProviderOpt?.Dispose();
      this._blockProviderOpt = (MemoryBlockProvider) null;
      this._lazyMetadataBlock?.Dispose();
      this._lazyMetadataBlock = (AbstractMemoryBlock) null;
      this._lazyMetadataReader = (MetadataReader) null;
    }

    /// <summary>
    /// Gets a <see cref="T:System.Reflection.Metadata.MetadataReader" /> from a <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" />.
    /// </summary>
    /// <remarks>
    /// The caller must keep the <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" /> undisposed throughout the lifetime of the metadata reader.
    /// 
    /// If this method is called multiple times each call with arguments equal to the arguments passed to the previous successful call
    /// returns the same instance of <see cref="T:System.Reflection.Metadata.MetadataReader" /> as the previous call.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">The encoding of <paramref name="utf8Decoder" /> is not <see cref="T:System.Text.UTF8Encoding" />.</exception>
    /// <exception cref="T:System.PlatformNotSupportedException">The current platform is big-endian.</exception>
    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    /// <exception cref="T:System.ObjectDisposedException">Provider has been disposed.</exception>
    public unsafe MetadataReader GetMetadataReader(
      MetadataReaderOptions options = MetadataReaderOptions.Default,
      MetadataStringDecoder? utf8Decoder = null)
    {
      MetadataReader lazyMetadataReader1 = this._lazyMetadataReader;
      if (MetadataReaderProvider.CanReuseReader(lazyMetadataReader1, options, utf8Decoder))
        return lazyMetadataReader1;
      lock (this._metadataReaderGuard)
      {
        MetadataReader lazyMetadataReader2 = this._lazyMetadataReader;
        if (MetadataReaderProvider.CanReuseReader(lazyMetadataReader2, options, utf8Decoder))
          return lazyMetadataReader2;
        AbstractMemoryBlock metadataBlock = this.GetMetadataBlock();
        MetadataReader metadataReader = new MetadataReader(metadataBlock.Pointer, metadataBlock.Size, options, utf8Decoder, (object) this);
        this._lazyMetadataReader = metadataReader;
        return metadataReader;
      }
    }


    #nullable disable
    private static bool CanReuseReader(
      MetadataReader reader,
      MetadataReaderOptions options,
      MetadataStringDecoder utf8DecoderOpt)
    {
      return reader != null && reader.Options == options && reader.UTF8Decoder == (utf8DecoderOpt ?? MetadataStringDecoder.DefaultUTF8);
    }


    #nullable enable
    /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
    /// <exception cref="T:System.ObjectDisposedException">Provider has been disposed.</exception>
    internal AbstractMemoryBlock GetMetadataBlock()
    {
      if (this._lazyMetadataBlock == null)
      {
        AbstractMemoryBlock abstractMemoryBlock = this._blockProviderOpt != null ? this._blockProviderOpt.GetMemoryBlock(0, this._blockProviderOpt.Size) : throw new ObjectDisposedException(nameof (MetadataReaderProvider));
        if (Interlocked.CompareExchange<AbstractMemoryBlock>(ref this._lazyMetadataBlock, abstractMemoryBlock, (AbstractMemoryBlock) null) != null)
          abstractMemoryBlock.Dispose();
      }
      return this._lazyMetadataBlock;
    }
  }
}

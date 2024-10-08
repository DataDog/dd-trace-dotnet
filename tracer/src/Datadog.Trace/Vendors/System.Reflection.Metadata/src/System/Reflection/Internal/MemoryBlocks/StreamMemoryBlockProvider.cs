//------------------------------------------------------------------------------
// <auto-generated />
// This file was automatically generated by the UpdateVendors tool.
//------------------------------------------------------------------------------
#pragma warning disable CS0618, CS0649, CS1574, CS1580, CS1581, CS1584, CS1591, CS1573, CS8018, SYSLIB0011, SYSLIB0032
#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618, CS8620, CS8714, CS8762, CS8765, CS8766, CS8767, CS8768, CS8769, CS8612, CS8629, CS8774
// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.StreamMemoryBlockProvider
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
#pragma warning disable CS8625


#nullable enable
namespace Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Internal
{
    /// <summary>Represents data read from a stream.</summary>
    /// <remarks>
    /// Uses memory map to load data from streams backed by files that are bigger than <see cref="F:System.Reflection.Internal.StreamMemoryBlockProvider.MemoryMapThreshold" />.
    /// </remarks>
    internal sealed class StreamMemoryBlockProvider : MemoryBlockProvider
  {
    internal const int MemoryMapThreshold = 16384;

    #nullable disable
    private Stream _stream;
    private readonly object _streamGuard;
    private readonly bool _leaveOpen;
    private bool _useMemoryMap;
    private readonly long _imageStart;
    private readonly int _imageSize;
    private MemoryMappedFile _lazyMemoryMap;


    #nullable enable
    public StreamMemoryBlockProvider(
      Stream stream,
      long imageStart,
      int imageSize,
      bool leaveOpen)
    {
      this._stream = stream;
      this._streamGuard = new object();
      this._imageStart = imageStart;
      this._imageSize = imageSize;
      this._leaveOpen = leaveOpen;
      this._useMemoryMap = stream is FileStream;
    }

    protected override void Dispose(bool disposing)
    {
      if (!this._leaveOpen)
        Interlocked.Exchange<Stream>(ref this._stream, (Stream) null)?.Dispose();
      Interlocked.Exchange<MemoryMappedFile>(ref this._lazyMemoryMap, (MemoryMappedFile) null)?.Dispose();
    }

    public override int Size => this._imageSize;

    /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
    internal static unsafe NativeHeapMemoryBlock ReadMemoryBlockNoLock(
      Stream stream,
      long start,
      int size)
    {
      NativeHeapMemoryBlock nativeHeapMemoryBlock = new NativeHeapMemoryBlock(size);
      bool flag = true;
      try
      {
        stream.Seek(start, SeekOrigin.Begin);
        int num;
        if ((num = stream.Read(nativeHeapMemoryBlock.Pointer, size)) != size)
          stream.CopyTo(nativeHeapMemoryBlock.Pointer + num, size - num);
        flag = false;
      }
      finally
      {
        if (flag)
          nativeHeapMemoryBlock.Dispose();
      }
      return nativeHeapMemoryBlock;
    }

    /// <exception cref="T:System.IO.IOException">Error while reading from the stream.</exception>
    protected override AbstractMemoryBlock GetMemoryBlockImpl(int start, int size)
    {
      long start1 = this._imageStart + (long) start;
      if (this._useMemoryMap && size > 16384)
      {
        MemoryMappedFileBlock block;
        if (this.TryCreateMemoryMappedFileBlock(start1, size, out block))
          return (AbstractMemoryBlock) block;
        this._useMemoryMap = false;
      }
      lock (this._streamGuard)
        return (AbstractMemoryBlock) StreamMemoryBlockProvider.ReadMemoryBlockNoLock(this._stream, start1, size);
    }

    public override Stream GetStream(out StreamConstraints constraints)
    {
      constraints = new StreamConstraints(this._streamGuard, this._imageStart, this._imageSize);
      return this._stream;
    }


    #nullable disable
    /// <exception cref="T:System.IO.IOException">IO error while mapping memory or not enough memory to create the mapping.</exception>
    private bool TryCreateMemoryMappedFileBlock(
      long start,
      int size,
      [NotNullWhen(true)] out MemoryMappedFileBlock block)
    {
      if (this._lazyMemoryMap == null)
      {
        MemoryMappedFile fromFile;
        lock (this._streamGuard)
        {
          try
          {
            fromFile = MemoryMappedFile.CreateFromFile((FileStream) this._stream, (string) null, 0L, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
          }
          catch (UnauthorizedAccessException ex)
          {
            throw new IOException(ex.Message, (Exception) ex);
          }
        }
        if (fromFile == null)
        {
          block = (MemoryMappedFileBlock) null;
          return false;
        }
        if (Interlocked.CompareExchange<MemoryMappedFile>(ref this._lazyMemoryMap, fromFile, (MemoryMappedFile) null) != null)
          fromFile.Dispose();
      }
      MemoryMappedViewAccessor viewAccessor;
      lock (this._streamGuard)
        viewAccessor = this._lazyMemoryMap.CreateViewAccessor(start, (long) size, MemoryMappedFileAccess.Read);
      if (viewAccessor == null)
      {
        block = (MemoryMappedFileBlock) null;
        return false;
      }
      block = new MemoryMappedFileBlock((IDisposable) viewAccessor, (SafeBuffer) viewAccessor.SafeMemoryMappedViewHandle, viewAccessor.PointerOffset, size);
      return true;
    }
  }
}

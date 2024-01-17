﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.PortableExecutable.PEReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;
using Datadog.System.Reflection.Metadata;


#nullable enable
namespace Datadog.System.Reflection.PortableExecutable
{
    /// <summary>Portable Executable format reader.</summary>
    /// <remarks>
    /// The implementation is thread-safe, that is multiple threads can read data from the reader in parallel.
    /// Disposal of the reader is not thread-safe (see <see cref="M:System.Reflection.PortableExecutable.PEReader.Dispose" />).
    /// </remarks>
    /// <summary>Portable Executable format reader.</summary>
    /// <remarks>
    /// The implementation is thread-safe, that is multiple threads can read data from the reader in parallel.
    /// Disposal of the reader is not thread-safe (see <see cref="M:System.Reflection.PortableExecutable.PEReader.Dispose" />).
    /// </remarks>
    public sealed class PEReader : IDisposable
    {

#nullable disable
        private MemoryBlockProvider _peImage;
        private PEHeaders _lazyPEHeaders;
        private AbstractMemoryBlock _lazyMetadataBlock;
        private AbstractMemoryBlock _lazyImageBlock;
        private AbstractMemoryBlock[] _lazyPESectionBlocks;

        /// <summary>
        /// True if the PE image has been loaded into memory by the OS loader.
        /// </summary>
        public bool IsLoadedImage { get; }


#nullable enable
        /// <summary>
        /// Creates a Portable Executable reader over a PE image stored in memory.
        /// </summary>
        /// <param name="peImage">Pointer to the start of the PE image.</param>
        /// <param name="size">The size of the PE image.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="peImage" /> is <see cref="F:System.IntPtr.Zero" />.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="size" /> is negative.</exception>
        /// <remarks>
        /// The memory is owned by the caller and not released on disposal of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
        /// The caller is responsible for keeping the memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
        /// The content of the image is not read during the construction of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// </remarks>
        public unsafe PEReader(byte* peImage, int size)
          : this(peImage, size, false)
        {
        }

        /// <summary>
        /// Creates a Portable Executable reader over a PE image stored in memory.
        /// </summary>
        /// <param name="peImage">Pointer to the start of the PE image.</param>
        /// <param name="size">The size of the PE image.</param>
        /// <param name="isLoadedImage">True if the PE image has been loaded into memory by the OS loader.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="peImage" /> is <see cref="F:System.IntPtr.Zero" />.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="size" /> is negative.</exception>
        /// <remarks>
        /// The memory is owned by the caller and not released on disposal of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
        /// The caller is responsible for keeping the memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />.
        /// The content of the image is not read during the construction of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// </remarks>
        public unsafe PEReader(byte* peImage, int size, bool isLoadedImage)
        {
            if ((IntPtr)peImage == IntPtr.Zero)
                Throw.ArgumentNull(nameof(peImage));
            this._peImage = size >= 0 ? (MemoryBlockProvider)new ExternalMemoryBlockProvider(peImage, size) : throw new ArgumentOutOfRangeException(nameof(size));
            this.IsLoadedImage = isLoadedImage;
        }

        /// <summary>
        /// Creates a Portable Executable reader over a PE image stored in a stream.
        /// </summary>
        /// <param name="peStream">PE image stream.</param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="peStream" /> is null.</exception>
        /// <remarks>
        /// Ownership of the stream is transferred to the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> upon successful validation of constructor arguments. It will be
        /// disposed by the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> and the caller must not manipulate it.
        /// </remarks>
        public PEReader(Stream peStream)
          : this(peStream, PEStreamOptions.Default)
        {
        }

        /// <summary>
        /// Creates a Portable Executable reader over a PE image stored in a stream beginning at its current position and ending at the end of the stream.
        /// </summary>
        /// <param name="peStream">PE image stream.</param>
        /// <param name="options">
        /// Options specifying how sections of the PE image are read from the stream.
        /// 
        /// Unless <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen" /> is specified, ownership of the stream is transferred to the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// upon successful argument validation. It will be disposed by the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> and the caller must not manipulate it.
        /// 
        /// Unless <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata" /> or <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchEntireImage" /> is specified no data
        /// is read from the stream during the construction of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />. Furthermore, the stream must not be manipulated
        /// by caller while the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> is alive and undisposed.
        /// 
        /// If <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata" /> or <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchEntireImage" />, the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// will have read all of the data requested during construction. As such, if <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen" /> is also
        /// specified, the caller retains full ownership of the stream and is assured that it will not be manipulated by the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// after construction.
        /// </param>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="peStream" /> is null.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="options" /> has an invalid value.</exception>
        /// <exception cref="T:System.IO.IOException">Error reading from the stream (only when prefetching data).</exception>
        /// <exception cref="T:System.BadImageFormatException"><see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata" /> is specified and the PE headers of the image are invalid.</exception>
        public PEReader(Stream peStream, PEStreamOptions options)
          : this(peStream, options, 0)
        {
        }

        /// <summary>
        /// Creates a Portable Executable reader over a PE image of the given size beginning at the stream's current position.
        /// </summary>
        /// <param name="peStream">PE image stream.</param>
        /// <param name="size">PE image size.</param>
        /// <param name="options">
        /// Options specifying how sections of the PE image are read from the stream.
        /// 
        /// Unless <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen" /> is specified, ownership of the stream is transferred to the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// upon successful argument validation. It will be disposed by the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> and the caller must not manipulate it.
        /// 
        /// Unless <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata" /> or <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchEntireImage" /> is specified no data
        /// is read from the stream during the construction of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />. Furthermore, the stream must not be manipulated
        /// by caller while the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> is alive and undisposed.
        /// 
        /// If <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata" /> or <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchEntireImage" />, the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// will have read all of the data requested during construction. As such, if <see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.LeaveOpen" /> is also
        /// specified, the caller retains full ownership of the stream and is assured that it will not be manipulated by the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// after construction.
        /// </param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">Size is negative or extends past the end of the stream.</exception>
        /// <exception cref="T:System.IO.IOException">Error reading from the stream (only when prefetching data).</exception>
        /// <exception cref="T:System.BadImageFormatException"><see cref="F:System.Reflection.PortableExecutable.PEStreamOptions.PrefetchMetadata" /> is specified and the PE headers of the image are invalid.</exception>
        public unsafe PEReader(Stream peStream, PEStreamOptions options, int size)
        {
            if (peStream == null)
                Throw.ArgumentNull(nameof(peStream));
            if (!peStream.CanRead || !peStream.CanSeek)
        throw new ArgumentException(SR.StreamMustSupportReadAndSeek, nameof (peStream));
            if (!options.IsValid())
                throw new ArgumentOutOfRangeException(nameof(options));
            this.IsLoadedImage = (options & PEStreamOptions.IsLoadedImage) != 0;
            long position = peStream.Position;
            int andValidateSize = StreamExtensions.GetAndValidateSize(peStream, size, nameof(peStream));
            bool flag = true;
            try
            {
                if ((options & (PEStreamOptions.PrefetchMetadata | PEStreamOptions.PrefetchEntireImage)) == PEStreamOptions.Default)
                {
                    this._peImage = (MemoryBlockProvider)new StreamMemoryBlockProvider(peStream, position, andValidateSize, (options & PEStreamOptions.LeaveOpen) != 0);
                    flag = false;
                }
                else if ((options & PEStreamOptions.PrefetchEntireImage) != PEStreamOptions.Default)
                {
                    NativeHeapMemoryBlock nativeHeapMemoryBlock = StreamMemoryBlockProvider.ReadMemoryBlockNoLock(peStream, position, andValidateSize);
                    this._lazyImageBlock = (AbstractMemoryBlock)nativeHeapMemoryBlock;
                    this._peImage = (MemoryBlockProvider)new ExternalMemoryBlockProvider(nativeHeapMemoryBlock.Pointer, nativeHeapMemoryBlock.Size);
                    if ((options & PEStreamOptions.PrefetchMetadata) == PEStreamOptions.Default)
                        return;
                    this.InitializePEHeaders();
                }
                else
                {
                    this._lazyPEHeaders = new PEHeaders(peStream);
                    this._lazyMetadataBlock = (AbstractMemoryBlock)StreamMemoryBlockProvider.ReadMemoryBlockNoLock(peStream, (long)this._lazyPEHeaders.MetadataStartOffset, this._lazyPEHeaders.MetadataSize);
                }
            }
            finally
            {
                if (flag && (options & PEStreamOptions.LeaveOpen) == PEStreamOptions.Default)
                    peStream.Dispose();
            }
        }

        /// <summary>
        /// Creates a Portable Executable reader over a PE image stored in a byte array.
        /// </summary>
        /// <param name="peImage">PE image.</param>
        /// <remarks>
        /// The content of the image is not read during the construction of the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="peImage" /> is null.</exception>
        public PEReader(ImmutableArray<byte> peImage)
        {
            if (peImage.IsDefault)
                Throw.ArgumentNull(nameof(peImage));
            this._peImage = (MemoryBlockProvider)new ByteArrayMemoryProvider(peImage);
        }

        /// <summary>Disposes all memory allocated by the reader.</summary>
        /// <remarks>
        /// <see cref="M:System.Reflection.PortableExecutable.PEReader.Dispose" />  can be called multiple times (but not in parallel).
        /// It is not safe to call <see cref="M:System.Reflection.PortableExecutable.PEReader.Dispose" /> in parallel with any other operation on the <see cref="T:System.Reflection.PortableExecutable.PEReader" />
        /// or reading from <see cref="T:System.Reflection.PortableExecutable.PEMemoryBlock" />s retrieved from the reader.
        /// </remarks>
        public void Dispose()
        {
            this._lazyPEHeaders = (PEHeaders)null;
            this._peImage?.Dispose();
            this._peImage = (MemoryBlockProvider)null;
            this._lazyImageBlock?.Dispose();
            this._lazyImageBlock = (AbstractMemoryBlock)null;
            this._lazyMetadataBlock?.Dispose();
            this._lazyMetadataBlock = (AbstractMemoryBlock)null;
            AbstractMemoryBlock[] lazyPeSectionBlocks = this._lazyPESectionBlocks;
            if (lazyPeSectionBlocks == null)
                return;
            foreach (AbstractMemoryBlock abstractMemoryBlock in lazyPeSectionBlocks)
                abstractMemoryBlock?.Dispose();
            this._lazyPESectionBlocks = (AbstractMemoryBlock[])null;
        }


#nullable disable
        private MemoryBlockProvider GetPEImage()
        {
            MemoryBlockProvider peImage = this._peImage;
            if (peImage == null)
            {
                if (this._lazyPEHeaders == null)
                    Throw.PEReaderDisposed();
                Throw.InvalidOperation_PEImageNotAvailable();
            }
            return peImage;
        }


#nullable enable
        /// <summary>Gets the PE headers.</summary>
        /// <exception cref="T:System.BadImageFormatException">The headers contain invalid data.</exception>
        /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
        public PEHeaders PEHeaders
        {
            get
            {
                if (this._lazyPEHeaders == null)
                    this.InitializePEHeaders();
                return this._lazyPEHeaders;
            }
        }

        /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
        private void InitializePEHeaders()
        {
            StreamConstraints constraints;
            Stream stream = this.GetPEImage().GetStream(out constraints);
            PEHeaders peHeaders;
            if (constraints.GuardOpt != null)
            {
                lock (constraints.GuardOpt)
                    peHeaders = PEReader.ReadPEHeadersNoLock(stream, constraints.ImageStart, constraints.ImageSize, this.IsLoadedImage);
            }
            else
                peHeaders = PEReader.ReadPEHeadersNoLock(stream, constraints.ImageStart, constraints.ImageSize, this.IsLoadedImage);
            Interlocked.CompareExchange<PEHeaders>(ref this._lazyPEHeaders, peHeaders, (PEHeaders)null);
        }


#nullable disable
        /// <exception cref="T:System.IO.IOException">Error reading from the stream.</exception>
        private static PEHeaders ReadPEHeadersNoLock(
          Stream stream,
          long imageStartPosition,
          int imageSize,
          bool isLoadedImage)
        {
            stream.Seek(imageStartPosition, SeekOrigin.Begin);
            return new PEHeaders(stream, imageSize, isLoadedImage);
        }

        /// <summary>
        /// Returns a view of the entire image as a pointer and length.
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        private AbstractMemoryBlock GetEntireImageBlock()
        {
            if (this._lazyImageBlock == null)
            {
                AbstractMemoryBlock memoryBlock = this.GetPEImage().GetMemoryBlock();
                if (Interlocked.CompareExchange<AbstractMemoryBlock>(ref this._lazyImageBlock, memoryBlock, (AbstractMemoryBlock)null) != null)
                    memoryBlock.Dispose();
            }
            return this._lazyImageBlock;
        }

        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image doesn't have metadata.</exception>
        private AbstractMemoryBlock GetMetadataBlock()
        {
            if (!this.HasMetadata)
                throw new InvalidOperationException(SR.PEImageDoesNotHaveMetadata);
            if (this._lazyMetadataBlock == null)
            {
                AbstractMemoryBlock memoryBlock = this.GetPEImage().GetMemoryBlock(this.PEHeaders.MetadataStartOffset, this.PEHeaders.MetadataSize);
                if (Interlocked.CompareExchange<AbstractMemoryBlock>(ref this._lazyMetadataBlock, memoryBlock, (AbstractMemoryBlock)null) != null)
                    memoryBlock.Dispose();
            }
            return this._lazyMetadataBlock;
        }

        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        private AbstractMemoryBlock GetPESectionBlock(int index)
        {
            MemoryBlockProvider peImage = this.GetPEImage();
            ImmutableArray<SectionHeader> sectionHeaders;
            if (this._lazyPESectionBlocks == null)
            {
                ref AbstractMemoryBlock[] local = ref this._lazyPESectionBlocks;
                sectionHeaders = this.PEHeaders.SectionHeaders;
                AbstractMemoryBlock[] abstractMemoryBlockArray = new AbstractMemoryBlock[sectionHeaders.Length];
                Interlocked.CompareExchange<AbstractMemoryBlock[]>(ref local, abstractMemoryBlockArray, (AbstractMemoryBlock[])null);
            }
            AbstractMemoryBlock memoryBlock;
            if (this.IsLoadedImage)
            {
                MemoryBlockProvider memoryBlockProvider = peImage;
                sectionHeaders = this.PEHeaders.SectionHeaders;
                int virtualAddress = sectionHeaders[index].VirtualAddress;
                sectionHeaders = this.PEHeaders.SectionHeaders;
                int virtualSize = sectionHeaders[index].VirtualSize;
                memoryBlock = memoryBlockProvider.GetMemoryBlock(virtualAddress, virtualSize);
            }
            else
            {
                sectionHeaders = this.PEHeaders.SectionHeaders;
                int virtualSize = sectionHeaders[index].VirtualSize;
                sectionHeaders = this.PEHeaders.SectionHeaders;
                int sizeOfRawData = sectionHeaders[index].SizeOfRawData;
                int num = Math.Min(virtualSize, sizeOfRawData);
                MemoryBlockProvider memoryBlockProvider = peImage;
                sectionHeaders = this.PEHeaders.SectionHeaders;
                int pointerToRawData = sectionHeaders[index].PointerToRawData;
                int size = num;
                memoryBlock = memoryBlockProvider.GetMemoryBlock(pointerToRawData, size);
            }
            if (Interlocked.CompareExchange<AbstractMemoryBlock>(ref this._lazyPESectionBlocks[index], memoryBlock, (AbstractMemoryBlock)null) != null)
                memoryBlock.Dispose();
            return this._lazyPESectionBlocks[index];
        }

        /// <summary>
        /// Return true if the reader can access the entire PE image.
        /// </summary>
        /// <remarks>
        /// Returns false if the <see cref="T:System.Reflection.PortableExecutable.PEReader" /> is constructed from a stream and only part of it is prefetched into memory.
        /// </remarks>
        public bool IsEntireImageAvailable => this._lazyImageBlock != null || this._peImage != null;

        /// <summary>
        /// Gets a pointer to and size of the PE image if available (<see cref="P:System.Reflection.PortableExecutable.PEReader.IsEntireImageAvailable" />).
        /// </summary>
        /// <exception cref="T:System.InvalidOperationException">The entire PE image is not available.</exception>
        public PEMemoryBlock GetEntireImage() => new PEMemoryBlock(this.GetEntireImageBlock());

        /// <summary>Returns true if the PE image contains CLI metadata.</summary>
        /// <exception cref="T:System.BadImageFormatException">The PE headers contain invalid data.</exception>
        /// <exception cref="T:System.IO.IOException">Error reading from the underlying stream.</exception>
        public bool HasMetadata => this.PEHeaders.MetadataSize > 0;

        /// <summary>Loads PE section that contains CLI metadata.</summary>
        /// <exception cref="T:System.InvalidOperationException">The PE image doesn't contain metadata (<see cref="P:System.Reflection.PortableExecutable.PEReader.HasMetadata" /> returns false).</exception>
        /// <exception cref="T:System.BadImageFormatException">The PE headers contain invalid data.</exception>
        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        public PEMemoryBlock GetMetadata() => new PEMemoryBlock(this.GetMetadataBlock());

        /// <summary>
        /// Loads PE section that contains the specified <paramref name="relativeVirtualAddress" /> into memory
        /// and returns a memory block that starts at <paramref name="relativeVirtualAddress" /> and ends at the end of the containing section.
        /// </summary>
        /// <param name="relativeVirtualAddress">Relative Virtual Address of the data to read.</param>
        /// <returns>
        /// An empty block if <paramref name="relativeVirtualAddress" /> doesn't represent a location in any of the PE sections of this PE image.
        /// </returns>
        /// <exception cref="T:System.BadImageFormatException">The PE headers contain invalid data.</exception>
        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="relativeVirtualAddress" /> is negative.</exception>
        public PEMemoryBlock GetSectionData(int relativeVirtualAddress)
        {
            if (relativeVirtualAddress < 0)
                Throw.ArgumentOutOfRange(nameof(relativeVirtualAddress));
            int containingSectionIndex = this.PEHeaders.GetContainingSectionIndex(relativeVirtualAddress);
            if (containingSectionIndex < 0)
                return new PEMemoryBlock();
            AbstractMemoryBlock peSectionBlock = this.GetPESectionBlock(containingSectionIndex);
            int offset = relativeVirtualAddress - this.PEHeaders.SectionHeaders[containingSectionIndex].VirtualAddress;
            return offset > peSectionBlock.Size ? new PEMemoryBlock() : new PEMemoryBlock(peSectionBlock, offset);
        }


#nullable enable
        /// <summary>
        /// Loads PE section of the specified name into memory and returns a memory block that spans the section.
        /// </summary>
        /// <param name="sectionName">Name of the section.</param>
        /// <returns>
        /// An empty block if no section of the given <paramref name="sectionName" /> exists in this PE image.
        /// </returns>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="sectionName" /> is null.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        public PEMemoryBlock GetSectionData(string sectionName)
        {
            if (sectionName == null)
                Throw.ArgumentNull(nameof(sectionName));
            int index = this.PEHeaders.IndexOfSection(sectionName);
            return index < 0 ? new PEMemoryBlock() : new PEMemoryBlock(this.GetPESectionBlock(index));
        }

        /// <summary>Reads all Debug Directory table entries.</summary>
        /// <exception cref="T:System.BadImageFormatException">Bad format of the entry.</exception>
        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        public ImmutableArray<DebugDirectoryEntry> ReadDebugDirectory()
        {
            DirectoryEntry debugTableDirectory = this.PEHeaders.PEHeader.DebugTableDirectory;
            if (debugTableDirectory.Size == 0)
                return ImmutableArray<DebugDirectoryEntry>.Empty;
            int offset;
            if (!this.PEHeaders.TryGetDirectoryOffset(debugTableDirectory, out offset))
        throw new BadImageFormatException(SR.InvalidDirectoryRVA);
            if (debugTableDirectory.Size % 28 != 0)
        throw new BadImageFormatException(SR.InvalidDirectorySize);
            using (AbstractMemoryBlock memoryBlock = this.GetPEImage().GetMemoryBlock(offset, debugTableDirectory.Size))
                return PEReader.ReadDebugDirectoryEntries(memoryBlock.GetReader());
        }

        internal static ImmutableArray<DebugDirectoryEntry> ReadDebugDirectoryEntries(BlobReader reader)
        {
            int initialCapacity = reader.Length / 28;
            ImmutableArray<DebugDirectoryEntry>.Builder builder = ImmutableArray.CreateBuilder<DebugDirectoryEntry>(initialCapacity);
            for (int index = 0; index < initialCapacity; ++index)
            {
        uint stamp = reader.ReadInt32() == 0 ? reader.ReadUInt32() : throw new BadImageFormatException(SR.InvalidDebugDirectoryEntryCharacteristics);
                ushort majorVersion = reader.ReadUInt16();
                ushort minorVersion = reader.ReadUInt16();
                DebugDirectoryEntryType type = (DebugDirectoryEntryType)reader.ReadInt32();
                int dataSize = reader.ReadInt32();
                int dataRelativeVirtualAddress = reader.ReadInt32();
                int dataPointer = reader.ReadInt32();
                builder.Add(new DebugDirectoryEntry(stamp, majorVersion, minorVersion, type, dataSize, dataRelativeVirtualAddress, dataPointer));
            }
            return builder.MoveToImmutable();
        }


#nullable disable
        private AbstractMemoryBlock GetDebugDirectoryEntryDataBlock(DebugDirectoryEntry entry)
        {
            int start = this.IsLoadedImage ? entry.DataRelativeVirtualAddress : entry.DataPointer;
            return this.GetPEImage().GetMemoryBlock(start, entry.DataSize);
        }

        /// <summary>
        /// Reads the data pointed to by the specified Debug Directory entry and interprets them as CodeView.
        /// </summary>
        /// <exception cref="T:System.ArgumentException"><paramref name="entry" /> is not a CodeView entry.</exception>
        /// <exception cref="T:System.BadImageFormatException">Bad format of the data.</exception>
        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        public CodeViewDebugDirectoryData ReadCodeViewDebugDirectoryData(DebugDirectoryEntry entry)
        {
            if (entry.Type != DebugDirectoryEntryType.CodeView)
        Throw.InvalidArgument(SR.Format(SR.UnexpectedDebugDirectoryType, (object) "CodeView"), nameof (entry));
            using (AbstractMemoryBlock directoryEntryDataBlock = this.GetDebugDirectoryEntryDataBlock(entry))
                return PEReader.DecodeCodeViewDebugDirectoryData(directoryEntryDataBlock);
        }


#nullable enable
        internal static CodeViewDebugDirectoryData DecodeCodeViewDebugDirectoryData(
          AbstractMemoryBlock block)
        {
            BlobReader reader = block.GetReader();
      return reader.ReadByte() == (byte) 82 && reader.ReadByte() == (byte) 83 && reader.ReadByte() == (byte) 68 && reader.ReadByte() == (byte) 83 ? new CodeViewDebugDirectoryData(reader.ReadGuid(), reader.ReadInt32(), reader.ReadUtf8NullTerminated()) : throw new BadImageFormatException(SR.UnexpectedCodeViewDataSignature);
        }

        /// <summary>
        /// Reads the data pointed to by the specified Debug Directory entry and interprets them as PDB Checksum entry.
        /// </summary>
        /// <exception cref="T:System.ArgumentException"><paramref name="entry" /> is not a PDB Checksum entry.</exception>
        /// <exception cref="T:System.BadImageFormatException">Bad format of the data.</exception>
        /// <exception cref="T:System.IO.IOException">IO error while reading from the underlying stream.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        public PdbChecksumDebugDirectoryData ReadPdbChecksumDebugDirectoryData(DebugDirectoryEntry entry)
        {
            if (entry.Type != DebugDirectoryEntryType.PdbChecksum)
        Throw.InvalidArgument(SR.Format(SR.UnexpectedDebugDirectoryType, (object) "PdbChecksum"), nameof (entry));
            using (AbstractMemoryBlock directoryEntryDataBlock = this.GetDebugDirectoryEntryDataBlock(entry))
                return PEReader.DecodePdbChecksumDebugDirectoryData(directoryEntryDataBlock);
        }

        internal static PdbChecksumDebugDirectoryData DecodePdbChecksumDebugDirectoryData(
          AbstractMemoryBlock block)
        {
            BlobReader reader = block.GetReader();
            string algorithmName = reader.ReadUtf8NullTerminated();
            byte[] array = reader.ReadBytes(reader.RemainingBytes);
            if (algorithmName.Length == 0 || array.Length == 0)
        throw new BadImageFormatException(SR.InvalidPdbChecksumDataFormat);
            return new PdbChecksumDebugDirectoryData(algorithmName, ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref array));
        }

        /// <summary>Opens a Portable PDB associated with this PE image.</summary>
        /// <param name="peImagePath">
        /// The path to the PE image. The path is used to locate the PDB file located in the directory containing the PE file.
        /// </param>
        /// <param name="pdbFileStreamProvider">
        /// If specified, called to open a <see cref="T:System.IO.Stream" /> for a given file path.
        /// The provider is expected to either return a readable and seekable <see cref="T:System.IO.Stream" />,
        /// or <c>null</c> if the target file doesn't exist or should be ignored for some reason.
        /// 
        /// The provider shall throw <see cref="T:System.IO.IOException" /> if it fails to open the file due to an unexpected IO error.
        /// </param>
        /// <param name="pdbReaderProvider">
        /// If successful, a new instance of <see cref="T:System.Reflection.Metadata.MetadataReaderProvider" /> to be used to read the Portable PDB,.
        /// </param>
        /// <param name="pdbPath">
        /// If successful and the PDB is found in a file, the path to the file. Returns <c>null</c> if the PDB is embedded in the PE image itself.
        /// </param>
        /// <returns>
        /// True if the PE image has a PDB associated with it and the PDB has been successfully opened.
        /// </returns>
        /// <remarks>
        /// Implements a simple PDB file lookup based on the content of the PE image Debug Directory.
        /// A sophisticated tool might need to follow up with additional lookup on search paths or symbol server.
        /// 
        /// The method looks the PDB up in the following steps in the listed order:
        /// 1) Check for a matching PDB file of the name found in the CodeView entry in the directory containing the PE file (the directory of <paramref name="peImagePath" />).
        /// 2) Check for a PDB embedded in the PE image itself.
        /// 
        /// The first PDB that matches the information specified in the Debug Directory is returned.
        /// </remarks>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="peImagePath" /> or <paramref name="pdbFileStreamProvider" /> is null.</exception>
        /// <exception cref="T:System.InvalidOperationException">The stream returned from <paramref name="pdbFileStreamProvider" /> doesn't support read and seek operations.</exception>
        /// <exception cref="T:System.BadImageFormatException">No matching PDB file is found due to an error: The PE image or the PDB is invalid.</exception>
        /// <exception cref="T:System.IO.IOException">No matching PDB file is found due to an error: An IO error occurred while reading the PE image or the PDB.</exception>
        public bool TryOpenAssociatedPortablePdb(
          string peImagePath,
          Func<string, Stream?> pdbFileStreamProvider,
          out MetadataReaderProvider? pdbReaderProvider,
          out string? pdbPath)
        {
            if (peImagePath == null)
                Throw.ArgumentNull(nameof(peImagePath));
            if (pdbFileStreamProvider == null)
                Throw.ArgumentNull(nameof(pdbFileStreamProvider));
            pdbReaderProvider = (MetadataReaderProvider)null;
            pdbPath = (string)null;
            string directoryName;
            try
            {
                directoryName = Path.GetDirectoryName(peImagePath);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message, nameof(peImagePath));
            }
            Exception errorToReport = (Exception)null;
            ImmutableArray<DebugDirectoryEntry> collection = this.ReadDebugDirectory();
            DebugDirectoryEntry codeViewEntry = collection.FirstOrDefault<DebugDirectoryEntry>((Func<DebugDirectoryEntry, bool>)(e => e.IsPortableCodeView));
            if (codeViewEntry.DataSize != 0 && this.TryOpenCodeViewPortablePdb(codeViewEntry, directoryName, pdbFileStreamProvider, out pdbReaderProvider, out pdbPath, ref errorToReport))
                return true;
            DebugDirectoryEntry embeddedPdbEntry = collection.FirstOrDefault<DebugDirectoryEntry>((Func<DebugDirectoryEntry, bool>)(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb));
            if (embeddedPdbEntry.DataSize != 0)
            {
                bool openedEmbeddedPdb = false;
                pdbReaderProvider = (MetadataReaderProvider)null;
                this.TryOpenEmbeddedPortablePdb(embeddedPdbEntry, ref openedEmbeddedPdb, ref pdbReaderProvider, ref errorToReport);
                if (openedEmbeddedPdb)
                    return true;
            }
            if (errorToReport != null)
                ExceptionDispatchInfo.Capture(errorToReport).Throw();
            return false;
        }


#nullable disable
        private bool TryOpenCodeViewPortablePdb(
          DebugDirectoryEntry codeViewEntry,
          string peImageDirectory,
          Func<string, Stream> pdbFileStreamProvider,
          out MetadataReaderProvider provider,
          out string pdbPath,
          ref Exception errorToReport)
        {
            pdbPath = (string)null;
            provider = (MetadataReaderProvider)null;
            CodeViewDebugDirectoryData debugDirectoryData;
            try
            {
                debugDirectoryData = this.ReadCodeViewDebugDirectoryData(codeViewEntry);
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is IOException)
            {
                if (errorToReport == null)
                    errorToReport = ex;
                return false;
            }
            BlobContentId id = new BlobContentId(debugDirectoryData.Guid, codeViewEntry.Stamp);
            string path = PathUtilities.CombinePathWithRelativePath(peImageDirectory, PathUtilities.GetFileName(debugDirectoryData.Path));
            if (!PEReader.TryOpenPortablePdbFile(path, id, pdbFileStreamProvider, out provider, ref errorToReport))
                return false;
            pdbPath = path;
            return true;
        }

        private static bool TryOpenPortablePdbFile(
          string path,
          BlobContentId id,
          Func<string, Stream> pdbFileStreamProvider,
          out MetadataReaderProvider provider,
          ref Exception errorToReport)
        {
            provider = (MetadataReaderProvider)null;
            MetadataReaderProvider metadataReaderProvider = (MetadataReaderProvider)null;
            try
            {
                Stream stream;
                try
                {
                    stream = pdbFileStreamProvider(path);
                }
                catch (FileNotFoundException ex)
                {
                    stream = (Stream)null;
                }
                if (stream == null)
                    return false;
                if (!stream.CanRead || !stream.CanSeek)
          throw new InvalidOperationException(SR.StreamMustSupportReadAndSeek);
                metadataReaderProvider = MetadataReaderProvider.FromPortablePdbStream(stream);
                if (new BlobContentId(metadataReaderProvider.GetMetadataReader().DebugMetadataHeader.Id) != id)
                    return false;
                provider = metadataReaderProvider;
                return true;
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is IOException)
            {
                if (errorToReport == null)
                    errorToReport = ex;
                return false;
            }
            finally
            {
                if (provider == null && metadataReaderProvider != null)
                    metadataReaderProvider.Dispose();
            }
        }

        private void TryOpenEmbeddedPortablePdb(
          DebugDirectoryEntry embeddedPdbEntry,
          ref bool openedEmbeddedPdb,
          ref MetadataReaderProvider provider,
          ref Exception errorToReport)
        {
            provider = (MetadataReaderProvider)null;
            MetadataReaderProvider metadataReaderProvider = (MetadataReaderProvider)null;
            try
            {
                metadataReaderProvider = this.ReadEmbeddedPortablePdbDebugDirectoryData(embeddedPdbEntry);
                metadataReaderProvider.GetMetadataReader();
                provider = metadataReaderProvider;
                openedEmbeddedPdb = true;
            }
            catch (Exception ex) when (ex is BadImageFormatException || ex is IOException)
            {
                if (errorToReport == null)
                    errorToReport = ex;
                openedEmbeddedPdb = false;
            }
            finally
            {
                if (provider == null && metadataReaderProvider != null)
                    metadataReaderProvider.Dispose();
            }
        }


#nullable enable
        /// <summary>
        /// Reads the data pointed to by the specified Debug Directory entry and interprets them as Embedded Portable PDB blob.
        /// </summary>
        /// <returns>
        /// Provider of a metadata reader reading the embedded Portable PDB image.
        /// Dispose to release resources allocated for the embedded PDB.
        /// </returns>
        /// <exception cref="T:System.ArgumentException"><paramref name="entry" /> is not a <see cref="F:System.Reflection.PortableExecutable.DebugDirectoryEntryType.EmbeddedPortablePdb" /> entry.</exception>
        /// <exception cref="T:System.BadImageFormatException">Bad format of the data.</exception>
        /// <exception cref="T:System.InvalidOperationException">PE image not available.</exception>
        public MetadataReaderProvider ReadEmbeddedPortablePdbDebugDirectoryData(
          DebugDirectoryEntry entry)
        {
            if (entry.Type != DebugDirectoryEntryType.EmbeddedPortablePdb)
        Throw.InvalidArgument(SR.Format(SR.UnexpectedDebugDirectoryType, (object) "EmbeddedPortablePdb"), nameof (entry));
            PEReader.ValidateEmbeddedPortablePdbVersion(entry);
            using (AbstractMemoryBlock directoryEntryDataBlock = this.GetDebugDirectoryEntryDataBlock(entry))
                return new MetadataReaderProvider((AbstractMemoryBlock)PEReader.DecodeEmbeddedPortablePdbDebugDirectoryData(directoryEntryDataBlock));
        }

        internal static void ValidateEmbeddedPortablePdbVersion(DebugDirectoryEntry entry)
        {
            ushort majorVersion = entry.MajorVersion;
            if (majorVersion < (ushort)256)
        throw new BadImageFormatException(SR.Format(SR.UnsupportedFormatVersion, (object) PortablePdbVersions.Format(majorVersion)));
            ushort minorVersion = entry.MinorVersion;
            if (minorVersion != (ushort)256)
        throw new BadImageFormatException(SR.Format(SR.UnsupportedFormatVersion, (object) PortablePdbVersions.Format(minorVersion)));
        }

        internal static unsafe NativeHeapMemoryBlock DecodeEmbeddedPortablePdbDebugDirectoryData(
          AbstractMemoryBlock block)
        {
            BlobReader reader = block.GetReader();
      int size = reader.ReadUInt32() == 1111773261U ? reader.ReadInt32() : throw new BadImageFormatException(SR.UnexpectedEmbeddedPortablePdbDataSignature);
            NativeHeapMemoryBlock nativeHeapMemoryBlock;
            try
            {
                nativeHeapMemoryBlock = new NativeHeapMemoryBlock(size);
            }
            catch
            {
        throw new BadImageFormatException(SR.DataTooBig);
            }
            bool flag = false;
            try
            {
                using (DeflateStream deflateStream = new DeflateStream((Stream)new ReadOnlyUnmanagedMemoryStream(reader.CurrentPointer, reader.RemainingBytes), CompressionMode.Decompress, true))
                {
                    if (size > 0)
                    {
                        int position;
                        try
                        {
                            using (UnmanagedMemoryStream destination = new UnmanagedMemoryStream(nativeHeapMemoryBlock.Pointer, (long)nativeHeapMemoryBlock.Size, (long)nativeHeapMemoryBlock.Size, FileAccess.Write))
                            {
                                deflateStream.CopyTo((Stream)destination);
                                position = (int)destination.Position;
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new BadImageFormatException(ex.Message, ex.InnerException);
                        }
                        if (position != nativeHeapMemoryBlock.Size)
              throw new BadImageFormatException(SR.SizeMismatch);
                    }
                    if (deflateStream.ReadByte() != -1)
            throw new BadImageFormatException(SR.SizeMismatch);
                    flag = true;
                }
            }
            finally
            {
                if (!flag)
                    nativeHeapMemoryBlock.Dispose();
            }
            return nativeHeapMemoryBlock;
        }
    }
}

﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MetadataReader
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;
using Datadog.System.Reflection.Metadata.Ecma335;
using Datadog.System.Reflection.PortableExecutable;
using Microsoft.Win32.SafeHandles;
using Datadog.System;

#nullable enable
namespace Datadog.System.Reflection.Metadata
{
    /// <summary>
    /// Reads metadata as defined byte the ECMA 335 CLI specification.
    /// </summary>
    public sealed class MetadataReader
    {
        internal readonly NamespaceCache NamespaceCache;
        internal readonly MemoryBlock Block;
        internal readonly int WinMDMscorlibRef;

#nullable disable
        private readonly object _memoryOwnerObj;
        private readonly MetadataReaderOptions _options;
        private Dictionary<TypeDefinitionHandle, ImmutableArray<TypeDefinitionHandle>> _lazyNestedTypesMap;
        private readonly string _versionString;
        private readonly MetadataKind _metadataKind;
        private readonly MetadataStreamKind _metadataStreamKind;
        private readonly DebugMetadataHeader _debugMetadataHeader;
        internal StringHeap StringHeap;
        internal BlobHeap BlobHeap;
        internal GuidHeap GuidHeap;
        internal UserStringHeap UserStringHeap;
        /// <summary>
        /// True if the metadata stream has minimal delta format. Used for EnC.
        /// </summary>
        /// <remarks>
        /// The metadata stream has minimal delta format if "#JTD" stream is present.
        /// Minimal delta format uses large size (4B) when encoding table/heap references.
        /// The heaps in minimal delta only contain data of the delta,
        /// there is no padding at the beginning of the heaps that would align them
        /// with the original full metadata heaps.
        /// </remarks>
        internal bool IsMinimalDelta;
        private readonly TableMask _sortedTables;

#nullable enable
        /// <summary>
        /// A row count for each possible table. May be indexed by <see cref="T:System.Reflection.Metadata.Ecma335.TableIndex" />.
        /// </summary>
        internal int[] TableRowCounts;
        internal ModuleTableReader ModuleTable;
        internal TypeRefTableReader TypeRefTable;
        internal TypeDefTableReader TypeDefTable;
        internal FieldPtrTableReader FieldPtrTable;
        internal FieldTableReader FieldTable;
        internal MethodPtrTableReader MethodPtrTable;
        internal MethodTableReader MethodDefTable;
        internal ParamPtrTableReader ParamPtrTable;
        internal ParamTableReader ParamTable;
        internal InterfaceImplTableReader InterfaceImplTable;
        internal MemberRefTableReader MemberRefTable;
        internal ConstantTableReader ConstantTable;
        internal CustomAttributeTableReader CustomAttributeTable;
        internal FieldMarshalTableReader FieldMarshalTable;
        internal DeclSecurityTableReader DeclSecurityTable;
        internal ClassLayoutTableReader ClassLayoutTable;
        internal FieldLayoutTableReader FieldLayoutTable;
        internal StandAloneSigTableReader StandAloneSigTable;
        internal EventMapTableReader EventMapTable;
        internal EventPtrTableReader EventPtrTable;
        internal EventTableReader EventTable;
        internal PropertyMapTableReader PropertyMapTable;
        internal PropertyPtrTableReader PropertyPtrTable;
        internal PropertyTableReader PropertyTable;
        internal MethodSemanticsTableReader MethodSemanticsTable;
        internal MethodImplTableReader MethodImplTable;
        internal ModuleRefTableReader ModuleRefTable;
        internal TypeSpecTableReader TypeSpecTable;
        internal ImplMapTableReader ImplMapTable;
        internal FieldRVATableReader FieldRvaTable;
        internal EnCLogTableReader EncLogTable;
        internal EnCMapTableReader EncMapTable;
        internal AssemblyTableReader AssemblyTable;
        internal AssemblyProcessorTableReader AssemblyProcessorTable;
        internal AssemblyOSTableReader AssemblyOSTable;
        internal AssemblyRefTableReader AssemblyRefTable;
        internal AssemblyRefProcessorTableReader AssemblyRefProcessorTable;
        internal AssemblyRefOSTableReader AssemblyRefOSTable;
        internal FileTableReader FileTable;
        internal ExportedTypeTableReader ExportedTypeTable;
        internal ManifestResourceTableReader ManifestResourceTable;
        internal NestedClassTableReader NestedClassTable;
        internal GenericParamTableReader GenericParamTable;
        internal MethodSpecTableReader MethodSpecTable;
        internal GenericParamConstraintTableReader GenericParamConstraintTable;
        internal DocumentTableReader DocumentTable;
        internal MethodDebugInformationTableReader MethodDebugInformationTable;
        internal LocalScopeTableReader LocalScopeTable;
        internal LocalVariableTableReader LocalVariableTable;
        internal LocalConstantTableReader LocalConstantTable;
        internal ImportScopeTableReader ImportScopeTable;
        internal StateMachineMethodTableReader StateMachineMethodTable;
        internal CustomDebugInformationTableReader CustomDebugInformationTable;
        private const int SmallIndexSize = 2;
        private const int LargeIndexSize = 4;
        internal const string ClrPrefix = "<CLR>";
        internal static readonly byte[] WinRTPrefix = Encoding.UTF8.GetBytes("<WinRT>");

#nullable disable
        private static string[] s_projectedTypeNames;
        private static MetadataReader.ProjectionInfo[] s_projectionInfos;


#nullable enable
        internal AssemblyName GetAssemblyName(
          StringHandle nameHandle,
          Version version,
          StringHandle cultureHandle,
          BlobHandle publicKeyOrTokenHandle,
          System.Reflection.AssemblyHashAlgorithm assemblyHashAlgorithm,
          AssemblyFlags flags)
        {
            string str1 = this.GetString(nameHandle);
            string str2 = !cultureHandle.IsNil ? this.GetString(cultureHandle) : "";
            global::System.Configuration.Assemblies.AssemblyHashAlgorithm assemblyHashAlgorithm1 = (global::System.Configuration.Assemblies.AssemblyHashAlgorithm)assemblyHashAlgorithm;
            byte[] numArray = !publicKeyOrTokenHandle.IsNil ? this.GetBlobBytes(publicKeyOrTokenHandle) : Array.Empty<byte>();
            AssemblyName assemblyName = new AssemblyName()
            {
                Name = str1,
                Version = version,
                CultureName = str2,
                HashAlgorithm = assemblyHashAlgorithm1,
                Flags = MetadataReader.GetAssemblyNameFlags(flags),
                ContentType = MetadataReader.GetContentTypeFromAssemblyFlags(flags)
            };
            if ((flags & AssemblyFlags.PublicKey) != 0)
                assemblyName.SetPublicKey(numArray);
            else
                assemblyName.SetPublicKeyToken(numArray);
            return assemblyName;
        }

        /// <summary>
        /// Gets the <see cref="T:System.Reflection.AssemblyName" /> for a given file.
        /// </summary>
        /// <param name="assemblyFile">The path for the assembly which <see cref="T:System.Reflection.AssemblyName" /> is to be returned.</param>
        /// <returns>An <see cref="T:System.Reflection.AssemblyName" /> that represents the given <paramref name="assemblyFile" />.</returns>
        /// <exception cref="T:System.ArgumentNullException">If <paramref name="assemblyFile" /> is null.</exception>
        /// <exception cref="T:System.ArgumentException">If <paramref name="assemblyFile" /> is invalid.</exception>
        /// <exception cref="T:System.IO.FileNotFoundException">If <paramref name="assemblyFile" /> is not found.</exception>
        /// <exception cref="T:System.BadImageFormatException">If <paramref name="assemblyFile" /> is not a valid assembly.</exception>
        public static unsafe AssemblyName GetAssemblyName(string assemblyFile)
        {
            if (assemblyFile == null)
                Throw.ArgumentNull(nameof(assemblyFile));
            FileStream fileStream = (FileStream)null;
            MemoryMappedFile memoryMappedFile = (MemoryMappedFile)null;
            MemoryMappedViewAccessor mappedViewAccessor = (MemoryMappedViewAccessor)null;
            PEReader peReader = (PEReader)null;
            try
            {
                try
                {
                    fileStream = new FileStream(assemblyFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1, false);
                    memoryMappedFile = fileStream.Length != 0L ? MemoryMappedFile.CreateFromFile(fileStream, (string)null, fileStream.Length, MemoryMappedFileAccess.Read, HandleInheritability.None, true) : throw new BadImageFormatException(SR.PEImageDoesNotHaveMetadata, assemblyFile);
                    mappedViewAccessor = memoryMappedFile.CreateViewAccessor(0L, 0L, MemoryMappedFileAccess.Read);
                    SafeMemoryMappedViewHandle mappedViewHandle = mappedViewAccessor.SafeMemoryMappedViewHandle;
                    peReader = new PEReader((byte*)(void*)mappedViewHandle.DangerousGetHandle(), (int)mappedViewHandle.ByteLength);
                    return peReader.GetMetadataReader(MetadataReaderOptions.None).GetAssemblyDefinition().GetAssemblyName();
                }
                finally
                {
                    peReader?.Dispose();
                    mappedViewAccessor?.Dispose();
                    memoryMappedFile?.Dispose();
                    fileStream?.Dispose();
                }
            }
            catch (InvalidOperationException ex)
            {
                throw new BadImageFormatException(ex.Message);
            }
        }

        private static AssemblyNameFlags GetAssemblyNameFlags(AssemblyFlags flags)
        {
            AssemblyNameFlags assemblyNameFlags = AssemblyNameFlags.None;
            if ((flags & AssemblyFlags.PublicKey) != (AssemblyFlags)0)
                assemblyNameFlags |= AssemblyNameFlags.PublicKey;
            if ((flags & AssemblyFlags.Retargetable) != (AssemblyFlags)0)
                assemblyNameFlags |= AssemblyNameFlags.Retargetable;
            if ((flags & AssemblyFlags.EnableJitCompileTracking) != (AssemblyFlags)0)
                assemblyNameFlags |= AssemblyNameFlags.EnableJITcompileTracking;
            if ((flags & AssemblyFlags.DisableJitCompileOptimizer) != (AssemblyFlags)0)
                assemblyNameFlags |= AssemblyNameFlags.EnableJITcompileOptimizer;
            return assemblyNameFlags;
        }

        private static AssemblyContentType GetContentTypeFromAssemblyFlags(AssemblyFlags flags) => (AssemblyContentType)((int)(flags & AssemblyFlags.ContentTypeMask) >> 9);

        /// <summary>
        /// Creates a metadata reader from the metadata stored at the given memory location.
        /// </summary>
        /// <remarks>
        /// The memory is owned by the caller and it must be kept memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.Metadata.MetadataReader" />.
        /// </remarks>
        public unsafe MetadataReader(byte* metadata, int length)
          : this(metadata, length, MetadataReaderOptions.Default, (MetadataStringDecoder)null, (object)null)
        {
        }

        /// <summary>
        /// Creates a metadata reader from the metadata stored at the given memory location.
        /// </summary>
        /// <remarks>
        /// The memory is owned by the caller and it must be kept memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.Metadata.MetadataReader" />.
        /// Use <see cref="M:System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(System.Reflection.PortableExecutable.PEReader,System.Reflection.Metadata.MetadataReaderOptions)" /> to obtain
        /// metadata from a PE image.
        /// </remarks>
        public unsafe MetadataReader(byte* metadata, int length, MetadataReaderOptions options)
          : this(metadata, length, options, (MetadataStringDecoder)null, (object)null)
        {
        }

        /// <summary>
        /// Creates a metadata reader from the metadata stored at the given memory location.
        /// </summary>
        /// <remarks>
        /// The memory is owned by the caller and it must be kept memory alive and unmodified throughout the lifetime of the <see cref="T:System.Reflection.Metadata.MetadataReader" />.
        /// Use <see cref="M:System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(System.Reflection.PortableExecutable.PEReader,System.Reflection.Metadata.MetadataReaderOptions,System.Reflection.Metadata.MetadataStringDecoder)" /> to obtain
        /// metadata from a PE image.
        /// </remarks>
        /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="length" /> is not positive.</exception>
        /// <exception cref="T:System.ArgumentNullException"><paramref name="metadata" /> is null.</exception>
        /// <exception cref="T:System.ArgumentException">The encoding of <paramref name="utf8Decoder" /> is not <see cref="T:System.Text.UTF8Encoding" />.</exception>
        /// <exception cref="T:System.PlatformNotSupportedException">The current platform is big-endian.</exception>
        /// <exception cref="T:System.BadImageFormatException">Bad metadata header.</exception>
        public unsafe MetadataReader(
          byte* metadata,
          int length,
          MetadataReaderOptions options,
          MetadataStringDecoder? utf8Decoder)
          : this(metadata, length, options, utf8Decoder, (object)null)
        {
        }

        internal unsafe MetadataReader(
          byte* metadata,
          int length,
          MetadataReaderOptions options,
          MetadataStringDecoder? utf8Decoder,
          object? memoryOwner)
        {
            if (length < 0)
                Throw.ArgumentOutOfRange(nameof(length));
            if ((IntPtr)metadata == IntPtr.Zero)
                Throw.ArgumentNull(nameof(metadata));
            if (utf8Decoder == null)
                utf8Decoder = MetadataStringDecoder.DefaultUTF8;
            if (!(utf8Decoder.Encoding is UTF8Encoding))
                Throw.InvalidArgument(SR.MetadataStringDecoderEncodingMustBeUtf8, nameof(utf8Decoder));
            this.Block = new MemoryBlock(metadata, length);
            this._memoryOwnerObj = memoryOwner;
            this._options = options;
            this.UTF8Decoder = utf8Decoder;
            BlobReader memReader = new BlobReader(this.Block);
            this.ReadMetadataHeader(ref memReader, out this._versionString);
            this._metadataKind = this.GetMetadataKind(this._versionString);
            MemoryBlock metadataTableStream;
            MemoryBlock standalonePdbStream;
            this.InitializeStreamReaders(in this.Block, MetadataReader.ReadStreamHeaders(ref memReader), out this._metadataStreamKind, out metadataTableStream, out standalonePdbStream);
            int[] externalTableRowCounts;
            if (standalonePdbStream.Length > 0)
            {
                int pdbStreamOffset = (int)(standalonePdbStream.Pointer - metadata);
                MetadataReader.ReadStandalonePortablePdbStream(standalonePdbStream, pdbStreamOffset, out this._debugMetadataHeader, out externalTableRowCounts);
            }
            else
                externalTableRowCounts = (int[])null;
            BlobReader reader = new BlobReader(metadataTableStream);
            HeapSizes heapSizes;
            int[] metadataTableRowCounts;
            this.ReadMetadataTableHeader(ref reader, out heapSizes, out metadataTableRowCounts, out this._sortedTables);
            this.InitializeTableReaders(reader.GetMemoryBlockAt(0, reader.RemainingBytes), heapSizes, metadataTableRowCounts, externalTableRowCounts);
            if (standalonePdbStream.Length == 0 && this.ModuleTable.NumberOfRows < 1)
                throw new BadImageFormatException(SR.Format(SR.ModuleTableInvalidNumberOfRows, (object)this.ModuleTable.NumberOfRows));
            this.NamespaceCache = new NamespaceCache(this);
            if (this._metadataKind == MetadataKind.Ecma335)
                return;
            this.WinMDMscorlibRef = this.FindMscorlibAssemblyRefNoProjection();
        }


#nullable disable
        /// <summary>
        /// Looks like this function reads beginning of the header described in
        /// ECMA-335 24.2.1 Metadata root
        /// </summary>
        private void ReadMetadataHeader(ref BlobReader memReader, out string versionString)
        {
            if (memReader.RemainingBytes < 16)
                throw new BadImageFormatException(SR.MetadataHeaderTooSmall);
            if (memReader.ReadUInt32() != 1112167234U)
                throw new BadImageFormatException(SR.MetadataSignature);
            int num1 = (int)memReader.ReadUInt16();
            int num2 = (int)memReader.ReadUInt16();
            int num3 = (int)memReader.ReadUInt32();
            int length = memReader.ReadInt32();
            if (memReader.RemainingBytes < length)
                throw new BadImageFormatException(SR.NotEnoughSpaceForVersionString);
            versionString = memReader.GetMemoryBlockAt(0, length).PeekUtf8NullTerminated(0, (byte[])null, this.UTF8Decoder, out int _);
            memReader.Offset += length;
        }

        private MetadataKind GetMetadataKind(string versionString)
        {
            if ((this._options & MetadataReaderOptions.Default) == MetadataReaderOptions.None || !versionString.Contains("WindowsRuntime"))
                return MetadataKind.Ecma335;
            return versionString.Contains("CLR") ? MetadataKind.ManagedWindowsMetadata : MetadataKind.WindowsMetadata;
        }

        /// <summary>
        /// Reads stream headers described in ECMA-335 24.2.2 Stream header
        /// </summary>
        private static StreamHeader[] ReadStreamHeaders(ref BlobReader memReader)
        {
            int num = (int)memReader.ReadUInt16();
            StreamHeader[] streamHeaderArray = new StreamHeader[(int)memReader.ReadInt16()];
            for (int index = 0; index < streamHeaderArray.Length; ++index)
            {
                if (memReader.RemainingBytes < 8)
                    throw new BadImageFormatException(SR.StreamHeaderTooSmall);
                streamHeaderArray[index].Offset = memReader.ReadUInt32();
                streamHeaderArray[index].Size = memReader.ReadInt32();
                streamHeaderArray[index].Name = memReader.ReadUtf8NullTerminated();
                if (!memReader.TryAlign((byte)4) || memReader.RemainingBytes == 0)
                    throw new BadImageFormatException(SR.NotEnoughSpaceForStreamHeaderName);
            }
            return streamHeaderArray;
        }

        private void InitializeStreamReaders(
          in MemoryBlock metadataRoot,
          StreamHeader[] streamHeaders,
          out MetadataStreamKind metadataStreamKind,
          out MemoryBlock metadataTableStream,
          out MemoryBlock standalonePdbStream)
        {
            metadataTableStream = new MemoryBlock();
            standalonePdbStream = new MemoryBlock();
            metadataStreamKind = MetadataStreamKind.Illegal;
            foreach (StreamHeader streamHeader in streamHeaders)
            {
                switch (streamHeader.Name)
                {
                    case "#-":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForMetadataStream);
                        metadataStreamKind = MetadataStreamKind.Uncompressed;
                        metadataTableStream = metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size);
                        break;
                    case "#Blob":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForBlobStream);
                        this.BlobHeap = new BlobHeap(metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size), this._metadataKind);
                        break;
                    case "#GUID":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForGUIDStream);
                        this.GuidHeap = new GuidHeap(metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size));
                        break;
                    case "#JTD":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForMetadataStream);
                        this.IsMinimalDelta = true;
                        break;
                    case "#Pdb":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForMetadataStream);
                        standalonePdbStream = metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size);
                        break;
                    case "#Strings":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForStringStream);
                        this.StringHeap = new StringHeap(metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size), this._metadataKind);
                        break;
                    case "#US":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForBlobStream);
                        this.UserStringHeap = new UserStringHeap(metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size));
                        break;
                    case "#~":
                        if ((long)metadataRoot.Length < (long)streamHeader.Offset + (long)streamHeader.Size)
                            throw new BadImageFormatException(SR.NotEnoughSpaceForMetadataStream);
                        metadataStreamKind = MetadataStreamKind.Compressed;
                        metadataTableStream = metadataRoot.GetMemoryBlockAt((int)streamHeader.Offset, streamHeader.Size);
                        break;
                }
            }
            if (this.IsMinimalDelta && metadataStreamKind != MetadataStreamKind.Uncompressed)
                throw new BadImageFormatException(SR.InvalidMetadataStreamFormat);
        }

        private void ReadMetadataTableHeader(
          ref BlobReader reader,
          out HeapSizes heapSizes,
          out int[] metadataTableRowCounts,
          out TableMask sortedTables)
        {
            if (reader.RemainingBytes < 24)
                throw new BadImageFormatException(SR.MetadataTableHeaderTooSmall);
            int num1 = (int)reader.ReadUInt32();
            int num2 = (int)reader.ReadByte();
            int num3 = (int)reader.ReadByte();
            heapSizes = (HeapSizes)reader.ReadByte();
            int num4 = (int)reader.ReadByte();
            ulong num5 = reader.ReadUInt64();
            sortedTables = (TableMask)reader.ReadUInt64();
            ulong num6 = 71811071505072127;
            if (((long)num5 & ~(long)num6) != 0L)
                throw new BadImageFormatException(SR.Format(SR.UnknownTables, (object)num5));
            metadataTableRowCounts = this._metadataStreamKind != MetadataStreamKind.Compressed || ((long)num5 & 2152202408L) == 0L ? MetadataReader.ReadMetadataTableRowCounts(ref reader, num5) : throw new BadImageFormatException(SR.IllegalTablesInCompressedMetadataStream);
            if ((heapSizes & HeapSizes.ExtraData) != HeapSizes.ExtraData)
                return;
            int num7 = (int)reader.ReadUInt32();
        }

        private static int[] ReadMetadataTableRowCounts(
          ref BlobReader memReader,
          ulong presentTableMask)
        {
            ulong num = 1;
            int[] numArray = new int[MetadataTokens.TableCount];
            for (int index = 0; index < numArray.Length; ++index)
            {
                if (((long)presentTableMask & (long)num) != 0L)
                {
                    if (memReader.RemainingBytes < 4)
                        throw new BadImageFormatException(SR.TableRowCountSpaceTooSmall);
                    uint p1 = memReader.ReadUInt32();
                    numArray[index] = p1 <= 16777215U ? (int)p1 : throw new BadImageFormatException(SR.Format(SR.InvalidRowCount, (object)p1));
                }
                num <<= 1;
            }
            return numArray;
        }


#nullable enable
        internal static void ReadStandalonePortablePdbStream(
          MemoryBlock pdbStreamBlock,
          int pdbStreamOffset,
          out DebugMetadataHeader debugMetadataHeader,
          out int[] externalTableRowCounts)
        {
            BlobReader memReader = new BlobReader(pdbStreamBlock);
            byte[] array = memReader.ReadBytes(20);
            uint p1 = memReader.ReadUInt32();
            int rowId = (int)p1 & 16777215;
            if (p1 != 0U && (((int)p1 & 2130706432) != 100663296 || rowId == 0))
                throw new BadImageFormatException(SR.Format(SR.InvalidEntryPointToken, (object)p1));
            ulong num = memReader.ReadUInt64();
            externalTableRowCounts = ((long)num & -34949217910616L) == 0L ? MetadataReader.ReadMetadataTableRowCounts(ref memReader, num) : throw new BadImageFormatException(SR.Format(SR.UnknownTables, (object)num));
            debugMetadataHeader = new DebugMetadataHeader(ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref array), MethodDefinitionHandle.FromRowId(rowId), pdbStreamOffset);
        }


#nullable disable
        private int GetReferenceSize(int[] rowCounts, TableIndex index) => rowCounts[(int)index] >= 65536 || this.IsMinimalDelta ? 4 : 2;

        private void InitializeTableReaders(
          MemoryBlock metadataTablesMemoryBlock,
          HeapSizes heapSizes,
          int[] rowCounts,
          int[] externalRowCountsOpt)
        {
            this.TableRowCounts = rowCounts;
            int fieldRefSize = this.GetReferenceSize(rowCounts, TableIndex.FieldPtr) > 2 ? 4 : this.GetReferenceSize(rowCounts, TableIndex.Field);
            int methodRefSize = this.GetReferenceSize(rowCounts, TableIndex.MethodPtr) > 2 ? 4 : this.GetReferenceSize(rowCounts, TableIndex.MethodDef);
            int paramRefSize = this.GetReferenceSize(rowCounts, TableIndex.ParamPtr) > 2 ? 4 : this.GetReferenceSize(rowCounts, TableIndex.Param);
            int eventRefSize = this.GetReferenceSize(rowCounts, TableIndex.EventPtr) > 2 ? 4 : this.GetReferenceSize(rowCounts, TableIndex.Event);
            int propertyRefSize = this.GetReferenceSize(rowCounts, TableIndex.PropertyPtr) > 2 ? 4 : this.GetReferenceSize(rowCounts, TableIndex.Property);
            int codedTokenSize1 = this.ComputeCodedTokenSize(16384, rowCounts, TableMask.TypeRef | TableMask.TypeDef | TableMask.TypeSpec);
            int codedTokenSize2 = this.ComputeCodedTokenSize(16384, rowCounts, TableMask.Field | TableMask.Param | TableMask.Property);
            int codedTokenSize3 = this.ComputeCodedTokenSize(2048, rowCounts, TableMask.Module | TableMask.TypeRef | TableMask.TypeDef | TableMask.Field | TableMask.MethodDef | TableMask.Param | TableMask.InterfaceImpl | TableMask.MemberRef | TableMask.DeclSecurity | TableMask.StandAloneSig | TableMask.Event | TableMask.Property | TableMask.ModuleRef | TableMask.TypeSpec | TableMask.Assembly | TableMask.AssemblyRef | TableMask.File | TableMask.ExportedType | TableMask.ManifestResource | TableMask.GenericParam | TableMask.MethodSpec | TableMask.GenericParamConstraint);
            int codedTokenSize4 = this.ComputeCodedTokenSize(32768, rowCounts, TableMask.Field | TableMask.Param);
            int codedTokenSize5 = this.ComputeCodedTokenSize(16384, rowCounts, TableMask.TypeDef | TableMask.MethodDef | TableMask.Assembly);
            int codedTokenSize6 = this.ComputeCodedTokenSize(8192, rowCounts, TableMask.TypeRef | TableMask.TypeDef | TableMask.MethodDef | TableMask.ModuleRef | TableMask.TypeSpec);
            int codedTokenSize7 = this.ComputeCodedTokenSize(32768, rowCounts, TableMask.Event | TableMask.Property);
            int codedTokenSize8 = this.ComputeCodedTokenSize(32768, rowCounts, TableMask.MethodDef | TableMask.MemberRef);
            int codedTokenSize9 = this.ComputeCodedTokenSize(32768, rowCounts, TableMask.Field | TableMask.MethodDef);
            int codedTokenSize10 = this.ComputeCodedTokenSize(16384, rowCounts, TableMask.AssemblyRef | TableMask.File | TableMask.ExportedType);
            int codedTokenSize11 = this.ComputeCodedTokenSize(8192, rowCounts, TableMask.MethodDef | TableMask.MemberRef);
            int codedTokenSize12 = this.ComputeCodedTokenSize(16384, rowCounts, TableMask.Module | TableMask.TypeRef | TableMask.ModuleRef | TableMask.AssemblyRef);
            int codedTokenSize13 = this.ComputeCodedTokenSize(32768, rowCounts, TableMask.TypeDef | TableMask.MethodDef);
            int stringHeapRefSize = (heapSizes & HeapSizes.StringHeapLarge) == HeapSizes.StringHeapLarge ? 4 : 2;
            int guidHeapRefSize = (heapSizes & HeapSizes.GuidHeapLarge) == HeapSizes.GuidHeapLarge ? 4 : 2;
            int blobHeapRefSize = (heapSizes & HeapSizes.BlobHeapLarge) == HeapSizes.BlobHeapLarge ? 4 : 2;
            int containingBlockOffset1 = 0;
            this.ModuleTable = new ModuleTableReader(rowCounts[0], stringHeapRefSize, guidHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset1);
            int containingBlockOffset2 = containingBlockOffset1 + this.ModuleTable.Block.Length;
            this.TypeRefTable = new TypeRefTableReader(rowCounts[1], codedTokenSize12, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset2);
            int containingBlockOffset3 = containingBlockOffset2 + this.TypeRefTable.Block.Length;
            this.TypeDefTable = new TypeDefTableReader(rowCounts[2], fieldRefSize, methodRefSize, codedTokenSize1, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset3);
            int containingBlockOffset4 = containingBlockOffset3 + this.TypeDefTable.Block.Length;
            this.FieldPtrTable = new FieldPtrTableReader(rowCounts[3], this.GetReferenceSize(rowCounts, TableIndex.Field), metadataTablesMemoryBlock, containingBlockOffset4);
            int containingBlockOffset5 = containingBlockOffset4 + this.FieldPtrTable.Block.Length;
            this.FieldTable = new FieldTableReader(rowCounts[4], stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset5);
            int containingBlockOffset6 = containingBlockOffset5 + this.FieldTable.Block.Length;
            this.MethodPtrTable = new MethodPtrTableReader(rowCounts[5], this.GetReferenceSize(rowCounts, TableIndex.MethodDef), metadataTablesMemoryBlock, containingBlockOffset6);
            int containingBlockOffset7 = containingBlockOffset6 + this.MethodPtrTable.Block.Length;
            this.MethodDefTable = new MethodTableReader(rowCounts[6], paramRefSize, stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset7);
            int containingBlockOffset8 = containingBlockOffset7 + this.MethodDefTable.Block.Length;
            this.ParamPtrTable = new ParamPtrTableReader(rowCounts[7], this.GetReferenceSize(rowCounts, TableIndex.Param), metadataTablesMemoryBlock, containingBlockOffset8);
            int containingBlockOffset9 = containingBlockOffset8 + this.ParamPtrTable.Block.Length;
            this.ParamTable = new ParamTableReader(rowCounts[8], stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset9);
            int containingBlockOffset10 = containingBlockOffset9 + this.ParamTable.Block.Length;
            this.InterfaceImplTable = new InterfaceImplTableReader(rowCounts[9], this.IsDeclaredSorted(TableMask.InterfaceImpl), this.GetReferenceSize(rowCounts, TableIndex.TypeDef), codedTokenSize1, metadataTablesMemoryBlock, containingBlockOffset10);
            int containingBlockOffset11 = containingBlockOffset10 + this.InterfaceImplTable.Block.Length;
            this.MemberRefTable = new MemberRefTableReader(rowCounts[10], codedTokenSize6, stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset11);
            int containingBlockOffset12 = containingBlockOffset11 + this.MemberRefTable.Block.Length;
            this.ConstantTable = new ConstantTableReader(rowCounts[11], this.IsDeclaredSorted(TableMask.Constant), codedTokenSize2, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset12);
            int containingBlockOffset13 = containingBlockOffset12 + this.ConstantTable.Block.Length;
            this.CustomAttributeTable = new CustomAttributeTableReader(rowCounts[12], this.IsDeclaredSorted(TableMask.CustomAttribute), codedTokenSize3, codedTokenSize11, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset13);
            int containingBlockOffset14 = containingBlockOffset13 + this.CustomAttributeTable.Block.Length;
            this.FieldMarshalTable = new FieldMarshalTableReader(rowCounts[13], this.IsDeclaredSorted(TableMask.FieldMarshal), codedTokenSize4, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset14);
            int containingBlockOffset15 = containingBlockOffset14 + this.FieldMarshalTable.Block.Length;
            this.DeclSecurityTable = new DeclSecurityTableReader(rowCounts[14], this.IsDeclaredSorted(TableMask.DeclSecurity), codedTokenSize5, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset15);
            int containingBlockOffset16 = containingBlockOffset15 + this.DeclSecurityTable.Block.Length;
            this.ClassLayoutTable = new ClassLayoutTableReader(rowCounts[15], this.IsDeclaredSorted(TableMask.ClassLayout), this.GetReferenceSize(rowCounts, TableIndex.TypeDef), metadataTablesMemoryBlock, containingBlockOffset16);
            int containingBlockOffset17 = containingBlockOffset16 + this.ClassLayoutTable.Block.Length;
            this.FieldLayoutTable = new FieldLayoutTableReader(rowCounts[16], this.IsDeclaredSorted(TableMask.FieldLayout), this.GetReferenceSize(rowCounts, TableIndex.Field), metadataTablesMemoryBlock, containingBlockOffset17);
            int containingBlockOffset18 = containingBlockOffset17 + this.FieldLayoutTable.Block.Length;
            this.StandAloneSigTable = new StandAloneSigTableReader(rowCounts[17], blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset18);
            int containingBlockOffset19 = containingBlockOffset18 + this.StandAloneSigTable.Block.Length;
            this.EventMapTable = new EventMapTableReader(rowCounts[18], this.GetReferenceSize(rowCounts, TableIndex.TypeDef), eventRefSize, metadataTablesMemoryBlock, containingBlockOffset19);
            int containingBlockOffset20 = containingBlockOffset19 + this.EventMapTable.Block.Length;
            this.EventPtrTable = new EventPtrTableReader(rowCounts[19], this.GetReferenceSize(rowCounts, TableIndex.Event), metadataTablesMemoryBlock, containingBlockOffset20);
            int containingBlockOffset21 = containingBlockOffset20 + this.EventPtrTable.Block.Length;
            this.EventTable = new EventTableReader(rowCounts[20], codedTokenSize1, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset21);
            int containingBlockOffset22 = containingBlockOffset21 + this.EventTable.Block.Length;
            this.PropertyMapTable = new PropertyMapTableReader(rowCounts[21], this.GetReferenceSize(rowCounts, TableIndex.TypeDef), propertyRefSize, metadataTablesMemoryBlock, containingBlockOffset22);
            int containingBlockOffset23 = containingBlockOffset22 + this.PropertyMapTable.Block.Length;
            this.PropertyPtrTable = new PropertyPtrTableReader(rowCounts[22], this.GetReferenceSize(rowCounts, TableIndex.Property), metadataTablesMemoryBlock, containingBlockOffset23);
            int containingBlockOffset24 = containingBlockOffset23 + this.PropertyPtrTable.Block.Length;
            this.PropertyTable = new PropertyTableReader(rowCounts[23], stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset24);
            int containingBlockOffset25 = containingBlockOffset24 + this.PropertyTable.Block.Length;
            this.MethodSemanticsTable = new MethodSemanticsTableReader(rowCounts[24], this.IsDeclaredSorted(TableMask.MethodSemantics), this.GetReferenceSize(rowCounts, TableIndex.MethodDef), codedTokenSize7, metadataTablesMemoryBlock, containingBlockOffset25);
            int containingBlockOffset26 = containingBlockOffset25 + this.MethodSemanticsTable.Block.Length;
            this.MethodImplTable = new MethodImplTableReader(rowCounts[25], this.IsDeclaredSorted(TableMask.MethodImpl), this.GetReferenceSize(rowCounts, TableIndex.TypeDef), codedTokenSize8, metadataTablesMemoryBlock, containingBlockOffset26);
            int containingBlockOffset27 = containingBlockOffset26 + this.MethodImplTable.Block.Length;
            this.ModuleRefTable = new ModuleRefTableReader(rowCounts[26], stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset27);
            int containingBlockOffset28 = containingBlockOffset27 + this.ModuleRefTable.Block.Length;
            this.TypeSpecTable = new TypeSpecTableReader(rowCounts[27], blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset28);
            int containingBlockOffset29 = containingBlockOffset28 + this.TypeSpecTable.Block.Length;
            this.ImplMapTable = new ImplMapTableReader(rowCounts[28], this.IsDeclaredSorted(TableMask.ImplMap), this.GetReferenceSize(rowCounts, TableIndex.ModuleRef), codedTokenSize9, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset29);
            int containingBlockOffset30 = containingBlockOffset29 + this.ImplMapTable.Block.Length;
            this.FieldRvaTable = new FieldRVATableReader(rowCounts[29], this.IsDeclaredSorted(TableMask.FieldRva), this.GetReferenceSize(rowCounts, TableIndex.Field), metadataTablesMemoryBlock, containingBlockOffset30);
            int containingBlockOffset31 = containingBlockOffset30 + this.FieldRvaTable.Block.Length;
            this.EncLogTable = new EnCLogTableReader(rowCounts[30], metadataTablesMemoryBlock, containingBlockOffset31, this._metadataStreamKind);
            int containingBlockOffset32 = containingBlockOffset31 + this.EncLogTable.Block.Length;
            this.EncMapTable = new EnCMapTableReader(rowCounts[31], metadataTablesMemoryBlock, containingBlockOffset32);
            int containingBlockOffset33 = containingBlockOffset32 + this.EncMapTable.Block.Length;
            this.AssemblyTable = new AssemblyTableReader(rowCounts[32], stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset33);
            int containingBlockOffset34 = containingBlockOffset33 + this.AssemblyTable.Block.Length;
            this.AssemblyProcessorTable = new AssemblyProcessorTableReader(rowCounts[33], metadataTablesMemoryBlock, containingBlockOffset34);
            int containingBlockOffset35 = containingBlockOffset34 + this.AssemblyProcessorTable.Block.Length;
            this.AssemblyOSTable = new AssemblyOSTableReader(rowCounts[34], metadataTablesMemoryBlock, containingBlockOffset35);
            int containingBlockOffset36 = containingBlockOffset35 + this.AssemblyOSTable.Block.Length;
            this.AssemblyRefTable = new AssemblyRefTableReader(rowCounts[35], stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset36, this._metadataKind);
            int containingBlockOffset37 = containingBlockOffset36 + this.AssemblyRefTable.Block.Length;
            this.AssemblyRefProcessorTable = new AssemblyRefProcessorTableReader(rowCounts[36], this.GetReferenceSize(rowCounts, TableIndex.AssemblyRef), metadataTablesMemoryBlock, containingBlockOffset37);
            int containingBlockOffset38 = containingBlockOffset37 + this.AssemblyRefProcessorTable.Block.Length;
            this.AssemblyRefOSTable = new AssemblyRefOSTableReader(rowCounts[37], this.GetReferenceSize(rowCounts, TableIndex.AssemblyRef), metadataTablesMemoryBlock, containingBlockOffset38);
            int containingBlockOffset39 = containingBlockOffset38 + this.AssemblyRefOSTable.Block.Length;
            this.FileTable = new FileTableReader(rowCounts[38], stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset39);
            int containingBlockOffset40 = containingBlockOffset39 + this.FileTable.Block.Length;
            this.ExportedTypeTable = new ExportedTypeTableReader(rowCounts[39], codedTokenSize10, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset40);
            int containingBlockOffset41 = containingBlockOffset40 + this.ExportedTypeTable.Block.Length;
            this.ManifestResourceTable = new ManifestResourceTableReader(rowCounts[40], codedTokenSize10, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset41);
            int containingBlockOffset42 = containingBlockOffset41 + this.ManifestResourceTable.Block.Length;
            this.NestedClassTable = new NestedClassTableReader(rowCounts[41], this.IsDeclaredSorted(TableMask.NestedClass), this.GetReferenceSize(rowCounts, TableIndex.TypeDef), metadataTablesMemoryBlock, containingBlockOffset42);
            int containingBlockOffset43 = containingBlockOffset42 + this.NestedClassTable.Block.Length;
            this.GenericParamTable = new GenericParamTableReader(rowCounts[42], this.IsDeclaredSorted(TableMask.GenericParam), codedTokenSize13, stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset43);
            int containingBlockOffset44 = containingBlockOffset43 + this.GenericParamTable.Block.Length;
            this.MethodSpecTable = new MethodSpecTableReader(rowCounts[43], codedTokenSize8, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset44);
            int containingBlockOffset45 = containingBlockOffset44 + this.MethodSpecTable.Block.Length;
            this.GenericParamConstraintTable = new GenericParamConstraintTableReader(rowCounts[44], this.IsDeclaredSorted(TableMask.GenericParamConstraint), this.GetReferenceSize(rowCounts, TableIndex.GenericParam), codedTokenSize1, metadataTablesMemoryBlock, containingBlockOffset45);
            int containingBlockOffset46 = containingBlockOffset45 + this.GenericParamConstraintTable.Block.Length;
            int[] rowCounts1 = externalRowCountsOpt != null ? MetadataReader.CombineRowCounts(rowCounts, externalRowCountsOpt, TableIndex.Document) : rowCounts;
            int referenceSize = this.GetReferenceSize(rowCounts1, TableIndex.MethodDef);
            int codedTokenSize14 = this.ComputeCodedTokenSize(2048, rowCounts1, TableMask.Module | TableMask.TypeRef | TableMask.TypeDef | TableMask.Field | TableMask.MethodDef | TableMask.Param | TableMask.InterfaceImpl | TableMask.MemberRef | TableMask.DeclSecurity | TableMask.StandAloneSig | TableMask.Event | TableMask.Property | TableMask.ModuleRef | TableMask.TypeSpec | TableMask.Assembly | TableMask.AssemblyRef | TableMask.File | TableMask.ExportedType | TableMask.ManifestResource | TableMask.GenericParam | TableMask.MethodSpec | TableMask.GenericParamConstraint | TableMask.Document | TableMask.LocalScope | TableMask.LocalVariable | TableMask.LocalConstant | TableMask.ImportScope);
            this.DocumentTable = new DocumentTableReader(rowCounts[48], guidHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset46);
            int containingBlockOffset47 = containingBlockOffset46 + this.DocumentTable.Block.Length;
            this.MethodDebugInformationTable = new MethodDebugInformationTableReader(rowCounts[49], this.GetReferenceSize(rowCounts, TableIndex.Document), blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset47);
            int containingBlockOffset48 = containingBlockOffset47 + this.MethodDebugInformationTable.Block.Length;
            this.LocalScopeTable = new LocalScopeTableReader(rowCounts[50], this.IsDeclaredSorted(TableMask.LocalScope), referenceSize, this.GetReferenceSize(rowCounts, TableIndex.ImportScope), this.GetReferenceSize(rowCounts, TableIndex.LocalVariable), this.GetReferenceSize(rowCounts, TableIndex.LocalConstant), metadataTablesMemoryBlock, containingBlockOffset48);
            int containingBlockOffset49 = containingBlockOffset48 + this.LocalScopeTable.Block.Length;
            this.LocalVariableTable = new LocalVariableTableReader(rowCounts[51], stringHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset49);
            int containingBlockOffset50 = containingBlockOffset49 + this.LocalVariableTable.Block.Length;
            this.LocalConstantTable = new LocalConstantTableReader(rowCounts[52], stringHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset50);
            int containingBlockOffset51 = containingBlockOffset50 + this.LocalConstantTable.Block.Length;
            this.ImportScopeTable = new ImportScopeTableReader(rowCounts[53], this.GetReferenceSize(rowCounts, TableIndex.ImportScope), blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset51);
            int containingBlockOffset52 = containingBlockOffset51 + this.ImportScopeTable.Block.Length;
            this.StateMachineMethodTable = new StateMachineMethodTableReader(rowCounts[54], this.IsDeclaredSorted(TableMask.StateMachineMethod), referenceSize, metadataTablesMemoryBlock, containingBlockOffset52);
            int containingBlockOffset53 = containingBlockOffset52 + this.StateMachineMethodTable.Block.Length;
            this.CustomDebugInformationTable = new CustomDebugInformationTableReader(rowCounts[55], this.IsDeclaredSorted(TableMask.CustomDebugInformation), codedTokenSize14, guidHeapRefSize, blobHeapRefSize, metadataTablesMemoryBlock, containingBlockOffset53);
            if (containingBlockOffset53 + this.CustomDebugInformationTable.Block.Length > metadataTablesMemoryBlock.Length)
                throw new BadImageFormatException(SR.MetadataTablesTooSmall);
        }

        private static int[] CombineRowCounts(
          int[] local,
          int[] external,
          TableIndex firstLocalTableIndex)
        {
            int[] numArray = new int[local.Length];
            for (int index = 0; (TableIndex)index < firstLocalTableIndex; ++index)
                numArray[index] = external[index];
            for (int index = (int)firstLocalTableIndex; index < numArray.Length; ++index)
                numArray[index] = local[index];
            return numArray;
        }

        private int ComputeCodedTokenSize(
          int largeRowSize,
          int[] rowCounts,
          TableMask tablesReferenced)
        {
            if (this.IsMinimalDelta)
                return 4;
            bool flag = true;
            ulong num = (ulong)tablesReferenced;
            for (int index = 0; index < MetadataTokens.TableCount; ++index)
            {
                if (((long)num & 1L) != 0L)
                    flag = flag && rowCounts[index] < largeRowSize;
                num >>= 1;
            }
            return !flag ? 4 : 2;
        }

        private bool IsDeclaredSorted(TableMask index) => (this._sortedTables & index) > (TableMask)0;

        internal bool UseFieldPtrTable => this.FieldPtrTable.NumberOfRows > 0;

        internal bool UseMethodPtrTable => this.MethodPtrTable.NumberOfRows > 0;

        internal bool UseParamPtrTable => this.ParamPtrTable.NumberOfRows > 0;

        internal bool UseEventPtrTable => this.EventPtrTable.NumberOfRows > 0;

        internal bool UsePropertyPtrTable => this.PropertyPtrTable.NumberOfRows > 0;


#nullable enable
        internal void GetFieldRange(
          TypeDefinitionHandle typeDef,
          out int firstFieldRowId,
          out int lastFieldRowId)
        {
            int rowId = typeDef.RowId;
            firstFieldRowId = this.TypeDefTable.GetFieldStart(rowId);
            if (firstFieldRowId == 0)
            {
                firstFieldRowId = 1;
                lastFieldRowId = 0;
            }
            else if (rowId == this.TypeDefTable.NumberOfRows)
                lastFieldRowId = this.UseFieldPtrTable ? this.FieldPtrTable.NumberOfRows : this.FieldTable.NumberOfRows;
            else
                lastFieldRowId = this.TypeDefTable.GetFieldStart(rowId + 1) - 1;
        }

        internal void GetMethodRange(
          TypeDefinitionHandle typeDef,
          out int firstMethodRowId,
          out int lastMethodRowId)
        {
            int rowId = typeDef.RowId;
            firstMethodRowId = this.TypeDefTable.GetMethodStart(rowId);
            if (firstMethodRowId == 0)
            {
                firstMethodRowId = 1;
                lastMethodRowId = 0;
            }
            else if (rowId == this.TypeDefTable.NumberOfRows)
                lastMethodRowId = this.UseMethodPtrTable ? this.MethodPtrTable.NumberOfRows : this.MethodDefTable.NumberOfRows;
            else
                lastMethodRowId = this.TypeDefTable.GetMethodStart(rowId + 1) - 1;
        }

        internal void GetEventRange(
          TypeDefinitionHandle typeDef,
          out int firstEventRowId,
          out int lastEventRowId)
        {
            int eventMapRowIdFor = this.EventMapTable.FindEventMapRowIdFor(typeDef);
            if (eventMapRowIdFor == 0)
            {
                firstEventRowId = 1;
                lastEventRowId = 0;
            }
            else
            {
                firstEventRowId = this.EventMapTable.GetEventListStartFor(eventMapRowIdFor);
                if (eventMapRowIdFor == this.EventMapTable.NumberOfRows)
                    lastEventRowId = this.UseEventPtrTable ? this.EventPtrTable.NumberOfRows : this.EventTable.NumberOfRows;
                else
                    lastEventRowId = this.EventMapTable.GetEventListStartFor(eventMapRowIdFor + 1) - 1;
            }
        }

        internal void GetPropertyRange(
          TypeDefinitionHandle typeDef,
          out int firstPropertyRowId,
          out int lastPropertyRowId)
        {
            int propertyMapRowIdFor = this.PropertyMapTable.FindPropertyMapRowIdFor(typeDef);
            if (propertyMapRowIdFor == 0)
            {
                firstPropertyRowId = 1;
                lastPropertyRowId = 0;
            }
            else
            {
                firstPropertyRowId = this.PropertyMapTable.GetPropertyListStartFor(propertyMapRowIdFor);
                if (propertyMapRowIdFor == this.PropertyMapTable.NumberOfRows)
                    lastPropertyRowId = this.UsePropertyPtrTable ? this.PropertyPtrTable.NumberOfRows : this.PropertyTable.NumberOfRows;
                else
                    lastPropertyRowId = this.PropertyMapTable.GetPropertyListStartFor(propertyMapRowIdFor + 1) - 1;
            }
        }

        internal void GetParameterRange(
          MethodDefinitionHandle methodDef,
          out int firstParamRowId,
          out int lastParamRowId)
        {
            int rowId = methodDef.RowId;
            firstParamRowId = this.MethodDefTable.GetParamStart(rowId);
            if (firstParamRowId == 0)
            {
                firstParamRowId = 1;
                lastParamRowId = 0;
            }
            else if (rowId == this.MethodDefTable.NumberOfRows)
                lastParamRowId = this.UseParamPtrTable ? this.ParamPtrTable.NumberOfRows : this.ParamTable.NumberOfRows;
            else
                lastParamRowId = this.MethodDefTable.GetParamStart(rowId + 1) - 1;
        }

        internal void GetLocalVariableRange(
          LocalScopeHandle scope,
          out int firstVariableRowId,
          out int lastVariableRowId)
        {
            int rowId = scope.RowId;
            firstVariableRowId = this.LocalScopeTable.GetVariableStart(rowId);
            if (firstVariableRowId == 0)
            {
                firstVariableRowId = 1;
                lastVariableRowId = 0;
            }
            else if (rowId == this.LocalScopeTable.NumberOfRows)
                lastVariableRowId = this.LocalVariableTable.NumberOfRows;
            else
                lastVariableRowId = this.LocalScopeTable.GetVariableStart(rowId + 1) - 1;
        }

        internal void GetLocalConstantRange(
          LocalScopeHandle scope,
          out int firstConstantRowId,
          out int lastConstantRowId)
        {
            int rowId = scope.RowId;
            firstConstantRowId = this.LocalScopeTable.GetConstantStart(rowId);
            if (firstConstantRowId == 0)
            {
                firstConstantRowId = 1;
                lastConstantRowId = 0;
            }
            else if (rowId == this.LocalScopeTable.NumberOfRows)
                lastConstantRowId = this.LocalConstantTable.NumberOfRows;
            else
                lastConstantRowId = this.LocalScopeTable.GetConstantStart(rowId + 1) - 1;
        }

        /// <summary>Pointer to the underlying data.</summary>
        public unsafe byte* MetadataPointer => this.Block.Pointer;

        /// <summary>Length of the underlying data.</summary>
        public int MetadataLength => this.Block.Length;

        /// <summary>Options passed to the constructor.</summary>
        public MetadataReaderOptions Options => this._options;

        /// <summary>Version string read from metadata header.</summary>
        public string MetadataVersion => this._versionString;

        /// <summary>
        /// Information decoded from #Pdb stream, or null if the stream is not present.
        /// </summary>
        public DebugMetadataHeader? DebugMetadataHeader => this._debugMetadataHeader;

        /// <summary>
        /// The kind of the metadata (plain ECMA335, WinMD, etc.).
        /// </summary>
        public MetadataKind MetadataKind => this._metadataKind;

        /// <summary>Comparer used to compare strings stored in metadata.</summary>
        public MetadataStringComparer StringComparer => new MetadataStringComparer(this);

        /// <summary>
        /// The decoder used by the reader to produce <see cref="T:System.String" /> instances from UTF8 encoded byte sequences.
        /// </summary>
        public MetadataStringDecoder UTF8Decoder { get; }

        /// <summary>Returns true if the metadata represent an assembly.</summary>
        public bool IsAssembly => this.AssemblyTable.NumberOfRows == 1;

        public AssemblyReferenceHandleCollection AssemblyReferences => new AssemblyReferenceHandleCollection(this);

        public TypeDefinitionHandleCollection TypeDefinitions => new TypeDefinitionHandleCollection(this.TypeDefTable.NumberOfRows);

        public TypeReferenceHandleCollection TypeReferences => new TypeReferenceHandleCollection(this.TypeRefTable.NumberOfRows);

        public CustomAttributeHandleCollection CustomAttributes => new CustomAttributeHandleCollection(this);

        public DeclarativeSecurityAttributeHandleCollection DeclarativeSecurityAttributes => new DeclarativeSecurityAttributeHandleCollection(this);

        public MemberReferenceHandleCollection MemberReferences => new MemberReferenceHandleCollection(this.MemberRefTable.NumberOfRows);

        public ManifestResourceHandleCollection ManifestResources => new ManifestResourceHandleCollection(this.ManifestResourceTable.NumberOfRows);

        public AssemblyFileHandleCollection AssemblyFiles => new AssemblyFileHandleCollection(this.FileTable.NumberOfRows);

        public ExportedTypeHandleCollection ExportedTypes => new ExportedTypeHandleCollection(this.ExportedTypeTable.NumberOfRows);

        public MethodDefinitionHandleCollection MethodDefinitions => new MethodDefinitionHandleCollection(this);

        public FieldDefinitionHandleCollection FieldDefinitions => new FieldDefinitionHandleCollection(this);

        public EventDefinitionHandleCollection EventDefinitions => new EventDefinitionHandleCollection(this);

        public PropertyDefinitionHandleCollection PropertyDefinitions => new PropertyDefinitionHandleCollection(this);

        public DocumentHandleCollection Documents => new DocumentHandleCollection(this);

        public MethodDebugInformationHandleCollection MethodDebugInformation => new MethodDebugInformationHandleCollection(this);

        public LocalScopeHandleCollection LocalScopes => new LocalScopeHandleCollection(this, 0);

        public LocalVariableHandleCollection LocalVariables => new LocalVariableHandleCollection(this, new LocalScopeHandle());

        public LocalConstantHandleCollection LocalConstants => new LocalConstantHandleCollection(this, new LocalScopeHandle());

        public ImportScopeCollection ImportScopes => new ImportScopeCollection(this);

        public CustomDebugInformationHandleCollection CustomDebugInformation => new CustomDebugInformationHandleCollection(this);

        public AssemblyDefinition GetAssemblyDefinition()
        {
            if (!this.IsAssembly)
                throw new InvalidOperationException(SR.MetadataImageDoesNotRepresentAnAssembly);
            return new AssemblyDefinition(this);
        }

        public string GetString(StringHandle handle) => this.StringHeap.GetString(handle, this.UTF8Decoder);

        public string GetString(NamespaceDefinitionHandle handle) => handle.HasFullName ? this.StringHeap.GetString(handle.GetFullName(), this.UTF8Decoder) : this.NamespaceCache.GetFullName(handle);

        public byte[] GetBlobBytes(BlobHandle handle) => this.BlobHeap.GetBytes(handle);

        public ImmutableArray<byte> GetBlobContent(BlobHandle handle)
        {
            byte[] blobBytes = this.GetBlobBytes(handle);
            return ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref blobBytes);
        }

        public BlobReader GetBlobReader(BlobHandle handle) => this.BlobHeap.GetBlobReader(handle);

        public BlobReader GetBlobReader(StringHandle handle) => this.StringHeap.GetBlobReader(handle);

        public string GetUserString(UserStringHandle handle) => this.UserStringHeap.GetString(handle);

        public Guid GetGuid(GuidHandle handle) => this.GuidHeap.GetGuid(handle);

        public ModuleDefinition GetModuleDefinition()
        {
            if (this._debugMetadataHeader != null)
                throw new InvalidOperationException(SR.StandaloneDebugMetadataImageDoesNotContainModuleTable);
            return new ModuleDefinition(this);
        }

        public AssemblyReference GetAssemblyReference(AssemblyReferenceHandle handle) => new AssemblyReference(this, handle.Value);

        public TypeDefinition GetTypeDefinition(TypeDefinitionHandle handle) => new TypeDefinition(this, this.GetTypeDefTreatmentAndRowId(handle));

        public NamespaceDefinition GetNamespaceDefinitionRoot() => new NamespaceDefinition(this.NamespaceCache.GetRootNamespace());

        public NamespaceDefinition GetNamespaceDefinition(NamespaceDefinitionHandle handle) => new NamespaceDefinition(this.NamespaceCache.GetNamespaceData(handle));

        private uint GetTypeDefTreatmentAndRowId(TypeDefinitionHandle handle) => this._metadataKind == MetadataKind.Ecma335 ? (uint)handle.RowId : this.CalculateTypeDefTreatmentAndRowId(handle);

        public TypeReference GetTypeReference(TypeReferenceHandle handle) => new TypeReference(this, this.GetTypeRefTreatmentAndRowId(handle));

        private uint GetTypeRefTreatmentAndRowId(TypeReferenceHandle handle) => this._metadataKind == MetadataKind.Ecma335 ? (uint)handle.RowId : this.CalculateTypeRefTreatmentAndRowId(handle);

        public ExportedType GetExportedType(ExportedTypeHandle handle) => new ExportedType(this, handle.RowId);

        public CustomAttributeHandleCollection GetCustomAttributes(EntityHandle handle) => new CustomAttributeHandleCollection(this, handle);

        public CustomAttribute GetCustomAttribute(CustomAttributeHandle handle) => new CustomAttribute(this, this.GetCustomAttributeTreatmentAndRowId(handle));

        private uint GetCustomAttributeTreatmentAndRowId(CustomAttributeHandle handle) => this._metadataKind == MetadataKind.Ecma335 ? (uint)handle.RowId : MetadataReader.TreatmentAndRowId((byte)1, handle.RowId);

        public DeclarativeSecurityAttribute GetDeclarativeSecurityAttribute(
          DeclarativeSecurityAttributeHandle handle)
        {
            return new DeclarativeSecurityAttribute(this, handle.RowId);
        }

        public Constant GetConstant(ConstantHandle handle) => new Constant(this, handle.RowId);

        public MethodDefinition GetMethodDefinition(MethodDefinitionHandle handle) => new MethodDefinition(this, this.GetMethodDefTreatmentAndRowId(handle));

        private uint GetMethodDefTreatmentAndRowId(MethodDefinitionHandle handle) => this._metadataKind == MetadataKind.Ecma335 ? (uint)handle.RowId : this.CalculateMethodDefTreatmentAndRowId(handle);

        public FieldDefinition GetFieldDefinition(FieldDefinitionHandle handle) => new FieldDefinition(this, this.GetFieldDefTreatmentAndRowId(handle));

        private uint GetFieldDefTreatmentAndRowId(FieldDefinitionHandle handle) => this._metadataKind == MetadataKind.Ecma335 ? (uint)handle.RowId : this.CalculateFieldDefTreatmentAndRowId(handle);

        public PropertyDefinition GetPropertyDefinition(PropertyDefinitionHandle handle) => new PropertyDefinition(this, handle);

        public EventDefinition GetEventDefinition(EventDefinitionHandle handle) => new EventDefinition(this, handle);

        public MethodImplementation GetMethodImplementation(MethodImplementationHandle handle) => new MethodImplementation(this, handle);

        public MemberReference GetMemberReference(MemberReferenceHandle handle) => new MemberReference(this, this.GetMemberRefTreatmentAndRowId(handle));

        private uint GetMemberRefTreatmentAndRowId(MemberReferenceHandle handle) => this._metadataKind == MetadataKind.Ecma335 ? (uint)handle.RowId : this.CalculateMemberRefTreatmentAndRowId(handle);

        public MethodSpecification GetMethodSpecification(MethodSpecificationHandle handle) => new MethodSpecification(this, handle);

        public Parameter GetParameter(ParameterHandle handle) => new Parameter(this, handle);

        public GenericParameter GetGenericParameter(GenericParameterHandle handle) => new GenericParameter(this, handle);

        public GenericParameterConstraint GetGenericParameterConstraint(
          GenericParameterConstraintHandle handle)
        {
            return new GenericParameterConstraint(this, handle);
        }

        public ManifestResource GetManifestResource(ManifestResourceHandle handle) => new ManifestResource(this, handle);

        public AssemblyFile GetAssemblyFile(AssemblyFileHandle handle) => new AssemblyFile(this, handle);

        public StandaloneSignature GetStandaloneSignature(StandaloneSignatureHandle handle) => new StandaloneSignature(this, handle);

        public TypeSpecification GetTypeSpecification(TypeSpecificationHandle handle) => new TypeSpecification(this, handle);

        public ModuleReference GetModuleReference(ModuleReferenceHandle handle) => new ModuleReference(this, handle);

        public InterfaceImplementation GetInterfaceImplementation(InterfaceImplementationHandle handle) => new InterfaceImplementation(this, handle);

        internal TypeDefinitionHandle GetDeclaringType(MethodDefinitionHandle methodDef) => this.TypeDefTable.FindTypeContainingMethod(!this.UseMethodPtrTable ? methodDef.RowId : this.MethodPtrTable.GetRowIdForMethodDefRow(methodDef.RowId), this.MethodDefTable.NumberOfRows);

        internal TypeDefinitionHandle GetDeclaringType(FieldDefinitionHandle fieldDef) => this.TypeDefTable.FindTypeContainingField(!this.UseFieldPtrTable ? fieldDef.RowId : this.FieldPtrTable.GetRowIdForFieldDefRow(fieldDef.RowId), this.FieldTable.NumberOfRows);

        public string GetString(DocumentNameBlobHandle handle) => this.BlobHeap.GetDocumentName(handle);

        public Document GetDocument(DocumentHandle handle) => new Document(this, handle);

        public System.Reflection.Metadata.MethodDebugInformation GetMethodDebugInformation(
          MethodDebugInformationHandle handle)
        {
            return new System.Reflection.Metadata.MethodDebugInformation(this, handle);
        }

        public System.Reflection.Metadata.MethodDebugInformation GetMethodDebugInformation(
          MethodDefinitionHandle handle)
        {
            return new System.Reflection.Metadata.MethodDebugInformation(this, MethodDebugInformationHandle.FromRowId(handle.RowId));
        }

        public LocalScope GetLocalScope(LocalScopeHandle handle) => new LocalScope(this, handle);

        public LocalVariable GetLocalVariable(LocalVariableHandle handle) => new LocalVariable(this, handle);

        public LocalConstant GetLocalConstant(LocalConstantHandle handle) => new LocalConstant(this, handle);

        public ImportScope GetImportScope(ImportScopeHandle handle) => new ImportScope(this, handle);

        public System.Reflection.Metadata.CustomDebugInformation GetCustomDebugInformation(
          CustomDebugInformationHandle handle)
        {
            return new System.Reflection.Metadata.CustomDebugInformation(this, handle);
        }

        public CustomDebugInformationHandleCollection GetCustomDebugInformation(EntityHandle handle) => new CustomDebugInformationHandleCollection(this, handle);

        public LocalScopeHandleCollection GetLocalScopes(MethodDefinitionHandle handle) => new LocalScopeHandleCollection(this, handle.RowId);

        public LocalScopeHandleCollection GetLocalScopes(MethodDebugInformationHandle handle) => new LocalScopeHandleCollection(this, handle.RowId);

        private void InitializeNestedTypesMap()
        {
            Dictionary<TypeDefinitionHandle, ImmutableArray<TypeDefinitionHandle>.Builder> dictionary1 = new Dictionary<TypeDefinitionHandle, ImmutableArray<TypeDefinitionHandle>.Builder>();
            int numberOfRows = this.NestedClassTable.NumberOfRows;
            ImmutableArray<TypeDefinitionHandle>.Builder builder = (ImmutableArray<TypeDefinitionHandle>.Builder)null;
            TypeDefinitionHandle definitionHandle = new TypeDefinitionHandle();
            for (int rowId = 1; rowId <= numberOfRows; ++rowId)
            {
                TypeDefinitionHandle enclosingClass = this.NestedClassTable.GetEnclosingClass(rowId);
                if (enclosingClass != definitionHandle)
                {
                    if (!dictionary1.TryGetValue(enclosingClass, out builder))
                    {
                        builder = ImmutableArray.CreateBuilder<TypeDefinitionHandle>();
                        dictionary1.Add(enclosingClass, builder);
                    }
                    definitionHandle = enclosingClass;
                }
                builder.Add(this.NestedClassTable.GetNestedClass(rowId));
            }
            Dictionary<TypeDefinitionHandle, ImmutableArray<TypeDefinitionHandle>> dictionary2 = new Dictionary<TypeDefinitionHandle, ImmutableArray<TypeDefinitionHandle>>();
            foreach (KeyValuePair<TypeDefinitionHandle, ImmutableArray<TypeDefinitionHandle>.Builder> keyValuePair in dictionary1)
                dictionary2.Add(keyValuePair.Key, keyValuePair.Value.ToImmutable());
            this._lazyNestedTypesMap = dictionary2;
        }

        /// <summary>
        /// Returns an array of types nested in the specified type.
        /// </summary>
        internal ImmutableArray<TypeDefinitionHandle> GetNestedTypes(TypeDefinitionHandle typeDef)
        {
            if (this._lazyNestedTypesMap == null)
                this.InitializeNestedTypesMap();
            ImmutableArray<TypeDefinitionHandle> immutableArray;
            return this._lazyNestedTypesMap.TryGetValue(typeDef, out immutableArray) ? immutableArray : ImmutableArray<TypeDefinitionHandle>.Empty;
        }

        private TypeDefTreatment GetWellKnownTypeDefinitionTreatment(TypeDefinitionHandle typeDef)
        {
            MetadataReader.InitializeProjectedTypes();
            StringHandle name = this.TypeDefTable.GetName(typeDef);
            int index = this.StringHeap.BinarySearchRaw(MetadataReader.s_projectedTypeNames, name);
            if (index < 0)
                return TypeDefTreatment.None;
            StringHandle rawHandle = this.TypeDefTable.GetNamespace(typeDef);
            if (this.StringHeap.EqualsRaw(rawHandle, StringHeap.GetVirtualString(MetadataReader.s_projectionInfos[index].ClrNamespace)))
                return MetadataReader.s_projectionInfos[index].Treatment;
            return this.StringHeap.EqualsRaw(rawHandle, MetadataReader.s_projectionInfos[index].WinRTNamespace) ? MetadataReader.s_projectionInfos[index].Treatment | TypeDefTreatment.MarkInternalFlag : TypeDefTreatment.None;
        }


#nullable disable
        private int GetProjectionIndexForTypeReference(
          TypeReferenceHandle typeRef,
          out bool isIDisposable)
        {
            MetadataReader.InitializeProjectedTypes();
            int forTypeReference = this.StringHeap.BinarySearchRaw(MetadataReader.s_projectedTypeNames, this.TypeRefTable.GetName(typeRef));
            if (forTypeReference >= 0 && this.StringHeap.EqualsRaw(this.TypeRefTable.GetNamespace(typeRef), MetadataReader.s_projectionInfos[forTypeReference].WinRTNamespace))
            {
                isIDisposable = MetadataReader.s_projectionInfos[forTypeReference].IsIDisposable;
                return forTypeReference;
            }
            isIDisposable = false;
            return -1;
        }

        internal static AssemblyReferenceHandle GetProjectedAssemblyRef(int projectionIndex) => AssemblyReferenceHandle.FromVirtualIndex(MetadataReader.s_projectionInfos[projectionIndex].AssemblyRef);

        internal static StringHandle GetProjectedName(int projectionIndex) => StringHandle.FromVirtualIndex(MetadataReader.s_projectionInfos[projectionIndex].ClrName);

        internal static StringHandle GetProjectedNamespace(int projectionIndex) => StringHandle.FromVirtualIndex(MetadataReader.s_projectionInfos[projectionIndex].ClrNamespace);

        internal static TypeRefSignatureTreatment GetProjectedSignatureTreatment(int projectionIndex) => MetadataReader.s_projectionInfos[projectionIndex].SignatureTreatment;

        private static void InitializeProjectedTypes()
        {
            if (MetadataReader.s_projectedTypeNames != null && MetadataReader.s_projectionInfos != null)
                return;
            AssemblyReferenceHandle.VirtualIndex clrAssembly1 = AssemblyReferenceHandle.VirtualIndex.System_Runtime_WindowsRuntime;
            AssemblyReferenceHandle.VirtualIndex clrAssembly2 = AssemblyReferenceHandle.VirtualIndex.System_Runtime;
            AssemblyReferenceHandle.VirtualIndex clrAssembly3 = AssemblyReferenceHandle.VirtualIndex.System_ObjectModel;
            AssemblyReferenceHandle.VirtualIndex clrAssembly4 = AssemblyReferenceHandle.VirtualIndex.System_Runtime_WindowsRuntime_UI_Xaml;
            AssemblyReferenceHandle.VirtualIndex clrAssembly5 = AssemblyReferenceHandle.VirtualIndex.System_Runtime_InteropServices_WindowsRuntime;
            AssemblyReferenceHandle.VirtualIndex clrAssembly6 = AssemblyReferenceHandle.VirtualIndex.System_Numerics_Vectors;
            string[] strArray1 = new string[50];
            MetadataReader.ProjectionInfo[] projectionInfoArray1 = new MetadataReader.ProjectionInfo[50];
            int num1 = 0;
            int num2 = 0;
            string[] strArray2 = strArray1;
            int index1 = num1;
            int num3 = index1 + 1;
            strArray2[index1] = "AttributeTargets";
            MetadataReader.ProjectionInfo[] projectionInfoArray2 = projectionInfoArray1;
            int index2 = num2;
            int num4 = index2 + 1;
            MetadataReader.ProjectionInfo projectionInfo1 = new MetadataReader.ProjectionInfo("Windows.Foundation.Metadata", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.AttributeTargets, clrAssembly2);
            projectionInfoArray2[index2] = projectionInfo1;
            string[] strArray3 = strArray1;
            int index3 = num3;
            int num5 = index3 + 1;
            strArray3[index3] = "AttributeUsageAttribute";
            MetadataReader.ProjectionInfo[] projectionInfoArray3 = projectionInfoArray1;
            int index4 = num4;
            int num6 = index4 + 1;
            MetadataReader.ProjectionInfo projectionInfo2 = new MetadataReader.ProjectionInfo("Windows.Foundation.Metadata", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.AttributeUsageAttribute, clrAssembly2, TypeDefTreatment.RedirectedToClrAttribute);
            projectionInfoArray3[index4] = projectionInfo2;
            string[] strArray4 = strArray1;
            int index5 = num5;
            int num7 = index5 + 1;
            strArray4[index5] = "Color";
            MetadataReader.ProjectionInfo[] projectionInfoArray4 = projectionInfoArray1;
            int index6 = num6;
            int num8 = index6 + 1;
            MetadataReader.ProjectionInfo projectionInfo3 = new MetadataReader.ProjectionInfo("Windows.UI", StringHandle.VirtualIndex.Windows_UI, StringHandle.VirtualIndex.Color, clrAssembly1);
            projectionInfoArray4[index6] = projectionInfo3;
            string[] strArray5 = strArray1;
            int index7 = num7;
            int num9 = index7 + 1;
            strArray5[index7] = "CornerRadius";
            MetadataReader.ProjectionInfo[] projectionInfoArray5 = projectionInfoArray1;
            int index8 = num8;
            int num10 = index8 + 1;
            MetadataReader.ProjectionInfo projectionInfo4 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml", StringHandle.VirtualIndex.Windows_UI_Xaml, StringHandle.VirtualIndex.CornerRadius, clrAssembly4);
            projectionInfoArray5[index8] = projectionInfo4;
            string[] strArray6 = strArray1;
            int index9 = num9;
            int num11 = index9 + 1;
            strArray6[index9] = "DateTime";
            MetadataReader.ProjectionInfo[] projectionInfoArray6 = projectionInfoArray1;
            int index10 = num10;
            int num12 = index10 + 1;
            MetadataReader.ProjectionInfo projectionInfo5 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.DateTimeOffset, clrAssembly2);
            projectionInfoArray6[index10] = projectionInfo5;
            string[] strArray7 = strArray1;
            int index11 = num11;
            int num13 = index11 + 1;
            strArray7[index11] = "Duration";
            MetadataReader.ProjectionInfo[] projectionInfoArray7 = projectionInfoArray1;
            int index12 = num12;
            int num14 = index12 + 1;
            MetadataReader.ProjectionInfo projectionInfo6 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml", StringHandle.VirtualIndex.Windows_UI_Xaml, StringHandle.VirtualIndex.Duration, clrAssembly4);
            projectionInfoArray7[index12] = projectionInfo6;
            string[] strArray8 = strArray1;
            int index13 = num13;
            int num15 = index13 + 1;
            strArray8[index13] = "DurationType";
            MetadataReader.ProjectionInfo[] projectionInfoArray8 = projectionInfoArray1;
            int index14 = num14;
            int num16 = index14 + 1;
            MetadataReader.ProjectionInfo projectionInfo7 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml", StringHandle.VirtualIndex.Windows_UI_Xaml, StringHandle.VirtualIndex.DurationType, clrAssembly4);
            projectionInfoArray8[index14] = projectionInfo7;
            string[] strArray9 = strArray1;
            int index15 = num15;
            int num17 = index15 + 1;
            strArray9[index15] = "EventHandler`1";
            MetadataReader.ProjectionInfo[] projectionInfoArray9 = projectionInfoArray1;
            int index16 = num16;
            int num18 = index16 + 1;
            MetadataReader.ProjectionInfo projectionInfo8 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.EventHandler1, clrAssembly2);
            projectionInfoArray9[index16] = projectionInfo8;
            string[] strArray10 = strArray1;
            int index17 = num17;
            int num19 = index17 + 1;
            strArray10[index17] = "EventRegistrationToken";
            MetadataReader.ProjectionInfo[] projectionInfoArray10 = projectionInfoArray1;
            int index18 = num18;
            int num20 = index18 + 1;
            MetadataReader.ProjectionInfo projectionInfo9 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System_Runtime_InteropServices_WindowsRuntime, StringHandle.VirtualIndex.EventRegistrationToken, clrAssembly5);
            projectionInfoArray10[index18] = projectionInfo9;
            string[] strArray11 = strArray1;
            int index19 = num19;
            int num21 = index19 + 1;
            strArray11[index19] = "GeneratorPosition";
            MetadataReader.ProjectionInfo[] projectionInfoArray11 = projectionInfoArray1;
            int index20 = num20;
            int num22 = index20 + 1;
            MetadataReader.ProjectionInfo projectionInfo10 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Controls.Primitives", StringHandle.VirtualIndex.Windows_UI_Xaml_Controls_Primitives, StringHandle.VirtualIndex.GeneratorPosition, clrAssembly4);
            projectionInfoArray11[index20] = projectionInfo10;
            string[] strArray12 = strArray1;
            int index21 = num21;
            int num23 = index21 + 1;
            strArray12[index21] = "GridLength";
            MetadataReader.ProjectionInfo[] projectionInfoArray12 = projectionInfoArray1;
            int index22 = num22;
            int num24 = index22 + 1;
            MetadataReader.ProjectionInfo projectionInfo11 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml", StringHandle.VirtualIndex.Windows_UI_Xaml, StringHandle.VirtualIndex.GridLength, clrAssembly4);
            projectionInfoArray12[index22] = projectionInfo11;
            string[] strArray13 = strArray1;
            int index23 = num23;
            int num25 = index23 + 1;
            strArray13[index23] = "GridUnitType";
            MetadataReader.ProjectionInfo[] projectionInfoArray13 = projectionInfoArray1;
            int index24 = num24;
            int num26 = index24 + 1;
            MetadataReader.ProjectionInfo projectionInfo12 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml", StringHandle.VirtualIndex.Windows_UI_Xaml, StringHandle.VirtualIndex.GridUnitType, clrAssembly4);
            projectionInfoArray13[index24] = projectionInfo12;
            string[] strArray14 = strArray1;
            int index25 = num25;
            int num27 = index25 + 1;
            strArray14[index25] = "HResult";
            MetadataReader.ProjectionInfo[] projectionInfoArray14 = projectionInfoArray1;
            int index26 = num26;
            int num28 = index26 + 1;
            MetadataReader.ProjectionInfo projectionInfo13 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.Exception, clrAssembly2, signatureTreatment: TypeRefSignatureTreatment.ProjectedToClass);
            projectionInfoArray14[index26] = projectionInfo13;
            string[] strArray15 = strArray1;
            int index27 = num27;
            int num29 = index27 + 1;
            strArray15[index27] = "IBindableIterable";
            MetadataReader.ProjectionInfo[] projectionInfoArray15 = projectionInfoArray1;
            int index28 = num28;
            int num30 = index28 + 1;
            MetadataReader.ProjectionInfo projectionInfo14 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System_Collections, StringHandle.VirtualIndex.IEnumerable, clrAssembly2);
            projectionInfoArray15[index28] = projectionInfo14;
            string[] strArray16 = strArray1;
            int index29 = num29;
            int num31 = index29 + 1;
            strArray16[index29] = "IBindableVector";
            MetadataReader.ProjectionInfo[] projectionInfoArray16 = projectionInfoArray1;
            int index30 = num30;
            int num32 = index30 + 1;
            MetadataReader.ProjectionInfo projectionInfo15 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System_Collections, StringHandle.VirtualIndex.IList, clrAssembly2);
            projectionInfoArray16[index30] = projectionInfo15;
            string[] strArray17 = strArray1;
            int index31 = num31;
            int num33 = index31 + 1;
            strArray17[index31] = "IClosable";
            MetadataReader.ProjectionInfo[] projectionInfoArray17 = projectionInfoArray1;
            int index32 = num32;
            int num34 = index32 + 1;
            MetadataReader.ProjectionInfo projectionInfo16 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.IDisposable, clrAssembly2, isIDisposable: true);
            projectionInfoArray17[index32] = projectionInfo16;
            string[] strArray18 = strArray1;
            int index33 = num33;
            int num35 = index33 + 1;
            strArray18[index33] = "ICommand";
            MetadataReader.ProjectionInfo[] projectionInfoArray18 = projectionInfoArray1;
            int index34 = num34;
            int num36 = index34 + 1;
            MetadataReader.ProjectionInfo projectionInfo17 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Input", StringHandle.VirtualIndex.System_Windows_Input, StringHandle.VirtualIndex.ICommand, clrAssembly3);
            projectionInfoArray18[index34] = projectionInfo17;
            string[] strArray19 = strArray1;
            int index35 = num35;
            int num37 = index35 + 1;
            strArray19[index35] = "IIterable`1";
            MetadataReader.ProjectionInfo[] projectionInfoArray19 = projectionInfoArray1;
            int index36 = num36;
            int num38 = index36 + 1;
            MetadataReader.ProjectionInfo projectionInfo18 = new MetadataReader.ProjectionInfo("Windows.Foundation.Collections", StringHandle.VirtualIndex.System_Collections_Generic, StringHandle.VirtualIndex.IEnumerable1, clrAssembly2);
            projectionInfoArray19[index36] = projectionInfo18;
            string[] strArray20 = strArray1;
            int index37 = num37;
            int num39 = index37 + 1;
            strArray20[index37] = "IKeyValuePair`2";
            MetadataReader.ProjectionInfo[] projectionInfoArray20 = projectionInfoArray1;
            int index38 = num38;
            int num40 = index38 + 1;
            MetadataReader.ProjectionInfo projectionInfo19 = new MetadataReader.ProjectionInfo("Windows.Foundation.Collections", StringHandle.VirtualIndex.System_Collections_Generic, StringHandle.VirtualIndex.KeyValuePair2, clrAssembly2, signatureTreatment: TypeRefSignatureTreatment.ProjectedToValueType);
            projectionInfoArray20[index38] = projectionInfo19;
            string[] strArray21 = strArray1;
            int index39 = num39;
            int num41 = index39 + 1;
            strArray21[index39] = "IMapView`2";
            MetadataReader.ProjectionInfo[] projectionInfoArray21 = projectionInfoArray1;
            int index40 = num40;
            int num42 = index40 + 1;
            MetadataReader.ProjectionInfo projectionInfo20 = new MetadataReader.ProjectionInfo("Windows.Foundation.Collections", StringHandle.VirtualIndex.System_Collections_Generic, StringHandle.VirtualIndex.IReadOnlyDictionary2, clrAssembly2);
            projectionInfoArray21[index40] = projectionInfo20;
            string[] strArray22 = strArray1;
            int index41 = num41;
            int num43 = index41 + 1;
            strArray22[index41] = "IMap`2";
            MetadataReader.ProjectionInfo[] projectionInfoArray22 = projectionInfoArray1;
            int index42 = num42;
            int num44 = index42 + 1;
            MetadataReader.ProjectionInfo projectionInfo21 = new MetadataReader.ProjectionInfo("Windows.Foundation.Collections", StringHandle.VirtualIndex.System_Collections_Generic, StringHandle.VirtualIndex.IDictionary2, clrAssembly2);
            projectionInfoArray22[index42] = projectionInfo21;
            string[] strArray23 = strArray1;
            int index43 = num43;
            int num45 = index43 + 1;
            strArray23[index43] = "INotifyCollectionChanged";
            MetadataReader.ProjectionInfo[] projectionInfoArray23 = projectionInfoArray1;
            int index44 = num44;
            int num46 = index44 + 1;
            MetadataReader.ProjectionInfo projectionInfo22 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System_Collections_Specialized, StringHandle.VirtualIndex.INotifyCollectionChanged, clrAssembly3);
            projectionInfoArray23[index44] = projectionInfo22;
            string[] strArray24 = strArray1;
            int index45 = num45;
            int num47 = index45 + 1;
            strArray24[index45] = "INotifyPropertyChanged";
            MetadataReader.ProjectionInfo[] projectionInfoArray24 = projectionInfoArray1;
            int index46 = num46;
            int num48 = index46 + 1;
            MetadataReader.ProjectionInfo projectionInfo23 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Data", StringHandle.VirtualIndex.System_ComponentModel, StringHandle.VirtualIndex.INotifyPropertyChanged, clrAssembly3);
            projectionInfoArray24[index46] = projectionInfo23;
            string[] strArray25 = strArray1;
            int index47 = num47;
            int num49 = index47 + 1;
            strArray25[index47] = "IReference`1";
            MetadataReader.ProjectionInfo[] projectionInfoArray25 = projectionInfoArray1;
            int index48 = num48;
            int num50 = index48 + 1;
            MetadataReader.ProjectionInfo projectionInfo24 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.Nullable1, clrAssembly2, signatureTreatment: TypeRefSignatureTreatment.ProjectedToValueType);
            projectionInfoArray25[index48] = projectionInfo24;
            string[] strArray26 = strArray1;
            int index49 = num49;
            int num51 = index49 + 1;
            strArray26[index49] = "IVectorView`1";
            MetadataReader.ProjectionInfo[] projectionInfoArray26 = projectionInfoArray1;
            int index50 = num50;
            int num52 = index50 + 1;
            MetadataReader.ProjectionInfo projectionInfo25 = new MetadataReader.ProjectionInfo("Windows.Foundation.Collections", StringHandle.VirtualIndex.System_Collections_Generic, StringHandle.VirtualIndex.IReadOnlyList1, clrAssembly2);
            projectionInfoArray26[index50] = projectionInfo25;
            string[] strArray27 = strArray1;
            int index51 = num51;
            int num53 = index51 + 1;
            strArray27[index51] = "IVector`1";
            MetadataReader.ProjectionInfo[] projectionInfoArray27 = projectionInfoArray1;
            int index52 = num52;
            int num54 = index52 + 1;
            MetadataReader.ProjectionInfo projectionInfo26 = new MetadataReader.ProjectionInfo("Windows.Foundation.Collections", StringHandle.VirtualIndex.System_Collections_Generic, StringHandle.VirtualIndex.IList1, clrAssembly2);
            projectionInfoArray27[index52] = projectionInfo26;
            string[] strArray28 = strArray1;
            int index53 = num53;
            int num55 = index53 + 1;
            strArray28[index53] = "KeyTime";
            MetadataReader.ProjectionInfo[] projectionInfoArray28 = projectionInfoArray1;
            int index54 = num54;
            int num56 = index54 + 1;
            MetadataReader.ProjectionInfo projectionInfo27 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Media.Animation", StringHandle.VirtualIndex.Windows_UI_Xaml_Media_Animation, StringHandle.VirtualIndex.KeyTime, clrAssembly4);
            projectionInfoArray28[index54] = projectionInfo27;
            string[] strArray29 = strArray1;
            int index55 = num55;
            int num57 = index55 + 1;
            strArray29[index55] = "Matrix";
            MetadataReader.ProjectionInfo[] projectionInfoArray29 = projectionInfoArray1;
            int index56 = num56;
            int num58 = index56 + 1;
            MetadataReader.ProjectionInfo projectionInfo28 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Media", StringHandle.VirtualIndex.Windows_UI_Xaml_Media, StringHandle.VirtualIndex.Matrix, clrAssembly4);
            projectionInfoArray29[index56] = projectionInfo28;
            string[] strArray30 = strArray1;
            int index57 = num57;
            int num59 = index57 + 1;
            strArray30[index57] = "Matrix3D";
            MetadataReader.ProjectionInfo[] projectionInfoArray30 = projectionInfoArray1;
            int index58 = num58;
            int num60 = index58 + 1;
            MetadataReader.ProjectionInfo projectionInfo29 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Media.Media3D", StringHandle.VirtualIndex.Windows_UI_Xaml_Media_Media3D, StringHandle.VirtualIndex.Matrix3D, clrAssembly4);
            projectionInfoArray30[index58] = projectionInfo29;
            string[] strArray31 = strArray1;
            int index59 = num59;
            int num61 = index59 + 1;
            strArray31[index59] = "Matrix3x2";
            MetadataReader.ProjectionInfo[] projectionInfoArray31 = projectionInfoArray1;
            int index60 = num60;
            int num62 = index60 + 1;
            MetadataReader.ProjectionInfo projectionInfo30 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Matrix3x2, clrAssembly6);
            projectionInfoArray31[index60] = projectionInfo30;
            string[] strArray32 = strArray1;
            int index61 = num61;
            int num63 = index61 + 1;
            strArray32[index61] = "Matrix4x4";
            MetadataReader.ProjectionInfo[] projectionInfoArray32 = projectionInfoArray1;
            int index62 = num62;
            int num64 = index62 + 1;
            MetadataReader.ProjectionInfo projectionInfo31 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Matrix4x4, clrAssembly6);
            projectionInfoArray32[index62] = projectionInfo31;
            string[] strArray33 = strArray1;
            int index63 = num63;
            int num65 = index63 + 1;
            strArray33[index63] = "NotifyCollectionChangedAction";
            MetadataReader.ProjectionInfo[] projectionInfoArray33 = projectionInfoArray1;
            int index64 = num64;
            int num66 = index64 + 1;
            MetadataReader.ProjectionInfo projectionInfo32 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System_Collections_Specialized, StringHandle.VirtualIndex.NotifyCollectionChangedAction, clrAssembly3);
            projectionInfoArray33[index64] = projectionInfo32;
            string[] strArray34 = strArray1;
            int index65 = num65;
            int num67 = index65 + 1;
            strArray34[index65] = "NotifyCollectionChangedEventArgs";
            MetadataReader.ProjectionInfo[] projectionInfoArray34 = projectionInfoArray1;
            int index66 = num66;
            int num68 = index66 + 1;
            MetadataReader.ProjectionInfo projectionInfo33 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System_Collections_Specialized, StringHandle.VirtualIndex.NotifyCollectionChangedEventArgs, clrAssembly3);
            projectionInfoArray34[index66] = projectionInfo33;
            string[] strArray35 = strArray1;
            int index67 = num67;
            int num69 = index67 + 1;
            strArray35[index67] = "NotifyCollectionChangedEventHandler";
            MetadataReader.ProjectionInfo[] projectionInfoArray35 = projectionInfoArray1;
            int index68 = num68;
            int num70 = index68 + 1;
            MetadataReader.ProjectionInfo projectionInfo34 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System_Collections_Specialized, StringHandle.VirtualIndex.NotifyCollectionChangedEventHandler, clrAssembly3);
            projectionInfoArray35[index68] = projectionInfo34;
            string[] strArray36 = strArray1;
            int index69 = num69;
            int num71 = index69 + 1;
            strArray36[index69] = "Plane";
            MetadataReader.ProjectionInfo[] projectionInfoArray36 = projectionInfoArray1;
            int index70 = num70;
            int num72 = index70 + 1;
            MetadataReader.ProjectionInfo projectionInfo35 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Plane, clrAssembly6);
            projectionInfoArray36[index70] = projectionInfo35;
            string[] strArray37 = strArray1;
            int index71 = num71;
            int num73 = index71 + 1;
            strArray37[index71] = "Point";
            MetadataReader.ProjectionInfo[] projectionInfoArray37 = projectionInfoArray1;
            int index72 = num72;
            int num74 = index72 + 1;
            MetadataReader.ProjectionInfo projectionInfo36 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.Windows_Foundation, StringHandle.VirtualIndex.Point, clrAssembly1);
            projectionInfoArray37[index72] = projectionInfo36;
            string[] strArray38 = strArray1;
            int index73 = num73;
            int num75 = index73 + 1;
            strArray38[index73] = "PropertyChangedEventArgs";
            MetadataReader.ProjectionInfo[] projectionInfoArray38 = projectionInfoArray1;
            int index74 = num74;
            int num76 = index74 + 1;
            MetadataReader.ProjectionInfo projectionInfo37 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Data", StringHandle.VirtualIndex.System_ComponentModel, StringHandle.VirtualIndex.PropertyChangedEventArgs, clrAssembly3);
            projectionInfoArray38[index74] = projectionInfo37;
            string[] strArray39 = strArray1;
            int index75 = num75;
            int num77 = index75 + 1;
            strArray39[index75] = "PropertyChangedEventHandler";
            MetadataReader.ProjectionInfo[] projectionInfoArray39 = projectionInfoArray1;
            int index76 = num76;
            int num78 = index76 + 1;
            MetadataReader.ProjectionInfo projectionInfo38 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Data", StringHandle.VirtualIndex.System_ComponentModel, StringHandle.VirtualIndex.PropertyChangedEventHandler, clrAssembly3);
            projectionInfoArray39[index76] = projectionInfo38;
            string[] strArray40 = strArray1;
            int index77 = num77;
            int num79 = index77 + 1;
            strArray40[index77] = "Quaternion";
            MetadataReader.ProjectionInfo[] projectionInfoArray40 = projectionInfoArray1;
            int index78 = num78;
            int num80 = index78 + 1;
            MetadataReader.ProjectionInfo projectionInfo39 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Quaternion, clrAssembly6);
            projectionInfoArray40[index78] = projectionInfo39;
            string[] strArray41 = strArray1;
            int index79 = num79;
            int num81 = index79 + 1;
            strArray41[index79] = "Rect";
            MetadataReader.ProjectionInfo[] projectionInfoArray41 = projectionInfoArray1;
            int index80 = num80;
            int num82 = index80 + 1;
            MetadataReader.ProjectionInfo projectionInfo40 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.Windows_Foundation, StringHandle.VirtualIndex.Rect, clrAssembly1);
            projectionInfoArray41[index80] = projectionInfo40;
            string[] strArray42 = strArray1;
            int index81 = num81;
            int num83 = index81 + 1;
            strArray42[index81] = "RepeatBehavior";
            MetadataReader.ProjectionInfo[] projectionInfoArray42 = projectionInfoArray1;
            int index82 = num82;
            int num84 = index82 + 1;
            MetadataReader.ProjectionInfo projectionInfo41 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Media.Animation", StringHandle.VirtualIndex.Windows_UI_Xaml_Media_Animation, StringHandle.VirtualIndex.RepeatBehavior, clrAssembly4);
            projectionInfoArray42[index82] = projectionInfo41;
            string[] strArray43 = strArray1;
            int index83 = num83;
            int num85 = index83 + 1;
            strArray43[index83] = "RepeatBehaviorType";
            MetadataReader.ProjectionInfo[] projectionInfoArray43 = projectionInfoArray1;
            int index84 = num84;
            int num86 = index84 + 1;
            MetadataReader.ProjectionInfo projectionInfo42 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Media.Animation", StringHandle.VirtualIndex.Windows_UI_Xaml_Media_Animation, StringHandle.VirtualIndex.RepeatBehaviorType, clrAssembly4);
            projectionInfoArray43[index84] = projectionInfo42;
            string[] strArray44 = strArray1;
            int index85 = num85;
            int num87 = index85 + 1;
            strArray44[index85] = "Size";
            MetadataReader.ProjectionInfo[] projectionInfoArray44 = projectionInfoArray1;
            int index86 = num86;
            int num88 = index86 + 1;
            MetadataReader.ProjectionInfo projectionInfo43 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.Windows_Foundation, StringHandle.VirtualIndex.Size, clrAssembly1);
            projectionInfoArray44[index86] = projectionInfo43;
            string[] strArray45 = strArray1;
            int index87 = num87;
            int num89 = index87 + 1;
            strArray45[index87] = "Thickness";
            MetadataReader.ProjectionInfo[] projectionInfoArray45 = projectionInfoArray1;
            int index88 = num88;
            int num90 = index88 + 1;
            MetadataReader.ProjectionInfo projectionInfo44 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml", StringHandle.VirtualIndex.Windows_UI_Xaml, StringHandle.VirtualIndex.Thickness, clrAssembly4);
            projectionInfoArray45[index88] = projectionInfo44;
            string[] strArray46 = strArray1;
            int index89 = num89;
            int num91 = index89 + 1;
            strArray46[index89] = "TimeSpan";
            MetadataReader.ProjectionInfo[] projectionInfoArray46 = projectionInfoArray1;
            int index90 = num90;
            int num92 = index90 + 1;
            MetadataReader.ProjectionInfo projectionInfo45 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.TimeSpan, clrAssembly2);
            projectionInfoArray46[index90] = projectionInfo45;
            string[] strArray47 = strArray1;
            int index91 = num91;
            int num93 = index91 + 1;
            strArray47[index91] = "TypeName";
            MetadataReader.ProjectionInfo[] projectionInfoArray47 = projectionInfoArray1;
            int index92 = num92;
            int num94 = index92 + 1;
            MetadataReader.ProjectionInfo projectionInfo46 = new MetadataReader.ProjectionInfo("Windows.UI.Xaml.Interop", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.Type, clrAssembly2, signatureTreatment: TypeRefSignatureTreatment.ProjectedToClass);
            projectionInfoArray47[index92] = projectionInfo46;
            string[] strArray48 = strArray1;
            int index93 = num93;
            int num95 = index93 + 1;
            strArray48[index93] = "Uri";
            MetadataReader.ProjectionInfo[] projectionInfoArray48 = projectionInfoArray1;
            int index94 = num94;
            int num96 = index94 + 1;
            MetadataReader.ProjectionInfo projectionInfo47 = new MetadataReader.ProjectionInfo("Windows.Foundation", StringHandle.VirtualIndex.System, StringHandle.VirtualIndex.Uri, clrAssembly2);
            projectionInfoArray48[index94] = projectionInfo47;
            string[] strArray49 = strArray1;
            int index95 = num95;
            int num97 = index95 + 1;
            strArray49[index95] = "Vector2";
            MetadataReader.ProjectionInfo[] projectionInfoArray49 = projectionInfoArray1;
            int index96 = num96;
            int num98 = index96 + 1;
            MetadataReader.ProjectionInfo projectionInfo48 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Vector2, clrAssembly6);
            projectionInfoArray49[index96] = projectionInfo48;
            string[] strArray50 = strArray1;
            int index97 = num97;
            int num99 = index97 + 1;
            strArray50[index97] = "Vector3";
            MetadataReader.ProjectionInfo[] projectionInfoArray50 = projectionInfoArray1;
            int index98 = num98;
            int num100 = index98 + 1;
            MetadataReader.ProjectionInfo projectionInfo49 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Vector3, clrAssembly6);
            projectionInfoArray50[index98] = projectionInfo49;
            string[] strArray51 = strArray1;
            int index99 = num99;
            int num101 = index99 + 1;
            strArray51[index99] = "Vector4";
            MetadataReader.ProjectionInfo[] projectionInfoArray51 = projectionInfoArray1;
            int index100 = num100;
            int num102 = index100 + 1;
            MetadataReader.ProjectionInfo projectionInfo50 = new MetadataReader.ProjectionInfo("Windows.Foundation.Numerics", StringHandle.VirtualIndex.System_Numerics, StringHandle.VirtualIndex.Vector4, clrAssembly6);
            projectionInfoArray51[index100] = projectionInfo50;
            MetadataReader.s_projectedTypeNames = strArray1;
            MetadataReader.s_projectionInfos = projectionInfoArray1;
        }

        [Conditional("DEBUG")]
        private static void AssertSorted(string[] keys)
        {
            int num = 0;
            while (num < keys.Length - 1)
                ++num;
        }


#nullable enable
        internal static string[] GetProjectedTypeNames()
        {
            MetadataReader.InitializeProjectedTypes();
            return MetadataReader.s_projectedTypeNames;
        }

        private static uint TreatmentAndRowId(byte treatment, int rowId) => (uint)((int)treatment << 24 | rowId);

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal uint CalculateTypeDefTreatmentAndRowId(TypeDefinitionHandle handle)
        {
            TypeAttributes flags = this.TypeDefTable.GetFlags(handle);
            EntityHandle extends = this.TypeDefTable.GetExtends(handle);
            TypeDefTreatment treatment;
            if ((flags & TypeAttributes.WindowsRuntime) != TypeAttributes.NotPublic)
            {
                if (this._metadataKind == MetadataKind.WindowsMetadata)
                {
                    TypeDefTreatment definitionTreatment = this.GetWellKnownTypeDefinitionTreatment(handle);
                    if (definitionTreatment != TypeDefTreatment.None)
                        return MetadataReader.TreatmentAndRowId((byte)definitionTreatment, handle.RowId);
                    treatment = extends.Kind != HandleKind.TypeReference || !this.IsSystemAttribute((TypeReferenceHandle)extends) ? TypeDefTreatment.NormalNonAttribute : TypeDefTreatment.NormalAttribute;
                }
                else
                    treatment = this._metadataKind != MetadataKind.ManagedWindowsMetadata || !this.NeedsWinRTPrefix(flags, extends) ? TypeDefTreatment.None : TypeDefTreatment.PrefixWinRTName;
                if ((treatment == TypeDefTreatment.PrefixWinRTName || treatment == TypeDefTreatment.NormalNonAttribute) && (flags & TypeAttributes.ClassSemanticsMask) == TypeAttributes.NotPublic && this.HasAttribute((EntityHandle)handle, "Windows.UI.Xaml", "TreatAsAbstractComposableClassAttribute"))
                    treatment |= TypeDefTreatment.MarkAbstractFlag;
            }
            else
                treatment = this._metadataKind != MetadataKind.ManagedWindowsMetadata || !this.IsClrImplementationType(handle) ? TypeDefTreatment.None : TypeDefTreatment.UnmangleWinRTName;
            return MetadataReader.TreatmentAndRowId((byte)treatment, handle.RowId);
        }

        private bool IsClrImplementationType(TypeDefinitionHandle typeDef) => (this.TypeDefTable.GetFlags(typeDef) & (TypeAttributes.VisibilityMask | TypeAttributes.SpecialName)) == TypeAttributes.SpecialName && this.StringHeap.StartsWithRaw(this.TypeDefTable.GetName(typeDef), "<CLR>");

        internal uint CalculateTypeRefTreatmentAndRowId(TypeReferenceHandle handle)
        {
            int forTypeReference = this.GetProjectionIndexForTypeReference(handle, out bool _);
            return forTypeReference >= 0 ? MetadataReader.TreatmentAndRowId((byte)3, forTypeReference) : MetadataReader.TreatmentAndRowId((byte)this.GetSpecialTypeRefTreatment(handle), handle.RowId);
        }

        private TypeRefTreatment GetSpecialTypeRefTreatment(TypeReferenceHandle handle)
        {
            if (this.StringHeap.EqualsRaw(this.TypeRefTable.GetNamespace(handle), "System"))
            {
                StringHandle name = this.TypeRefTable.GetName(handle);
                if (this.StringHeap.EqualsRaw(name, "MulticastDelegate"))
                    return TypeRefTreatment.SystemDelegate;
                if (this.StringHeap.EqualsRaw(name, "Attribute"))
                    return TypeRefTreatment.SystemAttribute;
            }
            return TypeRefTreatment.None;
        }

        private bool IsSystemAttribute(TypeReferenceHandle handle) => this.StringHeap.EqualsRaw(this.TypeRefTable.GetNamespace(handle), "System") && this.StringHeap.EqualsRaw(this.TypeRefTable.GetName(handle), "Attribute");

        private bool NeedsWinRTPrefix(TypeAttributes flags, EntityHandle extends)
        {
            if ((flags & (TypeAttributes.VisibilityMask | TypeAttributes.ClassSemanticsMask)) != TypeAttributes.Public || extends.Kind != HandleKind.TypeReference)
                return false;
            TypeReferenceHandle handle = (TypeReferenceHandle)extends;
            if (this.StringHeap.EqualsRaw(this.TypeRefTable.GetNamespace(handle), "System"))
            {
                StringHandle name = this.TypeRefTable.GetName(handle);
                if (this.StringHeap.EqualsRaw(name, "MulticastDelegate") || this.StringHeap.EqualsRaw(name, "ValueType") || this.StringHeap.EqualsRaw(name, "Attribute"))
                    return false;
            }
            return true;
        }

        private uint CalculateMethodDefTreatmentAndRowId(MethodDefinitionHandle methodDef)
        {
            MethodDefTreatment treatment = MethodDefTreatment.Implementation;
            TypeDefinitionHandle declaringType = this.GetDeclaringType(methodDef);
            TypeAttributes flags = this.TypeDefTable.GetFlags(declaringType);
            if ((flags & TypeAttributes.WindowsRuntime) != TypeAttributes.NotPublic)
            {
                if (this.IsClrImplementationType(declaringType))
                    treatment = MethodDefTreatment.Implementation;
                else if (flags.IsNested())
                    treatment = MethodDefTreatment.Implementation;
                else if ((flags & TypeAttributes.ClassSemanticsMask) != TypeAttributes.NotPublic)
                    treatment = MethodDefTreatment.InterfaceMethod;
                else if (this._metadataKind == MetadataKind.ManagedWindowsMetadata && (flags & TypeAttributes.Public) == TypeAttributes.NotPublic)
                {
                    treatment = MethodDefTreatment.Implementation;
                }
                else
                {
                    treatment = MethodDefTreatment.Other;
                    EntityHandle extends = this.TypeDefTable.GetExtends(declaringType);
                    if (extends.Kind == HandleKind.TypeReference)
                    {
                        switch (this.GetSpecialTypeRefTreatment((TypeReferenceHandle)extends))
                        {
                            case TypeRefTreatment.SystemDelegate:
                                treatment = MethodDefTreatment.DelegateMethod | MethodDefTreatment.MarkPublicFlag;
                                break;
                            case TypeRefTreatment.SystemAttribute:
                                treatment = MethodDefTreatment.AttributeMethod;
                                break;
                        }
                    }
                }
            }
            if (treatment == MethodDefTreatment.Other)
            {
                bool flag1 = false;
                bool flag2 = false;
                bool isIDisposable = false;
                foreach (MethodImplementationHandle handle in new MethodImplementationHandleCollection(this, declaringType))
                {
                    MethodImplementation methodImplementation = this.GetMethodImplementation(handle);
                    if (methodImplementation.MethodBody == (EntityHandle)methodDef)
                    {
                        EntityHandle methodDeclaration = methodImplementation.MethodDeclaration;
                        if (methodDeclaration.Kind == HandleKind.MemberReference && this.ImplementsRedirectedInterface((MemberReferenceHandle)methodDeclaration, out isIDisposable))
                        {
                            flag1 = true;
                            if (isIDisposable)
                                break;
                        }
                        else
                            flag2 = true;
                    }
                }
                if (isIDisposable)
                    treatment = MethodDefTreatment.DisposeMethod;
                else if (flag1 && !flag2)
                    treatment = MethodDefTreatment.HiddenInterfaceImplementation;
            }
            if (treatment == MethodDefTreatment.Other)
                treatment |= this.GetMethodTreatmentFromCustomAttributes(methodDef);
            return MetadataReader.TreatmentAndRowId((byte)treatment, methodDef.RowId);
        }

        private MethodDefTreatment GetMethodTreatmentFromCustomAttributes(
          MethodDefinitionHandle methodDef)
        {
            MethodDefTreatment customAttributes = MethodDefTreatment.None;
            foreach (CustomAttributeHandle customAttribute in this.GetCustomAttributes((EntityHandle)methodDef))
            {
                StringHandle namespaceName;
                StringHandle typeName;
                if (this.GetAttributeTypeNameRaw(customAttribute, out namespaceName, out typeName) && this.StringHeap.EqualsRaw(namespaceName, "Windows.UI.Xaml"))
                {
                    if (this.StringHeap.EqualsRaw(typeName, "TreatAsPublicMethodAttribute"))
                        customAttributes |= MethodDefTreatment.MarkPublicFlag;
                    if (this.StringHeap.EqualsRaw(typeName, "TreatAsAbstractMethodAttribute"))
                        customAttributes |= MethodDefTreatment.MarkAbstractFlag;
                }
            }
            return customAttributes;
        }

        /// <summary>
        /// The backing field of a WinRT enumeration type is not public although the backing fields
        /// of managed enumerations are. To allow managed languages to directly access this field,
        /// it is made public by the metadata adapter.
        /// </summary>
        private uint CalculateFieldDefTreatmentAndRowId(FieldDefinitionHandle handle)
        {
            FieldAttributes flags = this.FieldTable.GetFlags(handle);
            FieldDefTreatment treatment = FieldDefTreatment.None;
            if ((flags & FieldAttributes.RTSpecialName) != FieldAttributes.PrivateScope && this.StringHeap.EqualsRaw(this.FieldTable.GetName(handle), "value__"))
            {
                EntityHandle extends = this.TypeDefTable.GetExtends(this.GetDeclaringType(handle));
                if (extends.Kind == HandleKind.TypeReference)
                {
                    TypeReferenceHandle handle1 = (TypeReferenceHandle)extends;
                    if (this.StringHeap.EqualsRaw(this.TypeRefTable.GetName(handle1), "Enum") && this.StringHeap.EqualsRaw(this.TypeRefTable.GetNamespace(handle1), "System"))
                        treatment = FieldDefTreatment.EnumValue;
                }
            }
            return MetadataReader.TreatmentAndRowId((byte)treatment, handle.RowId);
        }

        private uint CalculateMemberRefTreatmentAndRowId(MemberReferenceHandle handle)
        {
            bool isIDisposable;
            return MetadataReader.TreatmentAndRowId(!(this.ImplementsRedirectedInterface(handle, out isIDisposable) & isIDisposable) ? (byte)0 : (byte)1, handle.RowId);
        }


#nullable disable
        /// <summary>
        /// We want to know if a given method implements a redirected interface.
        /// For example, if we are given the method RemoveAt on a class "A"
        /// which implements the IVector interface (which is redirected
        /// to IList in .NET) then this method would return true. The most
        /// likely reason why we would want to know this is that we wish to hide
        /// (mark private) all methods which implement methods on a redirected
        /// interface.
        /// </summary>
        /// <param name="memberRef">The declaration token for the method</param>
        /// <param name="isIDisposable">
        /// Returns true if the redirected interface is <see cref="T:System.IDisposable" />.
        /// </param>
        /// <returns>True if the method implements a method on a redirected interface.
        /// False otherwise.</returns>
        private bool ImplementsRedirectedInterface(
          MemberReferenceHandle memberRef,
          out bool isIDisposable)
        {
            isIDisposable = false;
            EntityHandle handle = this.MemberRefTable.GetClass(memberRef);
            TypeReferenceHandle typeRef;
            if (handle.Kind == HandleKind.TypeReference)
            {
                typeRef = (TypeReferenceHandle)handle;
            }
            else
            {
                if (handle.Kind != HandleKind.TypeSpecification)
                    return false;
                BlobReader blobReader = new BlobReader(this.BlobHeap.GetMemoryBlock(this.TypeSpecTable.GetSignature((TypeSpecificationHandle)handle)));
                if (blobReader.Length < 2 || blobReader.ReadByte() != (byte)21 || blobReader.ReadByte() != (byte)18)
                    return false;
                EntityHandle entityHandle = blobReader.ReadTypeHandle();
                if (entityHandle.Kind != HandleKind.TypeReference)
                    return false;
                typeRef = (TypeReferenceHandle)entityHandle;
            }
            return this.GetProjectionIndexForTypeReference(typeRef, out isIDisposable) >= 0;
        }

        private int FindMscorlibAssemblyRefNoProjection()
        {
            for (int rowId = 1; rowId <= this.AssemblyRefTable.NumberOfNonVirtualRows; ++rowId)
            {
                if (this.StringHeap.EqualsRaw(this.AssemblyRefTable.GetName(rowId), "mscorlib"))
                    return rowId;
            }
            throw new BadImageFormatException(SR.WinMDMissingMscorlibRef);
        }

        internal CustomAttributeValueTreatment CalculateCustomAttributeValueTreatment(
          CustomAttributeHandle handle)
        {
            EntityHandle parent = this.CustomAttributeTable.GetParent(handle);
            if (!this.IsWindowsAttributeUsageAttribute(parent, handle))
                return CustomAttributeValueTreatment.None;
            TypeDefinitionHandle definitionHandle = (TypeDefinitionHandle)parent;
            if (this.StringHeap.EqualsRaw(this.TypeDefTable.GetNamespace(definitionHandle), "Windows.Foundation.Metadata"))
            {
                if (this.StringHeap.EqualsRaw(this.TypeDefTable.GetName(definitionHandle), "VersionAttribute"))
                    return CustomAttributeValueTreatment.AttributeUsageVersionAttribute;
                if (this.StringHeap.EqualsRaw(this.TypeDefTable.GetName(definitionHandle), "DeprecatedAttribute"))
                    return CustomAttributeValueTreatment.AttributeUsageDeprecatedAttribute;
            }
            return !this.HasAttribute((EntityHandle)definitionHandle, "Windows.Foundation.Metadata", "AllowMultipleAttribute") ? CustomAttributeValueTreatment.AttributeUsageAllowSingle : CustomAttributeValueTreatment.AttributeUsageAllowMultiple;
        }

        private bool IsWindowsAttributeUsageAttribute(
          EntityHandle targetType,
          CustomAttributeHandle attributeHandle)
        {
            if (targetType.Kind != HandleKind.TypeDefinition)
                return false;
            EntityHandle constructor = this.CustomAttributeTable.GetConstructor(attributeHandle);
            if (constructor.Kind != HandleKind.MemberReference)
                return false;
            EntityHandle entityHandle = this.MemberRefTable.GetClass((MemberReferenceHandle)constructor);
            if (entityHandle.Kind != HandleKind.TypeReference)
                return false;
            TypeReferenceHandle handle = (TypeReferenceHandle)entityHandle;
            return this.StringHeap.EqualsRaw(this.TypeRefTable.GetName(handle), "AttributeUsageAttribute") && this.StringHeap.EqualsRaw(this.TypeRefTable.GetNamespace(handle), "Windows.Foundation.Metadata");
        }

        private bool HasAttribute(EntityHandle token, string asciiNamespaceName, string asciiTypeName)
        {
            foreach (CustomAttributeHandle customAttribute in this.GetCustomAttributes(token))
            {
                StringHandle namespaceName;
                StringHandle typeName;
                if (this.GetAttributeTypeNameRaw(customAttribute, out namespaceName, out typeName) && this.StringHeap.EqualsRaw(typeName, asciiTypeName) && this.StringHeap.EqualsRaw(namespaceName, asciiNamespaceName))
                    return true;
            }
            return false;
        }

        private bool GetAttributeTypeNameRaw(
          CustomAttributeHandle caHandle,
          out StringHandle namespaceName,
          out StringHandle typeName)
        {
            namespaceName = typeName = default(StringHandle);

            ref StringHandle local1 = ref namespaceName;
            ref StringHandle local2 = ref typeName;
            StringHandle stringHandle1 = new StringHandle();
            StringHandle stringHandle2;
            StringHandle stringHandle3 = stringHandle2 = stringHandle1;
            local2 = stringHandle2;
            StringHandle stringHandle4 = stringHandle3;
            local1 = stringHandle4;
            EntityHandle attributeTypeRaw = this.GetAttributeTypeRaw(caHandle);
            if (attributeTypeRaw.IsNil)
                return false;
            if (attributeTypeRaw.Kind == HandleKind.TypeReference)
            {
                TypeReferenceHandle handle = (TypeReferenceHandle)attributeTypeRaw;
                EntityHandle resolutionScope = this.TypeRefTable.GetResolutionScope(handle);
                if (!resolutionScope.IsNil && resolutionScope.Kind == HandleKind.TypeReference)
                    return false;
                typeName = this.TypeRefTable.GetName(handle);
                namespaceName = this.TypeRefTable.GetNamespace(handle);
            }
            else
            {
                if (attributeTypeRaw.Kind != HandleKind.TypeDefinition)
                    return false;
                TypeDefinitionHandle handle = (TypeDefinitionHandle)attributeTypeRaw;
                if (this.TypeDefTable.GetFlags(handle).IsNested())
                    return false;
                typeName = this.TypeDefTable.GetName(handle);
                namespaceName = this.TypeDefTable.GetNamespace(handle);
            }
            return true;
        }

        /// <summary>
        /// Returns the type definition or reference handle of the attribute type.
        /// </summary>
        /// <returns><see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" /> or <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or nil token if the metadata is invalid and the type can't be determined.</returns>
        private EntityHandle GetAttributeTypeRaw(CustomAttributeHandle handle)
        {
            EntityHandle constructor = this.CustomAttributeTable.GetConstructor(handle);
            if (constructor.Kind == HandleKind.MethodDefinition)
                return (EntityHandle)this.GetDeclaringType((MethodDefinitionHandle)constructor);
            if (constructor.Kind == HandleKind.MemberReference)
            {
                EntityHandle attributeTypeRaw = this.MemberRefTable.GetClass((MemberReferenceHandle)constructor);
                switch (attributeTypeRaw.Kind)
                {
                    case HandleKind.TypeReference:
                    case HandleKind.TypeDefinition:
                        return attributeTypeRaw;
                }
            }
            return new EntityHandle();
        }

        private readonly struct ProjectionInfo
        {
            public readonly string WinRTNamespace;
            public readonly StringHandle.VirtualIndex ClrNamespace;
            public readonly StringHandle.VirtualIndex ClrName;
            public readonly AssemblyReferenceHandle.VirtualIndex AssemblyRef;
            public readonly TypeDefTreatment Treatment;
            public readonly TypeRefSignatureTreatment SignatureTreatment;
            public readonly bool IsIDisposable;

            public ProjectionInfo(
              string winRtNamespace,
              StringHandle.VirtualIndex clrNamespace,
              StringHandle.VirtualIndex clrName,
              AssemblyReferenceHandle.VirtualIndex clrAssembly,
              TypeDefTreatment treatment = TypeDefTreatment.RedirectedToClrType,
              TypeRefSignatureTreatment signatureTreatment = TypeRefSignatureTreatment.None,
              bool isIDisposable = false)
            {
                this.WinRTNamespace = winRtNamespace;
                this.ClrNamespace = clrNamespace;
                this.ClrName = clrName;
                this.AssemblyRef = clrAssembly;
                this.Treatment = treatment;
                this.SignatureTreatment = signatureTreatment;
                this.IsIDisposable = isIDisposable;
            }
        }
    }
}

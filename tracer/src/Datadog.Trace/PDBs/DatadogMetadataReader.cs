// <copyright file="DatadogMetadataReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Collections.Immutable;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata.Ecma335;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.PortableExecutable;

namespace Datadog.Trace.Pdb
{
    /// <summary>
    /// Reads metadata as well as both Windows and Portable PDBs.
    /// Note: reading Windows PDBs is only supported on Windows.
    /// </summary>
    internal partial class DatadogMetadataReader : IDisposable
    {
        private const int RidMask = 0x00FFFFFF;
        private const string Unknown = "UNKNOWN";
        private const string CompilerGeneratedAttribute = "System.Runtime.CompilerServices.CompilerGeneratedAttribute";
        protected const string AsyncStateMachineAttribute = "System.Runtime.CompilerServices.AsyncStateMachineAttribute";
        private const int UnknownLocalLine = int.MaxValue;
        private static readonly Guid SourceLink = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        private static readonly Guid EncLambdaAndClosureMap = new("A643004C-0240-496F-A783-30D64F4979DE");
        private static readonly Guid EncLocalSlotMap = new("755F52A8-91C5-45BE-B4B8-209571E552BD");
        private static readonly Guid StateMachineHoistedLocalScopes = new("6DA9A61E-F8C7-4874-BE62-68BC5630DF71");
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DatadogMetadataReader>();
        private readonly PEReader _peReader;
        private readonly bool _isDnlibPdbReader;
        private bool _disposed;

        private DatadogMetadataReader(PEReader peReader, MetadataReader metadataReader, MetadataReader? pdbReader, string? pdbFullPath, Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols.SymbolReader? dnlibPdbReader, Datadog.Trace.Vendors.dnlib.DotNet.ModuleDefMD? dnlibModule)
        {
            MetadataReader = metadataReader;
            PdbReader = pdbReader;
            _peReader = peReader;
            _dnlibModule = dnlibModule;
            DnlibPdbReader = dnlibPdbReader;
            PdbFullPath = pdbFullPath;
            _isDnlibPdbReader = dnlibPdbReader != null;
            IsPdbExist = PdbReader != null || DnlibPdbReader != null;
        }

        /// <summary>
        /// Gets the pdb path if exists, otherwise the assembly location
        /// </summary>
        internal string? PdbFullPath { get; }

        internal MetadataReader MetadataReader { get; }

        internal MetadataReader? PdbReader { get; }

        internal bool IsPdbExist { get; set; }

        internal static DatadogMetadataReader? CreatePdbReader(Assembly? assembly)
        {
            if (assembly == null || string.IsNullOrEmpty(assembly.Location))
            {
                return null;
            }

            // For metadata we are always using System.Reflection.Metadata
            // For PDB, Reflection.Metadata for portable and embedded PDB and dnlib for windows PDB
            var peReader = new PEReader(File.OpenRead(assembly.Location), PEStreamOptions.PrefetchMetadata | PEStreamOptions.PrefetchEntireImage);
            MetadataReader metadataReader = peReader.GetMetadataReader(MetadataReaderOptions.Default);
            MetadataReader? pdbReader;
            if (peReader.TryOpenAssociatedPortablePdb(assembly.Location, File.OpenRead, out var metadataReaderProvider, out var pdbPath))
            {
                pdbReader = metadataReaderProvider!.GetMetadataReader(MetadataReaderOptions.Default, MetadataStringDecoder.DefaultUTF8);
                return new DatadogMetadataReader(peReader, metadataReader, pdbReader, pdbPath ?? assembly.Location, null, null);
            }

            if (!TryFindPdbFile(assembly.Location, out var pdbFullPath))
            {
                return new DatadogMetadataReader(peReader, metadataReader, null, null, null, null);
            }

            var module = Datadog.Trace.Vendors.dnlib.DotNet.ModuleDefMD.Load(assembly.ManifestModule, new Datadog.Trace.Vendors.dnlib.DotNet.ModuleCreationOptions { TryToLoadPdbFromDisk = false });
            var pdbStream = Datadog.Trace.Vendors.dnlib.IO.DataReaderFactoryFactory.Create(pdbFullPath, false);
            var dnlibReader = Datadog.Trace.Vendors.dnlib.DotNet.Pdb.SymbolReaderFactory.Create(Datadog.Trace.Vendors.dnlib.DotNet.ModuleCreationOptions.DefaultPdbReaderOptions, module.Metadata, pdbStream);
            if (dnlibReader == null)
            {
                return new DatadogMetadataReader(peReader, metadataReader, null, null, null, null);
            }

            dnlibReader.Initialize(module);
            module.LoadPdb(dnlibReader);
            return new DatadogMetadataReader(peReader, metadataReader, null, pdbFullPath, dnlibReader, module);
        }

        private static bool TryFindPdbFile(string assemblyLocation, [NotNullWhen(true)] out string? pdbFullPath)
        {
            try
            {
                string pdbInSameFolder = Path.ChangeExtension(assemblyLocation, "pdb");

                if (File.Exists(pdbInSameFolder))
                {
                    pdbFullPath = pdbInSameFolder;
                    return true;
                }

#if NETFRAMEWORK
                // When using Dynamic Compilation in IIS-based .NET Framework applications, the PDB file may not be found in the same folder as the DLL.
                // This is because the DLL is loaded from the shadow copy folder (e.g. C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Temporary ASP.NET Files\root\1234567890abcdef\MyApp.dll)
                // and the PDB may not be copied to this folder until later or never, as it is only copied on demand (usually when an exception is thrown).
                // In these cases, we'll try to find it in the application directory (e.g. C:\inetpub\wwwroot\MyApp\bin\MyApp.pdb).
                //
                // Note that the PDB file might be located in a subfolder of the application directory (e.g. C:\inetpub\wwwroot\MyApp\bin\roslyn\MyApp.pdb),
                // however, we do not support this scenario and avoid scanning the entire directory tree, in order to mitigate the risk of a performance hit
                // when the application directory contains a large number of files or is located on a slow network share (see https://github.com/DataDog/dd-trace-dotnet/issues/4226)
                string fileName = Path.GetFileName(pdbInSameFolder);
                string applicationDirectory = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
                if (applicationDirectory != null)
                {
                    var pdbInAppDirectory = Path.Combine(applicationDirectory, "bin", fileName);
                    if (File.Exists(pdbInAppDirectory))
                    {
                        pdbFullPath = pdbInAppDirectory;
                        return true;
                    }
                }
#endif

            }
            catch (Exception e)
            {
                Logger.Error(e, "Error while trying to find PDB file for {Location}", assemblyLocation);
                pdbFullPath = null;
                return false;
            }

            pdbFullPath = null;
            return false;
        }

        internal StandaloneSignature? GetLocalSignature(MethodDefinition method)
        {
            var methodBodyBlock = _peReader?.GetMethodBody(method.RelativeVirtualAddress);
            if (methodBodyBlock == null || methodBodyBlock.LocalSignature.IsNil)
            {
                return null;
            }

            return MetadataReader.GetStandaloneSignature(methodBodyBlock.LocalSignature);
        }

        internal int GetLocalVariablesCount(MethodDefinition method)
        {
            var signature = GetLocalSignature(method);
            if (!signature.HasValue)
            {
                return 0;
            }

            BlobReader blobReader = MetadataReader.GetBlobReader(signature.Value.Signature);

            if (blobReader.ReadByte() == (byte)SignatureKind.LocalVariables)
            {
                int variableCount = blobReader.ReadCompressedInteger();
                return variableCount;
            }

            return 0;
        }

        internal string? GetSourceLinkJsonDocument()
        {
            if (_isDnlibPdbReader)
            {
                return GetSourceLinkJsonDocumentDnlib();
            }

            if (PdbReader != null)
            {
                // Iterate over the CustomDebugInformation table to find the source link entry
                foreach (var dciHandle in PdbReader.CustomDebugInformation)
                {
                    CustomDebugInformation customDebugInfo = PdbReader.GetCustomDebugInformation(dciHandle);
                    // Check if the GUID matches the source link GUID
                    if (!customDebugInfo.Kind.IsNil && PdbReader.GetGuid(customDebugInfo.Kind).Equals(SourceLink))
                    {
                        byte[] bytes = PdbReader.GetBlobBytes(customDebugInfo.Value);

                        // Decode the UTF8-encoded source link info bytes into a string
                        return System.Text.Encoding.UTF8.GetString(bytes);
                    }
                }
            }

            return null;
        }

        internal DatadogSequencePoint[]? GetMethodSequencePoints(int rowId, bool searchMoveNext = true)
        {
            using var memory = GetMethodSequencePointsAsMemoryOwner(rowId, searchMoveNext, out var count);
            if (count == 0 || memory == null)
            {
                return null;
            }

            var asSpan = memory.Memory.Span;
            return asSpan.Slice(0, count).ToArray();
        }

        internal IMemoryOwner<DatadogSequencePoint>? GetMethodSequencePointsAsMemoryOwner(int rowId, bool searchMoveNext, out int count)
        {
            count = 0;
            if (_isDnlibPdbReader)
            {
                return GetMethodSequencePointsDnlib(rowId, searchMoveNext, out count);
            }

            if (PdbReader != null)
            {
                var methodDef = GetMethodDef(rowId);
                if (methodDef.Handle.IsNil)
                {
                    return null;
                }

                MethodDebugInformation methodDebugInformation = PdbReader.GetMethodDebugInformation(methodDef.Handle.ToDebugInformationHandle());
                if (methodDebugInformation.SequencePointsBlob.IsNil && searchMoveNext)
                {
                    var moveNext = GetMoveNextMethod(methodDef);
                    if (moveNext.IsNil)
                    {
                        return null;
                    }

                    methodDebugInformation = PdbReader.GetMethodDebugInformation(moveNext.ToDebugInformationHandle());
                    if (methodDebugInformation.SequencePointsBlob.IsNil)
                    {
                        return null;
                    }
                }

                var memory = ArrayMemoryPool<DatadogSequencePoint>.Shared.Rent();
                var sequencePoints = memory.Memory.Span;
                foreach (var sp in methodDebugInformation.GetSequencePoints())
                {
                    if (sp.IsHidden)
                    {
                        continue;
                    }

                    if (count >= sequencePoints.Length)
                    {
                        memory = memory.EnlargeBuffer(count);
                        sequencePoints = memory.Memory.Span;
                    }

                    sequencePoints[count] = new DatadogSequencePoint
                    {
                        StartLine = sp.StartLine,
                        EndLine = sp.EndLine,
                        StartColumn = sp.StartColumn,
                        EndColumn = sp.EndColumn,
                        Offset = sp.Offset,
                        IsHidden = sp.IsHidden,
                        URL = GetDocumentName(sp.Document)
                    };
                    count++;
                }

                return memory;
            }

            return null;
        }

        private MethodDefinitionHandle GetMoveNextMethod(MethodDefinition methodDef)
        {
            if (methodDef.GetDeclaringType().IsNil)
            {
                return default;
            }

            TypeDefinitionHandle nestedTypeHandle = default;
            var enclosingType = MetadataReader.GetTypeDefinition(methodDef.GetDeclaringType());
            var provider = new AsyncStateMachineAttributeTypeProvider();
            foreach (var attributeHandle in MetadataReader.GetCustomAttributes(methodDef.Handle))
            {
                if (attributeHandle.IsNil)
                {
                    continue;
                }

                if (GetAttributeName(attributeHandle) != AsyncStateMachineAttribute)
                {
                    continue;
                }

                var att = MetadataReader.GetCustomAttribute(attributeHandle);

                var decoded = att.DecodeValue(provider);
                if (decoded.FixedArguments.Length != 1)
                {
                    continue;
                }

                var fixedArgValue = decoded.FixedArguments[0].Value;
                if (fixedArgValue == null)
                {
                    return default;
                }

                foreach (var handle in enclosingType.GetNestedTypes())
                {
                    if (fixedArgValue.Equals(handle.FullName(MetadataReader)))
                    {
                        nestedTypeHandle = handle;
                        break;
                    }
                }

                if (nestedTypeHandle != default)
                {
                    break;
                }
            }

            if (nestedTypeHandle == default)
            {
                return default;
            }

            var generatedType = MetadataReader.GetTypeDefinition(nestedTypeHandle);
            var moveNext = generatedType.GetMethods().FirstOrDefault(handle => MetadataReader.GetString(MetadataReader.GetMethodDefinition(handle).Name) == "MoveNext");
            return moveNext;
        }

        private string? GetDocumentName(DocumentHandle doc)
        {
            if (PdbReader == null || doc.IsNil)
            {
                return null;
            }

            var document = PdbReader.GetDocument(doc);
            if (document.Name.IsNil)
            {
                return null;
            }

            return PdbReader.GetString(document.Name);
        }

        internal int? GetContainingMethodTokenAndOffset(string filePath, int line, int? column, out int? byteCodeOffset)
        {
            byteCodeOffset = null;

            if (_isDnlibPdbReader)
            {
                return GetContainingMethodTokenAndOffsetDnlib(filePath, line, column, out byteCodeOffset);
            }

            if (PdbReader != null)
            {
                var normalizeFilePath = GetNormalizedPath(filePath);
                if (string.IsNullOrEmpty(normalizeFilePath))
                {
                    return null;
                }

                const int methodDefTablePrefix = 0x06000000;
                foreach (MethodDefinitionHandle methodDefinitionHandle in MetadataReader.MethodDefinitions)
                {
                    MethodDebugInformation methodDebugInformation = PdbReader.GetMethodDebugInformation(methodDefinitionHandle);

                    foreach (VendoredMicrosoftCode.System.Reflection.Metadata.SequencePoint sequencePoint in methodDebugInformation.GetSequencePoints())
                    {
                        if (sequencePoint.IsHidden)
                        {
                            continue;
                        }

                        var normalizeDocumentPath = GetNormalizedPath(GetDocumentName(sequencePoint.Document));
                        if (!string.Equals(normalizeDocumentPath, normalizeFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }

                        // Check if the line and column match
                        if (sequencePoint.StartLine <= line && sequencePoint.EndLine >= line &&
                            (column.HasValue == false || (sequencePoint.StartColumn <= column && sequencePoint.EndColumn >= column)))
                        {
                            byteCodeOffset = sequencePoint.Offset;
                            return methodDefTablePrefix | methodDefinitionHandle.RowId;
                        }
                    }
                }
            }

            return null;

            string? GetNormalizedPath(string? path)
            {
                return path == null ? null : Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        internal IList<string>? GetDocuments()
        {
            List<string>? docs = null;
            if (_isDnlibPdbReader)
            {
                docs = new List<string>(DnlibPdbReader!.Documents.Count);
                for (int i = 0; i < DnlibPdbReader.Documents.Count; i++)
                {
                    var url = DnlibPdbReader.Documents[i].URL;
                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    docs.Add(url);
                }
            }

            if (PdbReader != null)
            {
                docs = new List<string>(PdbReader.Documents.Count);
                foreach (var document in PdbReader.Documents)
                {
                    if (document.IsNil)
                    {
                        continue;
                    }

                    var docName = PdbReader.GetDocument(document).Name;
                    if (docName.IsNil)
                    {
                        continue;
                    }

                    var url = PdbReader.GetString(docName);
                    if (string.IsNullOrEmpty(url))
                    {
                        continue;
                    }

                    docs.Add(url);
                }
            }

            return docs;
        }

        internal string[]? GetLocalVariableNames(int methodToken, int localVariablesCount, bool searchMoveNext)
        {
            if (_isDnlibPdbReader)
            {
                return GetLocalVariableNamesDnlib(methodToken & RidMask, localVariablesCount, searchMoveNext);
            }

            if (PdbReader != null)
            {
                return GetLocalVariableNames(methodToken & RidMask, searchMoveNext);
            }

            return null;
        }

        private string[]? GetLocalVariableNames(int rowId, bool searchMoveNext)
        {
            if (PdbReader == null)
            {
                return null;
            }

            var method = GetMethodDef(rowId);
            int localsCount = 0;
            var methodLocalsCount = GetLocalVariablesCount(method);
            if (methodLocalsCount == 0)
            {
                return null;
            }

            using var memory = ArrayMemoryPool<string>.Shared.Rent(methodLocalsCount);
            var names = memory.Memory.Span;

            var signature = GetLocalSignature(method);
            if (signature == null)
            {
                return null;
            }

            foreach (var scopeHandle in PdbReader.GetLocalScopes(method.Handle.ToDebugInformationHandle()))
            {
                var localScope = PdbReader.GetLocalScope(scopeHandle);
                foreach (var localVarHandle in localScope.GetLocalVariables())
                {
                    var local = PdbReader.GetLocalVariable(localVarHandle);
                    if (local.Index > methodLocalsCount || local.Attributes.HasFlag(LocalVariableAttributes.DebuggerHidden))
                    {
                        continue;
                    }

                    var localName = PdbReader.GetString(local.Name);
                    if (string.IsNullOrEmpty(localName))
                    {
                        continue;
                    }

                    names[local.Index] = localName;
                    localsCount++;
                }
            }

            if (localsCount == 0 && searchMoveNext)
            {
                var moveNext = GetMoveNextMethod(method);
                if (!moveNext.IsNil)
                {
                    return GetLocalVariableNames(moveNext.RowId, false);
                }
            }

            return names.Slice(0, methodLocalsCount).ToArray();
        }

        internal CustomDebugInfoAsyncAndClosure GetAsyncAndClosureCustomDebugInfo(int methodRid)
        {
            if (_isDnlibPdbReader)
            {
                return GetAsyncAndClosureCustomDebugInfoDnlib(methodRid);
            }

            CustomDebugInfoAsyncAndClosure cdiAsyncAndClosure = default;
            if (PdbReader != null)
            {
                var methodHandle = MethodDefinitionHandle.FromRowId(methodRid);
                if (methodHandle.IsNil)
                {
                    return default;
                }

                foreach (var handle in PdbReader.GetCustomDebugInformation(methodHandle))
                {
                    if (handle.IsNil)
                    {
                        continue;
                    }

                    var customDebugInformation = PdbReader.GetCustomDebugInformation(handle);
                    var cdiGuid = PdbReader.GetGuid(customDebugInformation.Kind);
                    if (cdiGuid == EncLocalSlotMap)
                    {
                        // https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#edit-and-continue-local-slot-map-c-and-vb-compilers
                        cdiAsyncAndClosure.LocalSlot = true;
                    }
                    else if (cdiGuid == EncLambdaAndClosureMap)
                    {
                        // https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#edit-and-continue-lambda-and-closure-map-c-and-vb-compilers
                        cdiAsyncAndClosure.EncLambdaAndClosureMap = true;
                    }
                    else if (cdiGuid == StateMachineHoistedLocalScopes)
                    {
                        // https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#state-machine-hoisted-local-scopes-c--vb-compilers
                        cdiAsyncAndClosure.StateMachineHoistedLocal = true;
                        MethodDebugInformation methodDebugInformation = PdbReader.GetMethodDebugInformation(methodHandle.ToDebugInformationHandle());
                        cdiAsyncAndClosure.StateMachineKickoffMethodRid = methodDebugInformation.GetStateMachineKickoffMethod().RowId;
                    }
                }
            }

            return cdiAsyncAndClosure;
        }

        internal bool IsCompilerGeneratedAttributeDefinedOnMethod(int methodRid)
        {
            var method = GetMethodDef(methodRid);
            var attributes = method.GetCustomAttributes();
            return IsCompilerGeneratedAttributeDefine(attributes);
        }

        internal bool IsCompilerGeneratedAttributeDefinedOnType(int typeRid)
        {
            var nestedType = MetadataReader.GetTypeDefinition(TypeDefinitionHandle.FromRowId(typeRid));
            var attributes = nestedType.GetCustomAttributes();
            return IsCompilerGeneratedAttributeDefine(attributes);
        }

        private bool IsCompilerGeneratedAttributeDefine(CustomAttributeHandleCollection attributes)
        {
            foreach (var attributeHandle in attributes)
            {
                if (attributeHandle.IsNil)
                {
                    continue;
                }

                var attributeName = GetAttributeName(attributeHandle);
                if (attributeName?.Equals(CompilerGeneratedAttribute) == true)
                {
                    return true;
                }
            }

            return false;
        }

        internal bool IsAsyncMethod(CustomAttributeHandleCollection attributes)
        {
            foreach (var attributeHandle in attributes)
            {
                if (attributeHandle.IsNil)
                {
                    continue;
                }

                if (GetAttributeName(attributeHandle) == AsyncStateMachineAttribute)
                {
                    return true;
                }
            }

            return false;
        }

        private string? GetAttributeName(CustomAttributeHandle attributeHandle)
        {
            var attribute = MetadataReader.GetCustomAttribute(attributeHandle);
            var constructorHandle = attribute.Constructor;
            if (constructorHandle.IsNil)
            {
                return null;
            }

            switch (constructorHandle.Kind)
            {
                case HandleKind.MemberReference:
                    {
                        var memberRef = MetadataReader.GetMemberReference((MemberReferenceHandle)constructorHandle);
                        var attributeTypeHandle = memberRef.Parent;
                        if (attributeTypeHandle.IsNil)
                        {
                            return null;
                        }

                        return attributeTypeHandle.FullName(MetadataReader);
                    }

                case HandleKind.MethodDefinition:
                    {
                        var constructorDefinition = MetadataReader.GetMethodDefinition((MethodDefinitionHandle)constructorHandle);
                        var attributeTypeHandle = constructorDefinition.GetDeclaringType();
                        if (attributeTypeHandle.IsNil)
                        {
                            return null;
                        }

                        return attributeTypeHandle.FullName(MetadataReader);
                    }

                default:
                    return null;
            }
        }

        internal MethodDefinition GetMethodDef(int methodRid)
        {
            return MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(methodRid));
        }

        internal ImmutableArray<LocalScope>? GetLocalSymbols(int rowId, VendoredMicrosoftCode.System.ReadOnlySpan<DatadogSequencePoint> sequencePoints, bool searchMoveNext)
        {
            if (_isDnlibPdbReader)
            {
                return GetLocalSymbolsDnlib(rowId, sequencePoints, searchMoveNext);
            }

            ImmutableArray<LocalScope>.Builder? localScopes = default;
            if (PdbReader != null)
            {
                MethodDefinition method = GetMethodDef(rowId);
                var methodLocalsCount = GetLocalVariablesCount(method);
                if (methodLocalsCount == 0)
                {
                    return null;
                }

                var signature = GetLocalSignature(method);
                if (signature == null)
                {
                    return null;
                }

                var localTypes = signature.Value.DecodeLocalSignature(new TypeProvider(false), 0);
                localScopes = new ImmutableArray<LocalScope>.Builder();

                foreach (var scopeHandle in PdbReader.GetLocalScopes(method.Handle.ToDebugInformationHandle()))
                {
                    var localScope = PdbReader.GetLocalScope(scopeHandle);
                    var locals = localScope.GetLocalVariables();
                    var constants = localScope.GetLocalConstants();
                    if (locals.Count == 0 && constants.Count == 0)
                    {
                        continue;
                    }

                    var datadogScop = new LocalScope();
                    var scopeLocals = new ImmutableArray<DatadogLocal>.Builder();
                    DatadogSequencePoint sequencePointForScope = default;
                    foreach (var localVarHandle in locals)
                    {
                        if (localVarHandle.IsNil)
                        {
                            continue;
                        }

                        var local = PdbReader.GetLocalVariable(localVarHandle);
                        if (local.Name.IsNil)
                        {
                            continue;
                        }

                        var localName = PdbReader.GetString(local.Name);
                        if (local.Index > methodLocalsCount ||
                            string.IsNullOrEmpty(localName))
                        {
                            continue;
                        }

                        if (sequencePointForScope == default)
                        {
                            foreach (var sequencePoint in sequencePoints)
                            {
                                if (sequencePoint.Offset >= localScope.StartOffset &&
                                    sequencePoint.Offset <= localScope.EndOffset)
                                {
                                    sequencePointForScope = sequencePoint;
                                    break;
                                }
                            }
                        }

                        datadogScop.SequencePoint = sequencePointForScope;
                        scopeLocals.Add(new DatadogLocal
                        {
                            Name = localName,
                            Type = localTypes[local.Index],
                            Index = local.Index,
                            Line = sequencePointForScope.StartLine
                        });
                    }

                    foreach (var constantHandle in constants)
                    {
                        if (constantHandle.IsNil)
                        {
                            continue;
                        }

                        var constant = PdbReader.GetLocalConstant(constantHandle);

                        if (constant.Name.IsNil)
                        {
                            continue;
                        }

                        var constantName = PdbReader.GetString(constant.Name);
                        if (string.IsNullOrEmpty(constantName))
                        {
                            continue;
                        }

                        var signatureDecoder = new SignatureDecoder<string, int>(new TypeProvider(false), PdbReader, 0);
                        var blobReader = PdbReader.GetBlobReader(constant.Signature);
                        var constantType = signatureDecoder.DecodeType(ref blobReader);

                        if (sequencePointForScope == default)
                        {
                            foreach (var sequencePoint in sequencePoints)
                            {
                                if (sequencePoint.Offset >= localScope.StartOffset &&
                                    sequencePoint.Offset <= localScope.EndOffset)
                                {
                                    sequencePointForScope = sequencePoint;
                                    break;
                                }
                            }
                        }

                        scopeLocals.Add(new DatadogLocal
                        {
                            Name = constantName,
                            Type = constantType,
                            Index = -1,
                            Line = sequencePointForScope == default ? UnknownLocalLine : sequencePointForScope.StartLine
                        });
                    }

                    datadogScop.Locals = scopeLocals.ToImmutable();
                    localScopes.Add(datadogScop);
                }

                if (localScopes.Count == 0 && searchMoveNext)
                {
                    var moveNext = GetMoveNextMethod(method);
                    return GetLocalSymbols(moveNext.RowId, sequencePoints, false);
                }
            }

            return localScopes?.ToImmutable();
        }

        internal bool HasMethodBody(int methodRid)
        {
            var method = GetMethodDef(methodRid);
            if (method.RelativeVirtualAddress == 0)
            {
                // Method has no RVA (typically abstract or extern method)
                return false;
            }

            MethodBodyBlock methodBody = _peReader.GetMethodBody(method.RelativeVirtualAddress);
            return methodBody.Size > 1;
        }

        internal bool HasSequencePoints(int methodRid)
        {
            return GetMethodSequencePoints(methodRid, false)?.Length > 0;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DnlibPdbReader?.Dispose();
            _peReader?.Dispose();
            _dnlibModule?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        internal record struct DatadogSequencePoint
        {
            internal int StartLine;
            internal int EndLine;
            internal int StartColumn;
            internal int EndColumn;
            internal int Offset;
            internal bool IsHidden;
            internal string? URL;
        }

        internal record struct CustomDebugInfoAsyncAndClosure
        {
            internal bool LocalSlot;
            internal bool EncLambdaAndClosureMap;
            internal bool StateMachineHoistedLocal;
            internal int StateMachineKickoffMethodRid;

            internal bool IsNil => LocalSlot == false && EncLambdaAndClosureMap == false && StateMachineHoistedLocal == false;
        }

        internal record struct DatadogLocal
        {
            internal string Name;
            internal string Type;
            internal int Index;
            internal int Line;
        }

        internal struct LocalScope
        {
            internal DatadogSequencePoint SequencePoint;
            internal ImmutableArray<DatadogLocal> Locals;
        }
    }
}

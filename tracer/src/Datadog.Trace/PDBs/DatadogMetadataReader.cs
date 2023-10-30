// <copyright file="DatadogMetadataReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.Trace.Debugger.Symbols;
using Datadog.Trace.Logging;
using Datadog.Trace.VendoredMicrosoftCode.System.Buffers;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.Metadata;
using Datadog.Trace.VendoredMicrosoftCode.System.Reflection.PortableExecutable;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Dss;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Managed;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using Datadog.Trace.Vendors.dnlib.IO;
using SymbolReaderFactory = Datadog.Trace.Vendors.dnlib.DotNet.Pdb.SymbolReaderFactory;

namespace Datadog.Trace.Pdb
{
    /// <summary>
    /// Reads metadata as well as both Windows and Portable PDBs.
    /// Note: reading Windows PDBs is only supported on Windows.
    /// </summary>
    internal class DatadogMetadataReader : IDisposable
    {
        internal const int RidMask = 0x00FFFFFF;
        internal static readonly Guid SourceLink = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        internal static readonly Guid EncLambdaAndClosureMap = new("A643004C-0240-496F-A783-30D64F4979DE");
        internal static readonly Guid EncLocalSlotMap = new("755F52A8-91C5-45BE-B4B8-209571E552BD");
        // internal static readonly Guid AsyncMethodSteppingInformationBlob = new("54FD2AC5-E925-401A-9C2A-F94F171072F8");
        internal static readonly Guid StateMachineHoistedLocalScopes = new("6DA9A61E-F8C7-4874-BE62-68BC5630DF71");
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DatadogMetadataReader>();
        private readonly PEReader? _peReader;
        private readonly ModuleDefMD? _dnlibModule;
        private readonly bool _isDnlibPdbReader;
        private bool _disposed;

        private DatadogMetadataReader(PEReader peReader, MetadataReader metadataReader, MetadataReader? pdbReader, string? pdbFullPath, SymbolReader? dnlibPdbReader, ModuleDefMD? dnlibModule)
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

        internal string? PdbFullPath { get; }

        internal MetadataReader MetadataReader { get; }

        internal MetadataReader? PdbReader { get; }

        internal SymbolReader? DnlibPdbReader { get; }

        internal bool IsPdbExist { get; set; }

        public static DatadogMetadataReader CreatePdbReader(Assembly assembly)
        {
            // For metadata we are always using Reflection.Metadata
            // For PDB, Reflection.Metadata for portable and embedded PDB and dnlib for windows PDB
            var peReader = new PEReader(File.OpenRead(assembly.Location), PEStreamOptions.PrefetchMetadata | PEStreamOptions.PrefetchEntireImage);
            MetadataReader metadataReader = peReader.GetMetadataReader(MetadataReaderOptions.Default);
            MetadataReader? pdbReader;
            if (peReader.TryOpenAssociatedPortablePdb(assembly.Location, File.OpenRead, out var metadataReaderProvider, out var pdbPath))
            {
                pdbReader = metadataReaderProvider!.GetMetadataReader(MetadataReaderOptions.Default, MetadataStringDecoder.DefaultUTF8);
                return new DatadogMetadataReader(peReader, metadataReader, pdbReader, pdbPath, null, null);
            }

            if (!TryFindPdbFile(assembly.Location, out var pdbFullPath))
            {
                return new DatadogMetadataReader(peReader, metadataReader, null, null, null, null);
            }

            var module = ModuleDefMD.Load(assembly.ManifestModule, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
            var pdbStream = DataReaderFactoryFactory.Create(pdbFullPath, false);
            var dnlibReader = SymbolReaderFactory.Create(ModuleCreationOptions.DefaultPdbReaderOptions, module.Metadata, pdbStream);
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

        public string? GetSourceLinkJsonDocument()
        {
            if (_isDnlibPdbReader)
            {
                var sourceLink = _dnlibModule?.CustomDebugInfos.OfType<PdbSourceLinkCustomDebugInfo>().FirstOrDefault();
                return sourceLink == null ? null : Encoding.UTF8.GetString(sourceLink.FileBlob);
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

        public DatadogSequencePoint[] GetMethodSequencePoints(int methodMetadataToken)
        {
            if (_isDnlibPdbReader)
            {
                var rid = MDToken.ToRID(methodMetadataToken);
                var mdMethod = _dnlibModule?.ResolveMethod(rid);
                var symbolMethod = GetDnlibSymbolMethodOfAsyncMethodOrDefault(mdMethod) ?? DnlibPdbReader?.GetMethod(mdMethod, version: 1);
                if (symbolMethod == null)
                {
                    return Array.Empty<DatadogSequencePoint>();
                }

                var dnlibSp = ArrayPool<DatadogSequencePoint>.Shared.Rent(symbolMethod.SequencePoints.Count);
                for (int i = 0; i < symbolMethod.SequencePoints.Count; i++)
                {
                    var sp = symbolMethod.SequencePoints[i];
                    dnlibSp[i] = new DatadogSequencePoint
                    {
                        StartLine = sp.Line,
                        EndLine = sp.EndLine,
                        StartColumn = sp.Column,
                        EndColumn = sp.EndColumn,
                        Offset = sp.Offset,
                        IsHidden = sp.IsHidden(),
                        URL = sp.Document.URL
                    };
                }
            }

            if (PdbReader != null)
            {
                var def = MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(methodMetadataToken & RidMask));
                if (!def.Handle.IsNil)
                {
                    MethodDebugInformation methodDebugInformation = PdbReader.GetMethodDebugInformation(def.Handle.ToDebugInformationHandle());
                    if (methodDebugInformation.SequencePointsBlob.IsNil)
                    {
                        return Array.Empty<DatadogSequencePoint>();
                    }

                    return methodDebugInformation.GetSequencePoints().
                                                  Select(sp => new DatadogSequencePoint
                                                  {
                                                      StartLine = sp.StartLine,
                                                      EndLine = sp.EndLine,
                                                      StartColumn = sp.StartColumn,
                                                      EndColumn = sp.EndColumn,
                                                      Offset = sp.Offset,
                                                      IsHidden = sp.IsHidden,
                                                      URL = GetDocumentName(sp.Document)
                                                  }).ToArray();
                }
            }

            return Array.Empty<DatadogSequencePoint>();

            string? GetDocumentName(DocumentHandle doc)
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
        }

        private SymbolMethod? GetDnlibSymbolMethodOfAsyncMethodOrDefault(MethodDef? mdMethod)
        {
            // Determine if the given mdMethod is a kickoff of a state machine(aka an async method)
            // The first step is to check if the method is decorated with the attribute `AsyncStateMachine`
            var stateMachineAttributeFullName = typeof(AsyncStateMachineAttribute).FullName;
            var stateMachineAttributeOfMdMethod = mdMethod?.CustomAttributes?.FirstOrDefault(ca => ca.TypeFullName == stateMachineAttributeFullName);
            // Grab the generated inner type state machine from the argument given by the compiler to the AsyncStateMachineAttribute
            var stateMachineInnerTypeTypeDefOrRefSig = (stateMachineAttributeOfMdMethod?.ConstructorArguments.FirstOrDefault().Value as TypeDefOrRefSig);
            // Grab the MoveNext from inside the stateMachine and from there the corresponding debugInfo
            var moveNextDebugInfo = stateMachineInnerTypeTypeDefOrRefSig?.TypeDefOrRef?.ResolveTypeDef()?.FindMethod("MoveNext")?.CustomDebugInfos.OfType<PdbAsyncMethodCustomDebugInfo>().FirstOrDefault();
            var breakpointMethod = moveNextDebugInfo?.StepInfos.FirstOrDefault();

            if (breakpointMethod?.BreakpointMethod == null)
            {
                return null;
            }

            return DnlibPdbReader?.GetMethod(breakpointMethod.Value.BreakpointMethod, version: 1);
        }

        public int? GetContainingMethodTokenAndOffset(string filePath, int line, int? column, out int? byteCodeOffset)
        {
            byteCodeOffset = null;

            if (_isDnlibPdbReader)
            {
                return DnlibPdbReader switch
                {
                    PortablePdbReader portablePdbReader => portablePdbReader.GetContainingMethod(filePath, line, column, out byteCodeOffset)?.Token,
                    PdbReader managedPdbReader => managedPdbReader.GetContainingMethod(filePath, line, column, out byteCodeOffset)?.Token,
                    SymbolReaderImpl symUnmanagedReader => symUnmanagedReader.GetContainingMethod(filePath, line, column, out byteCodeOffset)?.Token,
                    _ => throw new ArgumentOutOfRangeException(nameof(filePath), $"Reader type {DnlibPdbReader!.GetType().FullName} is not supported")
                };
            }

            if (PdbReader != null)
            {
                // todo: check document RID!!
                foreach (MethodDefinitionHandle methodDefinitionHandle in MetadataReader.MethodDefinitions)
                {
                    // Get the method debug information
                    MethodDebugInformation methodDebugInformation = PdbReader.GetMethodDebugInformation(methodDefinitionHandle);

                    // Get the sequence points for the method
                    foreach (VendoredMicrosoftCode.System.Reflection.Metadata.SequencePoint sequencePoint in methodDebugInformation.GetSequencePoints())
                    {
                        // Check if the line and column match
                        if (sequencePoint.StartLine <= line && line <= sequencePoint.EndLine
                                                            && sequencePoint.StartColumn <= column && column <= sequencePoint.EndColumn)
                        {
                            byteCodeOffset = sequencePoint.Offset;
                            return methodDefinitionHandle.RowId;
                        }
                    }
                }
            }

            return null;
        }

        public string[]? GetDocuments()
        {
            if (_isDnlibPdbReader)
            {
                var dnlibDocs = ArrayPool<string>.Shared.Rent(DnlibPdbReader!.Documents.Count);
                for (int i = 0; i < DnlibPdbReader.Documents.Count; i++)
                {
                    dnlibDocs[i] = DnlibPdbReader.Documents[i].URL;
                }

                return dnlibDocs;
            }

            if (PdbReader != null)
            {
                int index = 0;
                var docs = ArrayPool<string>.Shared.Rent(PdbReader.Documents.Count);
                foreach (var document in PdbReader.Documents)
                {
                    docs[index] = PdbReader.GetString(PdbReader.GetDocument(document).Name);
                    index++;
                }

                return docs;
            }

            return null;
        }

        internal string[]? GetLocalVariableNamesOrDefault(int methodToken, int localVariablesCount)
        {
            if (_isDnlibPdbReader)
            {
                return GetDnlibLocalVariableNamesOrDefault(methodToken, localVariablesCount);
            }

            if (PdbReader != null)
            {
                return GetLocalVariableNames(methodToken);
            }

            return null;
        }

        private SymbolMethod? GetDnlibSymbolMethod(int methodMetadataToken)
        {
            var rid = MDToken.ToRID(methodMetadataToken);
            var mdMethod = _dnlibModule?.ResolveMethod(rid);
            return GetDnlibSymbolMethodOfAsyncMethodOrDefault(mdMethod) ?? DnlibPdbReader?.GetMethod(mdMethod, version: 1);
        }

        private string[]? GetDnlibLocalVariableNamesOrDefault(int methodToken, int localVariablesCount)
        {
            if (GetDnlibSymbolMethod(methodToken) is not { } symbolMethod)
            {
                return null;
            }

            var localVariables = symbolMethod.GetLocalVariables();
            var localNames = new string[localVariablesCount];
            foreach (var local in localVariables)
            {
                if (local.Attributes.HasFlag(PdbLocalAttributes.DebuggerHidden))
                {
                    continue;
                }

                if (local.Index > localVariablesCount)
                {
                    // PDB information is inconsistent with the locals that are actually in the metadata.
                    // This might be caused by code obfuscation tools that try to remove/modify locals, and neglect to update the PDB.
                    // We'll simply ignore these additional locals in the hope that things will work out for the best.
                    continue;
                }

                localNames[local.Index] = local.Name;
            }

            return localNames;
        }

        private string[]? GetLocalVariableNames(int methodToken)
        {
            var method = MetadataReader.GetMethodDefinition(MethodDefinitionHandle.FromRowId(methodToken));
            int localsCount = 0;
            var methodLocalsCount = GetLocalVariablesCount(method);
            if (methodLocalsCount == 0)
            {
                return null;
            }

            var names = ArrayPool<string>.Shared.Rent(methodLocalsCount);
            var signature = GetLocalSignature(method);
            if (signature == null)
            {
                return null;
            }

            foreach (var scopeHandle in MetadataReader.GetLocalScopes(method.Handle.ToDebugInformationHandle()))
            {
                var localScope = MetadataReader.GetLocalScope(scopeHandle);
                foreach (var localVarHandle in localScope.GetLocalVariables())
                {
                    var local = MetadataReader.GetLocalVariable(localVarHandle);
                    if (local.Index > methodLocalsCount || string.IsNullOrEmpty(MetadataReader.GetString(local.Name)))
                    {
                        continue;
                    }

                    names[localsCount] = MetadataReader.GetString(local.Name);
                    localsCount++;
                }
            }

            return names;
        }

        internal CustomDebugInfoAsyncAndClosure GetAsyncAndClosureCustomDebugInfo(MethodDefinitionHandle methodHandle)
        {
            if (methodHandle.IsNil || PdbReader == null)
            {
                return default;
            }

            CustomDebugInfoAsyncAndClosure cdiAsyncAndClosure = default;
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
                    cdiAsyncAndClosure.StateMachineKickoffMethod = methodDebugInformation.GetStateMachineKickoffMethod();
                }
            }

            return cdiAsyncAndClosure;
        }

        internal CustomDebugInfoAsyncAndClosure GetClosureCustomDebugInfo(MethodDefinitionHandle methodHandle)
        {
            if (methodHandle.IsNil || PdbReader == null)
            {
                return default;
            }

            CustomDebugInfoAsyncAndClosure cdiAsyncAndClosure = default;
            foreach (var handle in PdbReader.GetCustomDebugInformation(methodHandle))
            {
                if (handle.IsNil)
                {
                    continue;
                }

                var customDebugInformation = PdbReader.GetCustomDebugInformation(handle);
                var cdiGuid = PdbReader.GetGuid(customDebugInformation.Kind);
                if (cdiGuid == EncLambdaAndClosureMap)
                {
                    // https://github.com/dotnet/runtime/blob/main/docs/design/specs/PortablePdb-Metadata.md#edit-and-continue-lambda-and-closure-map-c-and-vb-compilers
                    cdiAsyncAndClosure.EncLambdaAndClosureMap = true;
                    var blob = customDebugInformation.Value;
                    var reader = PdbReader.GetBlobReader(blob);
                    int methodOrdinal = reader.ReadCompressedInteger();
                    int syntaxOffsetBaseline = reader.ReadCompressedInteger();
                    int closureCount = reader.ReadCompressedInteger();
                    int syntaxOffset = reader.ReadCompressedInteger();
                    int closureOrdinal = reader.ReadCompressedInteger();
                }
            }

            return cdiAsyncAndClosure;
        }

        internal string? ReadKickoffMethodNameFromBlob(BlobReader blobReader, MethodDefinitionHandle methodHandle)
        {
            if (blobReader.RemainingBytes == 0)
            {
                return null;
            }

            uint catchHandlerOffset = blobReader.ReadUInt32() - 1;

            while (blobReader.Offset < blobReader.Length)
            {
                var yieldInstr = blobReader.ReadUInt32();
                uint resumeOffset = blobReader.ReadUInt32();
                var moveNextRid = blobReader.ReadCompressedInteger();
                if (moveNextRid == methodHandle.RowId)
                {
                    // Get the method name from the MetadataReader
                    string methodName = MetadataReader.GetString(MetadataReader.GetMethodDefinition(methodHandle).Name);

                    return methodName;
                }
            }

            return null;
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
            internal MethodDefinitionHandle StateMachineKickoffMethod;

            internal bool IsNil => LocalSlot == false && EncLambdaAndClosureMap == false && StateMachineHoistedLocal == false;
        }

        internal struct DatadogLocal
        {
            internal string Name;
            internal string Type;

            public DatadogLocal(string localName, string type)
            {
                Name = localName;
                Type = type;
            }
        }
    }
}

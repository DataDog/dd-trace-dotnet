// <copyright file="DatadogPdbReader.cs" company="Datadog">
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
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.System.Reflection.Metadata;
using Datadog.System.Reflection.PortableExecutable;
using Datadog.Trace.Logging;
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
    /// Reads both Windows and Portable PDBs.
    /// Note: reading Windows PDBs is only supported on Windows.
    /// </summary>
    internal class DatadogPdbReader : IDisposable
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor<DatadogPdbReader>();
        private bool _disposed;

        private DatadogPdbReader(SymbolReader symbolReader, MetadataReader metadataReader, string pdbFullPath)
        {
            DnlibSymbolReader = symbolReader;
            MetadataReader = metadataReader;
            PdbFullPath = pdbFullPath;
        }

        private DatadogPdbReader(PEReader peReader, MetadataReader metadataReader, string? pdbFullPath)
        {
            PEReader = peReader;
            MetadataReader = metadataReader;
            PdbFullPath = pdbFullPath;
        }

        ~DatadogPdbReader() => Dispose(false);

        internal string? PdbFullPath { get; }

        internal MetadataReader MetadataReader { get; }

        internal PEReader? PEReader { get; }

        internal SymbolReader? DnlibSymbolReader { get; }

        public static DatadogPdbReader? CreatePdbReader(Assembly assembly)
        {
            var peReader = new Datadog.System.Reflection.PortableExecutable.PEReader(File.OpenRead(assembly.Location), PEStreamOptions.PrefetchMetadata);
            MetadataReader? metadataReader;
            if (peReader.TryOpenAssociatedPortablePdb(assembly.Location, File.OpenRead, out var metadataReaderProvider, out var pdbPath))
            {
                metadataReader = metadataReaderProvider!.GetMetadataReader(MetadataReaderOptions.Default, MetadataStringDecoder.DefaultUTF8);
                return new DatadogPdbReader(peReader, metadataReader, pdbPath);
            }

            var module = ModuleDefMD.Load(assembly.ManifestModule, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });

            if (!TryFindPdbFile(assembly.Location, out var pdbFullPath))
            {
                return null;
            }

            var pdbStream = DataReaderFactoryFactory.Create(pdbFullPath, false);
            var dnlibReader = SymbolReaderFactory.Create(ModuleCreationOptions.DefaultPdbReaderOptions, module.Metadata, pdbStream);
            if (dnlibReader == null)
            {
                return null;
            }

            dnlibReader.Initialize(module);
            module.LoadPdb(dnlibReader);
            metadataReader = peReader.GetMetadataReader(MetadataReaderOptions.Default);
            return new DatadogPdbReader(dnlibReader, metadataReader, pdbFullPath);
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

        public string? GetSourceLinkJsonDocument()
        {
            var sourceLink = Module.CustomDebugInfos.OfType<PdbSourceLinkCustomDebugInfo>().FirstOrDefault();
            return sourceLink == null ? null : Encoding.UTF8.GetString(sourceLink.FileBlob);
        }

        public SymbolMethod? GetMethodSymbolInfoOrDefault(int methodMetadataToken)
        {
            var rid = MDToken.ToRID(methodMetadataToken);
            var mdMethod = Module.ResolveMethod(rid);
            return GetSymbolMethodOfAsyncMethodOrDefault(mdMethod) ?? _symbolReader.GetMethod(mdMethod, version: 1);
        }

        private SymbolMethod? GetSymbolMethodOfAsyncMethodOrDefault(MethodDef mdMethod)
        {
            // Determine if the given mdMethod is a kickoff of a state machine (aka an async method)
            // The first step is to check if the method is decorated with the attribute `AsyncStateMachine`
            var stateMachineAttributeFullName = typeof(AsyncStateMachineAttribute).FullName;
            var stateMachineAttributeOfMdMethod = mdMethod.CustomAttributes?.FirstOrDefault(ca => ca.TypeFullName == stateMachineAttributeFullName);
            // Grab the generated inner type state machine from the argument given by the compiler to the AsyncStateMachineAttribute
            var stateMachineInnerTypeTypeDefOrRefSig = (stateMachineAttributeOfMdMethod?.ConstructorArguments.FirstOrDefault().Value as TypeDefOrRefSig);
            // Grab the MoveNext from inside the stateMachine and from there the corresponding debugInfo
            var moveNextDebugInfo = stateMachineInnerTypeTypeDefOrRefSig?.TypeDefOrRef?.ResolveTypeDef()?.FindMethod("MoveNext")?.CustomDebugInfos.OfType<PdbAsyncMethodCustomDebugInfo>().FirstOrDefault();
            var breakpointMethod = moveNextDebugInfo?.StepInfos.FirstOrDefault();

            if (breakpointMethod?.BreakpointMethod == null)
            {
                return null;
            }

            return _symbolReader.GetMethod(breakpointMethod.Value.BreakpointMethod, version: 1);
        }

        public SymbolMethod GetContainingMethodAndOffset(string filePath, int line, int? column, out int? bytecodeOffset)
        {
            return _symbolReader switch
            {
                PortablePdbReader portablePdbReader => portablePdbReader.GetContainingMethod(filePath, line, column, out bytecodeOffset),
                PdbReader managedPdbReader => managedPdbReader.GetContainingMethod(filePath, line, column, out bytecodeOffset),
                SymbolReaderImpl symUnmanagedReader => symUnmanagedReader.GetContainingMethod(filePath, line, column, out bytecodeOffset),
                _ => throw new ArgumentOutOfRangeException(nameof(filePath), $"Reader type {_symbolReader.GetType().FullName} is not supported")
            };
        }

        public IList<SymbolDocument> GetDocuments()
        {
            return _symbolReader.Documents;
        }

        internal string[]? GetLocalVariableNamesOrDefault(int methodToken, int localVariablesCount)
        {
            if (GetMethodSymbolInfoOrDefault(methodToken) is not { } symbolMethod)
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                Module.Dispose();
            }

            _symbolReader.Dispose();
            _disposed = true;
        }
    }
}

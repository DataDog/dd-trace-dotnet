// <copyright file="DatadogPdbReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
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
        private readonly SymbolReader _symbolReader;
        private readonly ModuleDefMD _module;

        private DatadogPdbReader(SymbolReader symbolReader, ModuleDefMD module, string pdbFullPath)
        {
            PdbFullPath = pdbFullPath;
            _symbolReader = symbolReader;
            _module = module;
        }

        internal string PdbFullPath { get; }

        public static DatadogPdbReader CreatePdbReader(Assembly assembly)
        {
            var module = ModuleDefMD.Load(assembly.ManifestModule, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
            if (!TryFindPdbFile(assembly, out var pdbFullPath))
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
            return new DatadogPdbReader(dnlibReader, module, pdbFullPath);
        }

        private static bool TryFindPdbFile(Assembly assembly, [NotNullWhen(true)] out string pdbFullPath)
        {
            try
            {
                string pdbInSameFolder = Path.ChangeExtension(assembly.Location, "pdb");

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
                string fileName = Path.GetFileName(pdbInSameFolder);
                string applicationDirectory = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
                if (applicationDirectory != null)
                {
                    var pdbInAppDirectory = Directory.EnumerateFiles(applicationDirectory, fileName, SearchOption.AllDirectories).FirstOrDefault();
                    if (pdbInAppDirectory != null)
                    {
                        pdbFullPath = pdbInAppDirectory;
                        return true;
                    }
                }
#endif

            }
            catch (Exception e)
            {
                Logger.Error(e, "Error while trying to find PDB file for {Assembly} ({Location})", assembly, assembly.Location);
                pdbFullPath = null;
                return false;
            }

            pdbFullPath = null;
            return false;
        }

        public string GetSourceLinkJsonDocument()
        {
            var sourceLink = _module.CustomDebugInfos.OfType<PdbSourceLinkCustomDebugInfo>().FirstOrDefault();
            return sourceLink == null ? null : Encoding.UTF8.GetString(sourceLink.FileBlob);
        }

        public SymbolMethod ReadMethodSymbolInfo(int methodMetadataToken)
        {
            var rid = MDToken.ToRID(methodMetadataToken);
            var mdMethod = _module.ResolveMethod(rid);
            return TryGetSymbolMethodOfAsyncMethod(mdMethod, out var symbolMethod) ?
                       symbolMethod :
                       _symbolReader.GetMethod(mdMethod, version: 1);
        }

        private bool TryGetSymbolMethodOfAsyncMethod(MethodDef mdMethod, out SymbolMethod symbolMethod)
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
                symbolMethod = null;
                return false;
            }

            symbolMethod = _symbolReader.GetMethod(breakpointMethod?.BreakpointMethod, version: 1);
            return true;
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

        public void Dispose()
        {
            _symbolReader.Dispose();
            _module.Dispose();
        }
    }
}

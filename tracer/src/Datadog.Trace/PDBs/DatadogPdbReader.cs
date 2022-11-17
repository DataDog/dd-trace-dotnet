// <copyright file="DatadogPdbReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.MD;
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
        private readonly SymbolReader _symbolReader;
        private readonly ModuleDefMD _module;

        private DatadogPdbReader(SymbolReader symbolReader, ModuleDefMD module)
        {
            _symbolReader = symbolReader;
            _module = module;
        }

        public static DatadogPdbReader CreatePdbReader(Assembly assembly)
        {
            var assemblyFullPath = assembly.Location;
            var module = ModuleDefMD.Load(assembly.ManifestModule);
            var pdbFullPath = Path.ChangeExtension(assemblyFullPath, "pdb");

            if (!File.Exists(pdbFullPath))
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
            return new DatadogPdbReader(dnlibReader, module);
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

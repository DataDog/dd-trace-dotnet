// <copyright file="DatadogPdbReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.MD;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Dss;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Managed;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using Datadog.Trace.Vendors.dnlib.IO;
using SymbolReaderFactory = Datadog.Trace.Vendors.dnlib.DotNet.Pdb.SymbolReaderFactory;

namespace Datadog.Trace.PDBs
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
            string assemblyFullPath = assembly.Location;
            var module = ModuleDefMD.Load(assembly.ManifestModule);
            string pdbFullPath = Path.ChangeExtension(assemblyFullPath, "pdb");
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
            return _symbolReader.GetMethod(mdMethod, version: 1);
        }

        public SymbolMethod GetContainingMethodAndOffset(string filePath, int line, int column, out int? bytecodeOffset)
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

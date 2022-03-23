// <copyright file="DatadogPdbReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.MD;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;
using Datadog.Trace.Vendors.dnlib.IO;

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

        public static DatadogPdbReader CreatePdbReader(string assemblyFullPath)
        {
            var module = ModuleDefMD.Load(File.ReadAllBytes(assemblyFullPath));
            var metadata = MetadataFactory.Load(assemblyFullPath, CLRRuntimeReaderKind.CLR);
            string pdbFullPath = Path.ChangeExtension(assemblyFullPath, "pdb");
            var pdbStream = DataReaderFactoryFactory.Create(pdbFullPath, false);
            var options = new ModuleCreationOptions(CLRRuntimeReaderKind.CLR);
            var dnlibReader = SymbolReaderFactory.Create(options.PdbOptions, metadata, pdbStream);
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

        public void Dispose()
        {
            _symbolReader.Dispose();
            _module.Dispose();
        }
    }
}

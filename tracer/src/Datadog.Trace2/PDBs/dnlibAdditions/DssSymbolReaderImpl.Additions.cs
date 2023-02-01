// <copyright file="DssSymbolReaderImpl.Additions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

#pragma warning disable SA1300
namespace Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Dss
{
    internal sealed partial class SymbolReaderImpl
    {
        internal SymbolMethod GetContainingMethod(string documentUrl, int line, int? column, out int? bytecodeOffset)
        {
            reader.GetDocument(documentUrl, language: default, languageVendor: default, documentType: default, out var document);
            reader.GetMethodFromDocumentPosition(document, (uint)line, (uint)(column ?? 0), out var method);
            method.GetOffset(document, (uint)line, (uint)(column ?? 0), out var offset);
            method.GetToken(out var token);

            bytecodeOffset = (int?)offset;
            return new SymbolMethodImpl(this, method);
        }
    }
}

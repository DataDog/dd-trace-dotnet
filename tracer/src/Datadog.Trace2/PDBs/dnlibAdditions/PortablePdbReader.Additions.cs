// <copyright file="PortablePdbReader.Additions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

#pragma warning disable SA1300
namespace Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Portable;

internal sealed partial class PortablePdbReader
{
    internal SymbolMethod GetContainingMethod(string documentUrl, int line, int? column, out int? bytecodeOffset)
    {
        foreach (var methodRid in GetMethodRIDsContainedInDocument(documentUrl))
        {
            var method = ((ModuleDefMD)module).ResolveMethod(methodRid);
            var symbolMethod = GetMethod(method, version: 1);
            foreach (var sp in symbolMethod.SequencePoints)
            {
                // Check if the file position is within the method's bounds in terms of line number.
                // If the column number is specified, check that too, otherwise it's fine to ignore it.
                if (sp.Line <= line && sp.EndLine >= line &&
                    (column.HasValue == false || (sp.Column <= column && sp.EndColumn >= column)))
                {
                    bytecodeOffset = sp.Offset;
                    return symbolMethod;
                }
            }
        }

        bytecodeOffset = null;
        return null;
    }

    private IEnumerable<uint> GetMethodRIDsContainedInDocument(string documentUrl)
    {
        var requestedDocumentRid = GetDocumentRid(documentUrl);

        for (uint methodRid = 1; methodRid <= pdbMetadata.TablesStream.MethodDebugInformationTable.Rows; methodRid++)
        {
            if (!pdbMetadata.TablesStream.TryReadMethodDebugInformationRow(methodRid, out var row))
            {
                continue;
            }

            if (row.SequencePoints == 0)
            {
                continue;
            }

            if (row.Document == requestedDocumentRid)
            {
                yield return methodRid;
            }
        }
    }

    private int GetDocumentRid(string documentUrl)
    {
        var docTbl = pdbMetadata.TablesStream.DocumentTable;
        var docs = new SymbolDocument[docTbl.Rows];
        var nameReader = new DocumentNameReader(pdbMetadata.BlobStream);
        for (var i = 1; i <= docs.Length; i++)
        {
            if (!pdbMetadata.TablesStream.TryReadDocumentRow((uint)i, out var row))
            {
                continue;
            }

            var url = nameReader.ReadDocumentName(row.Name);
            if (url == documentUrl)
            {
                return i;
            }
        }

        return -1;
    }
}

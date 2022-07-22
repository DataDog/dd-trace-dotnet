// <copyright file="PdbReader.Additions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

#pragma warning disable SA1300
namespace Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Managed
{
    internal partial class PdbReader
    {
        internal SymbolMethod GetContainingMethod(string documentUrl, int line, int column, out int? bytecodeOffset)
        {
            var candidateSequencePoints = new List<ResolvedSequencePoint>();
            foreach (var function in functions.Values)
            {
                var methodIsInDocument = function.SequencePoints.Any(s => s.Document.URL == documentUrl);

                if (methodIsInDocument)
                {
                    var method = GetMethod(((ModuleDefMD)module).ResolveMethod(MDToken.ToRID(function.token)), version: 1);
                    foreach (var sp in method.SequencePoints)
                    {
                        if (sp.Line <= line && sp.EndLine >= line &&
                            sp.Column >= column && sp.EndColumn >= column)
                        {
                            candidateSequencePoints.Add(new ResolvedSequencePoint(method, sp));
                        }
                    }
                }
            }

            var matchingSequencePoint = candidateSequencePoints.FirstOrDefault();
            bytecodeOffset = matchingSequencePoint.SequencePoint.Offset;
            return matchingSequencePoint.Method; // TODO - find shortest sequence point
        }

        private record ResolvedSequencePoint
        {
            public ResolvedSequencePoint(SymbolMethod method, SymbolSequencePoint sequencePoint)
            {
                Method = method;
                SequencePoint = sequencePoint;
            }

            public SymbolMethod Method { get; }

            public SymbolSequencePoint SequencePoint { get; }
        }
    }
}

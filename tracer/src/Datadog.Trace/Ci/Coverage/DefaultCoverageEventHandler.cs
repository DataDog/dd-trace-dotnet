// <copyright file="DefaultCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Coverage.Models;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class DefaultCoverageEventHandler : CoverageEventHandler
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultCoverageEventHandler));

    protected override object? OnSessionFinished(ModuleValue[] modules)
    {
        const int HIDDEN = 0xFEEFEE;
        Dictionary<string, FileCoverage>? fileDictionary = null;
        foreach (var moduleValue in modules)
        {
            var moduleDef = MethodSymbolResolver.Instance.GetModuleDef(moduleValue.Module);
            if (moduleDef is null)
            {
                continue;
            }

            for (var i = 0; i < moduleValue.Types.Length; i++)
            {
                var currentType = moduleValue.Types[i];
                if (currentType is null)
                {
                    continue;
                }

                var typeDef = moduleDef.Types[i];

                for (var j = 0; j < currentType.Methods.Length; j++)
                {
                    var currentMethod = currentType.Methods[j];
                    if (currentMethod is null)
                    {
                        continue;
                    }

                    var methodDef = typeDef.Methods[j];
                    if (methodDef.HasBody && methodDef.Body.HasInstructions)
                    {
                        var seqPoints = new List<SequencePoint>(methodDef.Body.Instructions.Count);
                        foreach (var instruction in methodDef.Body.Instructions)
                        {
                            if (instruction.SequencePoint is null ||
                                instruction.SequencePoint.StartLine == HIDDEN ||
                                instruction.SequencePoint.EndLine == HIDDEN)
                            {
                                continue;
                            }

                            seqPoints.Add(instruction.SequencePoint);
                        }

                        for (var x = 0; x < currentMethod.SequencePoints.Length; x++)
                        {
                            var repInSeqPoints = currentMethod.SequencePoints[x];
                            if (repInSeqPoints == 0)
                            {
                                continue;
                            }

                            var seqPoint = seqPoints[x];
                            fileDictionary ??= new Dictionary<string, FileCoverage>();
                            if (!fileDictionary.TryGetValue(seqPoint.Document.Url, out var fileCoverage))
                            {
                                fileCoverage = new FileCoverage
                                {
                                    FileName = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(seqPoint.Document.Url, false)
                                };

                                fileDictionary[seqPoint.Document.Url] = fileCoverage;
                            }

                            fileCoverage.Segments.Add(new[] { (uint)seqPoint.StartLine, (uint)seqPoint.StartColumn, (uint)seqPoint.EndLine, (uint)seqPoint.EndColumn, (uint)repInSeqPoints });
                        }
                    }
                }
            }
        }

        if (fileDictionary is null || fileDictionary.Count == 0)
        {
            return null;
        }

        var payload = new CoveragePayload
        {
            Files = fileDictionary.Values.ToList(),
        };

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Coverage payload: {payload}", JsonConvert.SerializeObject(payload));
        }

        return payload;
    }
}

// <copyright file="DefaultCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Logging;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal class DefaultCoverageEventHandler : CoverageEventHandler
{
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultCoverageEventHandler));
    protected static readonly Dictionary<ModuleDef, List<TypeDef>> TypeDefsFromModuleDefs = new();

    protected override void OnSessionStart(CoverageContextContainer context)
    {
    }

    protected override object? OnSessionFinished(CoverageContextContainer context)
    {
        var modules = context.CloseContext();
        const int HIDDEN = 0xFEEFEE;

        Dictionary<string, FileCoverage>? fileDictionary = null;
        foreach (var moduleValue in modules)
        {
            var moduleDef = MethodSymbolResolver.Instance.GetModuleDef(moduleValue.Module);
            if (moduleDef is null)
            {
                continue;
            }

            List<TypeDef>? moduleTypes;
            lock (TypeDefsFromModuleDefs)
            {
                if (!TypeDefsFromModuleDefs.TryGetValue(moduleDef, out moduleTypes))
                {
                    moduleTypes = moduleDef.GetTypes().ToList();
                    TypeDefsFromModuleDefs[moduleDef] = moduleTypes;
                }
            }

            for (var i = 0; i < moduleValue.Methods.Length; i++)
            {
                var currentMethod = moduleValue.Methods[i];
                if (currentMethod is null)
                {
                    continue;
                }

                moduleValue.Metadata.GetMethodsMetadata(i, out var typeIndex, out var methodIndex);
                var typeDef = moduleTypes[typeIndex];
                var methodDef = typeDef.Methods[methodIndex];

                if (methodDef.HasBody && methodDef.Body.HasInstructions && currentMethod.SequencePoints.Length > 0)
                {
                    var seqPoints = new List<SequencePoint>(currentMethod.SequencePoints.Length);
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
                            fileCoverage = new FileCoverage { FileName = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(seqPoint.Document.Url, false) };

                            fileDictionary[seqPoint.Document.Url] = fileCoverage;
                        }

                        fileCoverage.Segments.Add(new[] { (uint)seqPoint.StartLine, (uint)seqPoint.StartColumn, (uint)seqPoint.EndLine, (uint)seqPoint.EndColumn, (uint)repInSeqPoints });
                    }
                }
            }
        }

        if (fileDictionary is null || fileDictionary.Count == 0)
        {
            return null;
        }

        var testCoverage = new TestCoverage
        {
            Files = fileDictionary.Values.ToList(),
        };

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Test Coverage: {Json}", JsonConvert.SerializeObject(testCoverage));
        }

        return testCoverage;
    }
}

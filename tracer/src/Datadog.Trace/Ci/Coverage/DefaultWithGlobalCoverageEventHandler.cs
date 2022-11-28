// <copyright file="DefaultWithGlobalCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Datadog.Trace.Ci.Coverage.Models;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal class DefaultWithGlobalCoverageEventHandler : DefaultCoverageEventHandler
{
    private readonly List<CoverageContextContainer> _coverages = new();

    protected override void OnSessionStart(CoverageContextContainer context)
    {
        _coverages.Add(context);
        base.OnSessionStart(context);
    }

    public void Clear()
    {
        foreach (var coverage in _coverages)
        {
            coverage.Clear();
        }

        _coverages.Clear();
        GlobalContainer.Clear();
    }

    public object? GetCodeCoverage()
    {
        Log.Warning("Global GetCodeCoverage.");

        // Get all ModuleValues
        var lstModulesInstances = new List<ModuleValue>();
        lstModulesInstances.AddRange(GlobalContainer.CloseContext());
        foreach (var coverage in _coverages)
        {
            lstModulesInstances.AddRange(coverage.CloseContext());
            coverage.Clear();
        }

        GlobalContainer.Clear();

        // Process
        var totalSequencePoints = 0L;
        var executedSequencePoints = 0L;
        foreach (var moduleValues in lstModulesInstances.GroupBy(i => i.Module))
        {
            var moduleDef = MethodSymbolResolver.Instance.GetModuleDef(moduleValues.Key);
            if (moduleDef is null)
            {
                continue;
            }

            var moduleMetadata = moduleValues.First().Metadata;
            totalSequencePoints += moduleMetadata.TotalInstructions;
            var totalTypesCount = moduleMetadata.GetTotalTypes();
            for (var i = 0; i < totalTypesCount; i++)
            {
                var typeDef = moduleDef.Types[i];
                var fullName = typeDef.FullName;
                var typeValues = moduleValues
                                .Where(m => m.Types[i] != null)
                                .Select(m => m.Types[i]!)
                                .ToList();
                if (typeValues.Count == 0)
                {
                    Log.Warning("GCov: [Type] {typeName} doesn't got covered", fullName);
                    continue;
                }

                var totalMethodsCount = moduleMetadata.GetTotalMethodsOfType(i);
                for (var j = 0; j < totalMethodsCount; j++)
                {
                    var methodDef = typeDef.Methods[j];
                    var methodName = methodDef.Name;
                    var methodValues = typeValues
                                      .Where(t => t.Methods[j] != null)
                                      .Select(t => t.Methods[j]!)
                                      .ToList();
                    if (methodValues.Count == 0)
                    {
                        Log.Warning("GCov: [Method] {typeName}.{methodName} doesn't got covered", fullName, methodName);
                        continue;
                    }

                    var seqPointsCount = methodValues[0].SequencePoints.Length;
                    for (var seqPointIdx = 0; seqPointIdx < seqPointsCount; seqPointIdx++)
                    {
                        foreach (var methodValue in methodValues)
                        {
                            if (methodValue.SequencePoints[seqPointIdx] != 0)
                            {
                                executedSequencePoints++;
                                break;
                            }
                        }
                    }
                }
            }
        }

        Log.Warning("GCov: Total Sequence Points: {totalSequencePoints}", totalSequencePoints);
        Log.Warning("GCov: Executed Sequence Points: {executedSequencePoints}", executedSequencePoints);
        var coveragePercentage = Math.Round(((double)totalSequencePoints / executedSequencePoints) * 100, 2);
        Log.Warning("GCov: Percentage: {percentage}%", coveragePercentage);

        // ***********************************************************
        const int HIDDEN = 0xFEEFEE;
        Dictionary<string, FileCoverage>? fileDictionary = null;

        // Group by Modules
        foreach (var moduleValue in lstModulesInstances)
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
            Log.Debug("Global GetCodeCoverage: {payload}", JsonConvert.SerializeObject(payload));
        }

        return payload;
    }
}

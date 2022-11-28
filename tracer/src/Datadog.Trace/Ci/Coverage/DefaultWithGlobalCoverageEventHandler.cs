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

    public IReadOnlyList<CoveragePercentages> GetCodeCoveragePercentage()
    {
        // Get all ModuleValues
        var lstModulesInstances = new List<ModuleValue>();
        lstModulesInstances.AddRange(GlobalContainer.CloseContext());
        foreach (var coverage in _coverages)
        {
            lstModulesInstances.AddRange(coverage.CloseContext());
            coverage.Clear();
        }

        GlobalContainer.Clear();

        var lstCoverageValues = new List<CoveragePercentages>();

        // Process
        var totalGlobalSequencePoints = 0L;
        var executedGlobalSequencePoints = 0L;
        foreach (var moduleValues in lstModulesInstances.GroupBy(i => i.Module))
        {
            var moduleDef = MethodSymbolResolver.Instance.GetModuleDef(moduleValues.Key);
            if (moduleDef is null)
            {
                continue;
            }

            var moduleMetadata = moduleValues.First().Metadata;

            var totalModuleSequencePoints = moduleMetadata.TotalInstructions;
            var executedModuleSequencePoints = 0L;
            totalGlobalSequencePoints += totalModuleSequencePoints;

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
                    Log.Debug("GCov: [Type] {typeName} doesn't have coverage", fullName);
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
                        Log.Debug("GCov: [Method] {typeName}.{methodName} doesn't have coverage", fullName, methodName);
                        continue;
                    }

                    var seqPointsCount = methodValues[0].SequencePoints.Length;
                    for (var seqPointIdx = 0; seqPointIdx < seqPointsCount; seqPointIdx++)
                    {
                        foreach (var methodValue in methodValues)
                        {
                            if (methodValue.SequencePoints[seqPointIdx] != 0)
                            {
                                executedGlobalSequencePoints++;
                                executedModuleSequencePoints++;
                                break;
                            }
                        }
                    }
                }
            }

            lstCoverageValues.Add(new CoveragePercentages(
                                      moduleValues.Key.Name,
                                      Math.Round(((double)executedModuleSequencePoints / totalModuleSequencePoints) * 100, 2),
                                      totalModuleSequencePoints,
                                      executedModuleSequencePoints));
        }

        lstCoverageValues.Insert(0, new CoveragePercentages(
                                     string.Empty,
                                     Math.Round(((double)executedGlobalSequencePoints / totalGlobalSequencePoints) * 100, 2),
                                     totalGlobalSequencePoints,
                                     executedGlobalSequencePoints));
        return lstCoverageValues.AsReadOnly();
    }

    public readonly struct CoveragePercentages
    {
        public readonly string ModuleName;
        public readonly double Percentage;
        public readonly double TotalSequencePoints;
        public readonly double ExecutedSequencePoints;

        public CoveragePercentages(string moduleName, double percentage, double totalSequencePoints, double executedSequencePoints)
        {
            ModuleName = moduleName;
            Percentage = percentage;
            TotalSequencePoints = totalSequencePoints;
            ExecutedSequencePoints = executedSequencePoints;

            Log.Debug("**************************************************************");
            Log.Debug("GCov: Module: {moduleName}", moduleName);
            Log.Debug("GCov: Total Sequence Points: {totalSequencePoints}", totalSequencePoints);
            Log.Debug("GCov: Executed Sequence Points: {executedSequencePoints}", executedSequencePoints);
            Log.Debug("GCov: Percentage: {percentage}%", percentage);
        }
    }
}

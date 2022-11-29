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
                var totalTypeSequencePoints = 0L;
                var executedTypeSequencePoints = 0L;

                var totalMethodsCount = moduleMetadata.GetTotalMethodsOfTypeOrDefault(i);
                for (var typeMethodIdx = 0; typeMethodIdx < totalMethodsCount; typeMethodIdx++)
                {
                    totalTypeSequencePoints += moduleMetadata.GetTotalSequencePointsOfMethodOrDefault(i, typeMethodIdx);
                }

                var typeDef = moduleDef.Types[i];
                var fullName = typeDef.FullName;
                var typeValues = moduleValues
                                .Where(m => m.Types[i] != null)
                                .Select(m => m.Types[i]!)
                                .ToList();
                if (typeValues.Count == 0)
                {
                    // Log.Debug("GCov: [Type] {typeName} doesn't have coverage", fullName);
                    continue;
                }

                for (var j = 0; j < totalMethodsCount; j++)
                {
                    var totalMethodSequencePoints = moduleMetadata.GetTotalSequencePointsOfMethod(i, j);
                    var executedMethodSequencePoints = 0L;

                    var methodDef = typeDef.Methods[j];
                    var methodName = methodDef.Name;
                    var methodValues = typeValues
                                      .Where(t => t.Methods[j] != null)
                                      .Select(t => t.Methods[j]!)
                                      .ToList();
                    if (methodValues.Count == 0)
                    {
                        // Log.Debug("GCov: [Method] {typeName}.{methodName} doesn't have coverage", fullName, methodName);
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
                                executedTypeSequencePoints++;
                                executedMethodSequencePoints++;
                                break;
                            }
                        }
                    }

                    lstCoverageValues.Add(new CoveragePercentages(
                                              moduleValues.Key.Name,
                                              fullName,
                                              methodDef.FullName,
                                              totalMethodSequencePoints,
                                              executedMethodSequencePoints));
                }

                lstCoverageValues.Add(new CoveragePercentages(
                                          moduleValues.Key.Name,
                                          fullName,
                                          null,
                                          totalTypeSequencePoints,
                                          executedTypeSequencePoints));
            }

            lstCoverageValues.Add(new CoveragePercentages(
                                      moduleValues.Key.Name,
                                      null,
                                      null,
                                      totalModuleSequencePoints,
                                      executedModuleSequencePoints));
        }

        lstCoverageValues.Insert(0, new CoveragePercentages(
                                     null,
                                     null,
                                     null,
                                     totalGlobalSequencePoints,
                                     executedGlobalSequencePoints));
        return lstCoverageValues.AsReadOnly();
    }

    public readonly struct CoveragePercentages
    {
        public readonly string? ModuleName;
        public readonly string? TypeName;
        public readonly string? MethodName;
        public readonly double Percentage;
        public readonly double TotalSequencePoints;
        public readonly double ExecutedSequencePoints;

        public CoveragePercentages(string? moduleName, string? typeName, string? methodName, double totalSequencePoints, double executedSequencePoints)
        {
            ModuleName = moduleName;
            TypeName = typeName;
            MethodName = methodName;
            Percentage = Math.Round((executedSequencePoints / totalSequencePoints) * 100, 2);
            TotalSequencePoints = totalSequencePoints;
            ExecutedSequencePoints = executedSequencePoints;

            Log.Debug("**************************************************************");
            Log.Debug("   GCov: Module: {moduleName}", moduleName);
            Log.Debug("   GCov: Type: {typeName}", typeName);
            Log.Debug("   GCov: Method: {methodName}", methodName);
            Log.Debug("   GCov: Total Sequence Points: {totalSequencePoints}", totalSequencePoints);
            Log.Debug("   GCov: Executed Sequence Points: {executedSequencePoints}", executedSequencePoints);
            Log.Debug("   GCov: Percentage: {percentage}%", Percentage);
            Log.Debug("**************************************************************");
        }
    }

    public abstract class CoverageInfo
    {
        private double[]? _data;

        [JsonProperty("data", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double[] Data
        {
            get
            {
                if (_data is null)
                {
                    RefreshData();
                }

                return _data!;
            }
        }

        protected void RefreshData()
        {
            ClearData();

            double total = 0L;
            double executed = 0L;

            if (this is FileCoverageInfo { Segments.Count: > 0 } fCovInfo)
            {
                foreach (var segment in fCovInfo.Segments)
                {
                    total++;
                    if (segment[5] != 0)
                    {
                        executed++;
                    }
                }
            }
            else if (this is ComponentCoverageInfo { Files.Count: > 0 } cCovInfo)
            {
                foreach (var file in cCovInfo.Files)
                {
                    var data = file.Data;
                    total += data[1];
                    executed += data[2];
                }
            }
            else if (this is GlobalCoverageInfo { Components.Count: > 0 } gCovInfo)
            {
                foreach (var component in gCovInfo.Components)
                {
                    var data = component.Data;
                    total += data[1];
                    executed += data[2];
                }
            }

            _data = new[] { Math.Round((executed / total) * 100, 2), total, executed };
        }

        protected void ClearData()
        {
            _data = null;

            if (this is ComponentCoverageInfo { Files.Count: > 0 } cCovInfo)
            {
                foreach (var file in cCovInfo.Files)
                {
                    file.ClearData();
                }
            }
            else if (this is GlobalCoverageInfo { Components.Count: > 0 } gCovInfo)
            {
                foreach (var component in gCovInfo.Components)
                {
                    component.ClearData();
                }
            }
        }
    }

    public sealed class GlobalCoverageInfo : CoverageInfo
    {
        public GlobalCoverageInfo()
        {
            Components = new List<ComponentCoverageInfo>();
        }

        [JsonProperty("components", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ComponentCoverageInfo> Components { get; }

        public static GlobalCoverageInfo operator +(GlobalCoverageInfo a, GlobalCoverageInfo b)
        {
            var globalCovInfo = new GlobalCoverageInfo();
            var aComponents = a?.Components ?? Enumerable.Empty<ComponentCoverageInfo>();
            var bComponents = b?.Components ?? Enumerable.Empty<ComponentCoverageInfo>();
            foreach (var componentGroup in aComponents.Concat(bComponents).GroupBy(m => m.Name))
            {
                var componentGroupArray = componentGroup.ToArray();
                if (componentGroupArray.Length == 1)
                {
                    globalCovInfo.Components.Add(componentGroupArray[0]);
                }
                else
                {
                    var res = componentGroupArray[0];
                    for (var i = 1; i < componentGroupArray.Length; i++)
                    {
                        res += componentGroupArray[i];
                    }

                    if (res is not null)
                    {
                        globalCovInfo.Components.Add(res);
                    }
                }
            }

            return globalCovInfo;
        }

        public void Add(ComponentCoverageInfo componentCoverageInfo)
        {
            var previous = Components.SingleOrDefault(m => m.Name == componentCoverageInfo.Name);
            if (previous is not null)
            {
                Components.Remove(previous);
                var res = previous + componentCoverageInfo;
                if (res is not null)
                {
                    Components.Add(res);
                    ClearData();
                }
            }
            else
            {
                Components.Add(componentCoverageInfo);
                ClearData();
            }
        }
    }

    public sealed class ComponentCoverageInfo : CoverageInfo
    {
        public ComponentCoverageInfo(string? name)
        {
            Name = name;
            Files = new List<FileCoverageInfo>();
        }

        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? Name { get; set; }

        [JsonProperty("files", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<FileCoverageInfo> Files { get; }

        public static ComponentCoverageInfo? operator +(ComponentCoverageInfo? a, ComponentCoverageInfo? b)
        {
            if (a is null && b is null)
            {
                return null;
            }
            else if (b is null)
            {
                return a;
            }
            else if (a is null)
            {
                return b;
            }
            else if (a.Name == b.Name)
            {
                var componentCoverageInfo = new ComponentCoverageInfo(a.Name);

                var aFiles = a.Files ?? Enumerable.Empty<FileCoverageInfo>();
                var bFiles = b.Files ?? Enumerable.Empty<FileCoverageInfo>();
                foreach (var filesGroup in aFiles.Concat(bFiles).GroupBy(f => f.Path))
                {
                    var filesGroupArray = filesGroup.ToArray();
                    if (filesGroupArray.Length == 1)
                    {
                        componentCoverageInfo.Files.Add(filesGroupArray[0]);
                    }
                    else
                    {
                        var res = filesGroupArray[0];
                        for (var i = 1; i < filesGroupArray.Length; i++)
                        {
                            res += filesGroupArray[i];
                        }

                        if (res is not null)
                        {
                            componentCoverageInfo.Files.Add(res);
                        }
                    }
                }

                return componentCoverageInfo;
            }

            throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
        }

        public void Add(FileCoverageInfo fileCoverageInfo)
        {
            var previous = Files.SingleOrDefault(m => m.Path == fileCoverageInfo.Path);
            if (previous is not null)
            {
                Files.Remove(previous);
                var res = previous + fileCoverageInfo;
                if (res is not null)
                {
                    Files.Add(res);
                    ClearData();
                }
            }
            else
            {
                Files.Add(fileCoverageInfo);
                ClearData();
            }
        }
    }

    public sealed class FileCoverageInfo : CoverageInfo
    {
        public FileCoverageInfo(string? path)
        {
            Path = path;
            Segments = new List<int[]>();
        }

        [JsonProperty("path", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? Path { get; set; }

        [JsonProperty("segments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<int[]> Segments { get; set; }

        public static FileCoverageInfo? operator +(FileCoverageInfo? a, FileCoverageInfo? b)
        {
            if (a is null && b is null)
            {
                return null;
            }
            else if (b is null)
            {
                return a;
            }
            else if (a is null)
            {
                return b;
            }
            else if (a.Path == b.Path)
            {
                var fcInfo = new FileCoverageInfo(a.Path);
                var aSegments = a.Segments ?? Enumerable.Empty<int[]>();
                var bSegments = b.Segments ?? Enumerable.Empty<int[]>();
                foreach (var segmentsGroup in aSegments.Concat(bSegments).GroupBy(s => new
                         {
                             StartLine = s[0],
                             StartColumn = s[1],
                             EndLine = s[2],
                             EndColumn = s[3]
                         }))
                {
                    fcInfo.Segments.Add(new[]
                    {
                        segmentsGroup.Key.StartLine,
                        segmentsGroup.Key.StartColumn,
                        segmentsGroup.Key.EndLine,
                        segmentsGroup.Key.EndColumn,
                        segmentsGroup.Sum(s => s[5]),
                    });
                }

                return fcInfo;
            }

            throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
        }
    }
}

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

            if (this is MethodCoverageInfo { Segments.Length: > 0 } methodCInfo)
            {
                foreach (var segment in methodCInfo.Segments)
                {
                    total++;
                    if (segment[5] != 0)
                    {
                        executed++;
                    }
                }

                goto set;
            }

            if (this is TypeCoverageInfo { Methods.Count: > 0 } typeCInfo)
            {
                foreach (var method in typeCInfo.Methods)
                {
                    var data = method.Data;
                    total += data[1];
                    executed += data[2];
                }

                goto set;
            }

            if (this is ModuleCoverageInfo { Types.Count: > 0 } modCInfo)
            {
                foreach (var type in modCInfo.Types)
                {
                    var data = type.Data;
                    total += data[1];
                    executed += data[2];
                }

                goto set;
            }

            if (this is GlobalCoverageInfo { Modules.Count: > 0 } globalCInfo)
            {
                foreach (var module in globalCInfo.Modules)
                {
                    var data = module.Data;
                    total += data[1];
                    executed += data[2];
                }

                goto set;
            }

            set:
            _data = new[] { Math.Round((executed / total) * 100, 2), total, executed };
        }

        private void ClearData()
        {
            if (this is MethodCoverageInfo)
            {
                _data = null;
                return;
            }

            if (this is TypeCoverageInfo { Methods.Count: > 0 } typeCInfo)
            {
                foreach (var method in typeCInfo.Methods)
                {
                    method.ClearData();
                }

                return;
            }

            if (this is ModuleCoverageInfo { Types.Count: > 0 } modCInfo)
            {
                foreach (var type in modCInfo.Types)
                {
                    type.ClearData();
                }

                return;
            }

            if (this is GlobalCoverageInfo { Modules.Count: > 0 } globalCInfo)
            {
                foreach (var module in globalCInfo.Modules)
                {
                    module.ClearData();
                }

                return;
            }
        }
    }

    public abstract class NamedCoverageInfo : CoverageInfo
    {
        public NamedCoverageInfo(string name)
        {
            Name = name;
        }

        [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Name { get; }
    }

    public sealed class GlobalCoverageInfo : CoverageInfo
    {
        public GlobalCoverageInfo()
        {
            Modules = new List<ModuleCoverageInfo>();
        }

        [JsonProperty("modules", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<ModuleCoverageInfo> Modules { get; }

        public static GlobalCoverageInfo operator +(GlobalCoverageInfo a, GlobalCoverageInfo b)
        {
            var globalCovInfo = new GlobalCoverageInfo();
            var aModules = a?.Modules ?? Enumerable.Empty<ModuleCoverageInfo>();
            var bModules = b?.Modules ?? Enumerable.Empty<ModuleCoverageInfo>();
            foreach (var moduleGroup in aModules.Concat(bModules).GroupBy(m => m.Name))
            {
                var moduleGroupArray = moduleGroup.ToArray();
                if (moduleGroupArray.Length == 1)
                {
                    globalCovInfo.Modules.Add(moduleGroupArray[0]);
                }
                else
                {
                    var res = moduleGroupArray[0];
                    for (var i = 1; i < moduleGroupArray.Length; i++)
                    {
                        res += moduleGroupArray[i];
                    }

                    globalCovInfo.Modules.Add(res);
                }
            }

            return globalCovInfo;
        }
    }

    public sealed class ModuleCoverageInfo : NamedCoverageInfo
    {
        public ModuleCoverageInfo(string name)
            : base(name)
        {
            Types = new List<TypeCoverageInfo>();
        }

        [JsonProperty("types", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<TypeCoverageInfo> Types { get; }

        public static ModuleCoverageInfo operator +(ModuleCoverageInfo a, ModuleCoverageInfo b)
        {
            if (a != null &&
                b != null &&
                a.Name == b.Name &&
                a.Types.Count == b.Types.Count)
            {
                var modCovInfo = new ModuleCoverageInfo(a.Name);
                foreach (var type in a.Types)
                {
                    var bType = b.Types.Single(t => t.Name == type.Name);
                    modCovInfo.Types.Add(type + bType);
                }

                return modCovInfo;
            }

            throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
        }
    }

    public sealed class TypeCoverageInfo : NamedCoverageInfo
    {
        public TypeCoverageInfo(string name)
            : base(name)
        {
            Methods = new List<MethodCoverageInfo>();
        }

        [JsonProperty("methods", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<MethodCoverageInfo> Methods { get; }

        public static TypeCoverageInfo operator +(TypeCoverageInfo a, TypeCoverageInfo b)
        {
            if (a != null &&
                b != null &&
                a.Name == b.Name &&
                a.Methods.Count == b.Methods.Count)
            {
                var typeCovInfo = new TypeCoverageInfo(a.Name);
                foreach (var method in a.Methods)
                {
                    var bMethod = b.Methods.Single(m => m.Name == method.Name && m.FileName == method.FileName);
                    typeCovInfo.Methods.Add(method + bMethod);
                }

                return typeCovInfo;
            }

            throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
        }
    }

    public sealed class MethodCoverageInfo : NamedCoverageInfo
    {
        public MethodCoverageInfo(string name)
            : base(name)
        {
            Segments = Array.Empty<int[]>();
        }

        [JsonProperty("filename", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? FileName { get; set; }

        [JsonProperty("segments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int[][] Segments { get; set; }

        public static MethodCoverageInfo operator +(MethodCoverageInfo a, MethodCoverageInfo b)
        {
            if (a != null &&
                b != null &&
                a.FileName == b.FileName &&
                a.Name == b.Name &&
                a.Segments.Length == b.Segments.Length)
            {
                var mcInfo = new MethodCoverageInfo(a.Name)
                {
                    FileName = a.FileName,
                    Segments = new int[a.Segments.Length][]
                };

                for (var i = 0; i < mcInfo.Segments.Length; i++)
                {
                    mcInfo.Segments[i] = new[]
                    {
                        a.Segments[i][0],
                        a.Segments[i][1],
                        a.Segments[i][2],
                        a.Segments[i][3],
                        a.Segments[i][4] + b.Segments[i][4],
                    };
                }

                return mcInfo;
            }

            throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
        }
    }
}

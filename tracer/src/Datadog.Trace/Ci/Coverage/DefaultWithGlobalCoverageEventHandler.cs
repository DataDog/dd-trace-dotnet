// <copyright file="DefaultWithGlobalCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

    public GlobalCoverageInfo GetCodeCoveragePercentage()
    {
        const int HIDDEN = 0xFEEFEE;

        // Get all ModuleValues
        var lstModulesInstances = new List<ModuleValue>();
        lstModulesInstances.AddRange(GlobalContainer.CloseContext());
        foreach (var coverage in _coverages)
        {
            lstModulesInstances.AddRange(coverage.CloseContext());
            coverage.Clear();
        }

        GlobalContainer.Clear();

        var globalCoverage = new GlobalCoverageInfo();
        var moduleProcessed = new HashSet<Module>();
        foreach (var moduleValue in lstModulesInstances)
        {
            if (moduleValue is null)
            {
                continue;
            }

            var moduleDef = MethodSymbolResolver.Instance.GetModuleDef(moduleValue.Module);
            if (moduleDef is null)
            {
                continue;
            }

            var componentCoverageInfo = new ComponentCoverageInfo(moduleDef.FullName);
            for (var tIdx = 0; tIdx < moduleValue.Metadata.GetTotalTypes(); tIdx++)
            {
                var typeValue = moduleValue.Types[tIdx];
                if (typeValue is null && moduleProcessed.Contains(moduleValue.Module))
                {
                    continue;
                }

                var typeDef = moduleDef.Types[tIdx];
                for (var mIdx = 0; mIdx < moduleValue.Metadata.GetTotalMethodsOfTypeOrDefault(tIdx); mIdx++)
                {
                    var methodValue = typeValue?.Methods[mIdx];
                    if (methodValue is null && moduleProcessed.Contains(moduleValue.Module))
                    {
                        continue;
                    }

                    var methodDef = typeDef.Methods[mIdx];
                    if (methodDef.HasBody && methodDef.Body.HasInstructions)
                    {
                        var seqPoints = new List<SequencePoint>(methodValue?.SequencePoints?.Length ?? methodDef.Body.Instructions.Count);
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

                        FileCoverageInfo? fileCoverageInfo = null;
                        var seqPointsCount = methodValue?.SequencePoints?.Length ?? seqPoints.Count;
                        for (var x = 0; x < seqPointsCount; x++)
                        {
                            var seqPoint = seqPoints[x];
                            fileCoverageInfo ??= new FileCoverageInfo(CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(seqPoint.Document.Url, false));

                            var repInSeqPoints = 0;
                            if (methodValue?.SequencePoints?.Length > x)
                            {
                                repInSeqPoints = methodValue.SequencePoints[x];
                            }
                            else if (methodValue?.SequencePoints is not null)
                            {
                                Log.Warning($"Index doesn't found: {fileCoverageInfo.Path} | {seqPoint.StartLine}:{seqPoint.StartColumn}:{seqPoint.EndLine}:{seqPoint.EndColumn} | {methodDef.FullName} | {x} | {methodValue.SequencePoints.Length}");
                            }

                            fileCoverageInfo.Add(new[] { (uint)seqPoint.StartLine, (uint)seqPoint.StartColumn, (uint)seqPoint.EndLine, (uint)seqPoint.EndColumn, (uint)repInSeqPoints });
                        }

                        if (fileCoverageInfo is not null)
                        {
                            componentCoverageInfo.Add(fileCoverageInfo);
                        }
                    }
                }
            }

            moduleProcessed.Add(moduleValue.Module);
            globalCoverage.Add(componentCoverageInfo);
        }

        if (Log.IsEnabled(LogEventLevel.Debug))
        {
            Log.Debug("Global Coverage payload: {payload}", JsonConvert.SerializeObject(globalCoverage));
        }

        return globalCoverage;
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
                    if (segment[4] != 0)
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
            Segments = new List<uint[]>();
        }

        [JsonProperty("path", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string? Path { get; set; }

        [JsonProperty("segments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<uint[]> Segments { get; set; }

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
                var aSegments = a.Segments ?? Enumerable.Empty<uint[]>();
                var bSegments = b.Segments ?? Enumerable.Empty<uint[]>();

                foreach (var segment in aSegments)
                {
                    fcInfo.Add(segment);
                }

                foreach (var segment in bSegments)
                {
                    fcInfo.Add(segment);
                }

                return fcInfo;
            }

            throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
        }

        public void Add(uint[] segment)
        {
            if (segment?.Length == 5)
            {
                var segIdx = Segments.FindIndex(
                    s =>
                        s[0] == segment[0] &&
                        s[1] == segment[1] &&
                        s[2] == segment[2] &&
                        s[3] == segment[3]);

                if (segIdx != -1)
                {
                    Segments[segIdx][4] += segment[4];
                }
                else
                {
                    Segments.Add(segment);
                }
            }
        }
    }
}

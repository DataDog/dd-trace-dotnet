// <copyright file="DefaultWithGlobalCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Pdb;
using Datadog.Trace.Vendors.dnlib.DotNet;
using Datadog.Trace.Vendors.dnlib.DotNet.Pdb;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal class DefaultWithGlobalCoverageEventHandler : DefaultCoverageEventHandler
{
    private readonly List<CoverageContextContainer> _coverages = new();

    protected override void OnSessionStart(CoverageContextContainer context)
    {
        if (context is not null)
        {
            lock (_coverages)
            {
                _coverages.Add(context);
            }

            base.OnSessionStart(context);
        }
    }

    public void Clear()
    {
        lock (_coverages)
        {
            foreach (var coverage in _coverages)
            {
                coverage?.Clear();
            }

            _coverages.Clear();
            GlobalContainer.Clear();
        }
    }

    public GlobalCoverageInfo GetCodeCoveragePercentage()
    {
        lock (_coverages)
        {
            var sw = Stopwatch.StartNew();
            const int HIDDEN = 0xFEEFEE;
            var globalCoverage = new GlobalCoverageInfo();
            var moduleProcessed = new HashSet<Module>();
            var fromGlobalContainer = GlobalContainer?.CloseContext() ?? Array.Empty<ModuleValue>();
            var fromTestsContainers = _coverages.SelectMany(c => c?.CloseContext() ?? Array.Empty<ModuleValue>());
            foreach (var moduleValue in fromGlobalContainer.Concat(fromTestsContainers))
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

                List<TypeDef>? moduleTypes;
                lock (TypeDefsFromModuleDefs)
                {
                    if (!TypeDefsFromModuleDefs.TryGetValue(moduleDef, out moduleTypes))
                    {
                        moduleTypes = moduleDef.GetTypes().ToList();
                        TypeDefsFromModuleDefs[moduleDef] = moduleTypes;
                    }
                }

                var componentCoverageInfo = new ComponentCoverageInfo(moduleDef.FullName);
                for (var mIdx = 0; mIdx < moduleValue.Metadata.GetMethodsCount(); mIdx++)
                {
                    var methodValue = moduleValue.Methods[mIdx];
                    if (methodValue is null && moduleProcessed.Contains(moduleValue.Module))
                    {
                        continue;
                    }

                    moduleValue.Metadata.GetMethodsMetadata(mIdx, out var typeIndex, out var methodIndex);
                    var typeDef = moduleTypes[typeIndex];
                    var methodDef = typeDef.Methods[methodIndex];

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
                                var location = $"{seqPoint.StartLine}:{seqPoint.StartColumn}:{seqPoint.EndLine}:{seqPoint.EndColumn}";
                                var method = $"{methodDef.FullName} | {x} | {methodValue.SequencePoints.Length}";
                                Log.Warning("Index not found: {Path} | {Location} | {Method}", fileCoverageInfo.Path, location, method);
                            }

                            fileCoverageInfo.Add(new[] { (uint)seqPoint.StartLine, (uint)seqPoint.StartColumn, (uint)seqPoint.EndLine, (uint)seqPoint.EndColumn, (uint)repInSeqPoints });
                        }

                        if (fileCoverageInfo is not null)
                        {
                            componentCoverageInfo.Add(fileCoverageInfo);
                        }
                    }
                }

                moduleProcessed.Add(moduleValue.Module);
                globalCoverage.Add(componentCoverageInfo);
            }

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Global Coverage payload: {Payload}", JsonConvert.SerializeObject(globalCoverage));
            }

            // Clean coverages
            foreach (var coverage in _coverages)
            {
                coverage?.Clear();
            }

            GlobalContainer?.Clear();

            Log.Information("Total time to calculate global coverage: {TotalMilliseconds}ms", sw.Elapsed.TotalMilliseconds);
            return globalCoverage;
        }
    }
}

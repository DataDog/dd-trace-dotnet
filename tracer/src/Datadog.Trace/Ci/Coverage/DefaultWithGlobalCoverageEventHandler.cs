// <copyright file="DefaultWithGlobalCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Ci.Coverage.Models.Global;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Telemetry;
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
                coverage.Clear();
            }

            _coverages.Clear();
        }
    }

    public unsafe GlobalCoverageInfo GetCodeCoveragePercentage()
    {
        try
        {
            lock (_coverages)
            {
                var sw = Stopwatch.StartNew();
                var globalCoverage = new GlobalCoverageInfo();

                IEnumerable<ModuleValue> GetModuleValues()
                {
                    var globalContainer = GlobalContainer.CloseContext();
                    foreach (var moduleValue in globalContainer)
                    {
                        yield return moduleValue;
                    }

                    foreach (var coverageContextContainer in _coverages)
                    {
                        var container = coverageContextContainer.CloseContext();
                        foreach (var moduleValue in container)
                        {
                            yield return moduleValue;
                        }
                    }
                }

                var componentCoverageInfos = new Dictionary<Module, ComponentCoverageInfo>();
                var fileCoverageInfos = new Dictionary<FileCoverageMetadata, FileCoverageInfo>();

                var fileBitmapBuffer = stackalloc byte[512];
                foreach (var moduleValue in GetModuleValues())
                {
                    var module = moduleValue.Module;
                    if (!componentCoverageInfos.TryGetValue(module, out var componentCoverageInfo))
                    {
                        componentCoverageInfo = new ComponentCoverageInfo(module.Name);
                        globalCoverage.Components.Add(componentCoverageInfo);
                        componentCoverageInfos[module] = componentCoverageInfo;
                    }

                    foreach (var moduleFile in moduleValue.Metadata.Files)
                    {
                        if (!fileCoverageInfos.TryGetValue(moduleFile, out var fileCoverageInfo))
                        {
                            fileCoverageInfo = new FileCoverageInfo(moduleFile.Path)
                            {
                                ExecutableBitmap = moduleFile.Bitmap
                            };

                            componentCoverageInfo.Files.Add(fileCoverageInfo);
                            fileCoverageInfos[moduleFile] = fileCoverageInfo;
                        }

                        var fileBitmapLastExecutableLine = moduleFile.LastExecutableLine;
                        var fileBitmapSize = FileBitmap.GetSize(fileBitmapLastExecutableLine);
                        using var fileBitmap = fileBitmapSize <= 512 ? new FileBitmap(fileBitmapBuffer, fileBitmapSize) : new FileBitmap(new byte[fileBitmapSize]);
                        if (moduleValue.Metadata.CoverageMode == 0)
                        {
                            var filesLines = (byte*)moduleValue.FilesLines + moduleFile.Offset;
                            for (var i = 0; i < fileBitmapLastExecutableLine; i++)
                            {
                                if (filesLines[i] == 1)
                                {
                                    fileBitmap.Set(i + 1);
                                }
                            }
                        }
                        else if (moduleValue.Metadata.CoverageMode == 1)
                        {
                            var filesLines = (int*)moduleValue.FilesLines + moduleFile.Offset;
                            for (var i = 0; i < fileBitmapLastExecutableLine; i++)
                            {
                                if (filesLines[i] == 1)
                                {
                                    fileBitmap.Set(i + 1);
                                }
                            }
                        }

                        if (fileBitmap.HasActiveBits())
                        {
                            if (fileCoverageInfo.ExecutedBitmap is null)
                            {
                                fileCoverageInfo.ExecutedBitmap = fileBitmap.GetInternalArrayOrToArrayAndDispose();
                            }
                            else
                            {
                                using var currentExecutedBitmap = new FileBitmap(fileCoverageInfo.ExecutedBitmap);
                                fileCoverageInfo.ExecutedBitmap = FileBitmap.Or(fileBitmap, currentExecutedBitmap, true).GetInternalArrayOrToArrayAndDispose();
                            }
                        }
                    }
                }

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Global Coverage payload: {Payload}", JsonConvert.SerializeObject(globalCoverage));
                }

                // Clean coverages
                Clear();

                Log.Information("Total time to calculate global coverage: {TotalMilliseconds}ms", sw.Elapsed.TotalMilliseconds);
                return globalCoverage;
            }
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
            Log.Error(ex, "Error processing the global coverage data.");
            throw;
        }
    }

    protected override void OnClearContext(CoverageContextContainer context)
    {
        // None we need to keep all context to calculate the global coverage later
    }
}

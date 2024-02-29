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
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Pdb;
using Datadog.Trace.Telemetry;
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

    public unsafe GlobalCoverageInfo GetCodeCoveragePercentage()
    {
        try
        {
            lock (_coverages)
            {
                var sw = Stopwatch.StartNew();
                var globalCoverage = new GlobalCoverageInfo();
                var fromGlobalContainer = GlobalContainer?.CloseContext() ?? [];
                var fromTestsContainers = _coverages.SelectMany(c => c?.CloseContext() ?? []);

                var fileBitmapBuffer = stackalloc byte[512];
                foreach (var moduleValue in fromGlobalContainer.Concat(fromTestsContainers))
                {
                    if (moduleValue is null)
                    {
                        continue;
                    }

                    var module = moduleValue.Module;
                    var componentCoverageInfo = new ComponentCoverageInfo(module.Name);
                    foreach (var moduleFile in moduleValue.Metadata.Files)
                    {
                        var fileCoverageInfo = new FileCoverageInfo(moduleFile.Path)
                        {
                            ExecutableBitmap = moduleFile.Bitmap
                        };

                        var fileBitmapLastExecutableLine = moduleFile.LastExecutableLine;
                        var fileBitmapSize = FileBitmap.GetSize(fileBitmapLastExecutableLine);
                        using var fileBitmap = fileBitmapSize <= 512 ? new FileBitmap(fileBitmapBuffer, fileBitmapSize) : new FileBitmap(new byte[fileBitmapSize]);
                        if (moduleValue.Metadata.CoverageMode == 0)
                        {
                            var linesInFile = new VendoredMicrosoftCode.System.Span<byte>((byte*)moduleValue.FilesLines + moduleFile.Offset, fileBitmapLastExecutableLine);
                            for (var i = 0; i < linesInFile.Length; i++)
                            {
                                if (linesInFile[i] == 1)
                                {
                                    fileBitmap.Set(i + 1);
                                }
                            }
                        }
                        else if (moduleValue.Metadata.CoverageMode == 1)
                        {
                            var linesInFile = new VendoredMicrosoftCode.System.Span<int>((int*)moduleValue.FilesLines + moduleFile.Offset, fileBitmapLastExecutableLine);
                            for (var i = 0; i < linesInFile.Length; i++)
                            {
                                if (linesInFile[i] > 0)
                                {
                                    fileBitmap.Set(i + 1);
                                }
                            }
                        }

                        fileCoverageInfo.ExecutedBitmap = fileBitmap.GetInternalArrayOrToArray();
                        componentCoverageInfo.Files.Add(fileCoverageInfo);
                    }

                    globalCoverage.Add(componentCoverageInfo);
                }

                // if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Information("Global Coverage payload: {Payload}", JsonConvert.SerializeObject(globalCoverage));
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
        catch
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
            throw;
        }
    }
}

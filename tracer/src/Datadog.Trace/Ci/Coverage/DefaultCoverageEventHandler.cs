// <copyright file="DefaultCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Coverage.Models.Tests;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Logging;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal class DefaultCoverageEventHandler : CoverageEventHandler
{
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultCoverageEventHandler));

    protected override void OnSessionStart(CoverageContextContainer context)
    {
    }

    protected override unsafe object? OnSessionFinished(CoverageContextContainer context)
    {
        try
        {
            var modules = context.CloseContext();

            Dictionary<string, FileCoverage>? fileDictionary = null;
            var fileBitmapBuffer = stackalloc byte[512];
            foreach (var moduleValue in modules)
            {
                foreach (var moduleFile in moduleValue.Metadata.Files)
                {
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
                        FileCoverage? fileCoverage;
                        if (fileDictionary is null)
                        {
                            fileCoverage = new FileCoverage
                            {
                                FileName = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(moduleFile.Path, false)
                            };

                            fileDictionary = new Dictionary<string, FileCoverage>
                            {
                                [moduleFile.Path] = fileCoverage
                            };
                        }
                        else if (!fileDictionary.TryGetValue(moduleFile.Path, out fileCoverage))
                        {
                            fileCoverage = new FileCoverage
                            {
                                FileName = CIEnvironmentValues.Instance.MakeRelativePathFromSourceRoot(moduleFile.Path, false)
                            };

                            fileDictionary[moduleFile.Path] = fileCoverage;
                        }

                        if (fileCoverage.Bitmap is { } bitmap)
                        {
                            using var currentBitmap = new FileBitmap(bitmap);
                            fileCoverage.Bitmap = FileBitmap.Or(fileBitmap, currentBitmap, true).GetInternalArrayOrToArrayAndDispose();
                        }
                        else
                        {
                            fileCoverage.Bitmap = fileBitmap.GetInternalArrayOrToArrayAndDispose();
                        }
                    }
                }
            }

            TelemetryFactory.Metrics.RecordDistributionCIVisibilityCodeCoverageFiles(fileDictionary?.Count ?? 0);

            if (fileDictionary is null || fileDictionary.Count == 0)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageIsEmpty();
                return null;
            }

            var testCoverage = new TestCoverage
            {
                Files = fileDictionary.Values.ToList()
            };

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("Test Coverage: {Json}", JsonConvert.SerializeObject(testCoverage));
            }

            return testCoverage;
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
            Log.Error(ex, "Error processing the coverage data.");
            throw;
        }
    }

    protected override void OnClearContext(CoverageContextContainer context)
    {
        context.Clear();
    }
}

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
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci.Coverage;

internal class DefaultCoverageEventHandler : CoverageEventHandler
{
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DefaultCoverageEventHandler));

    protected override void OnSessionStart(CoverageContextContainer context)
    {
    }

    protected override object? OnSessionFinished(CoverageContextContainer context, IReadOnlyList<ModuleValue> modules)
        => ProcessSessionFinished(modules, out _);

    protected object? ProcessSessionFinished(IReadOnlyList<ModuleValue> modules, out ModuleCoverageData[] moduleCoverage)
    {
        try
        {
            moduleCoverage = new ModuleCoverageData[modules.Count];
            Dictionary<string, FileCoverage>? fileDictionary = null;
            for (var moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
            {
                var capturedModule = ModuleCoverageData.Capture(modules[moduleIndex]);
                moduleCoverage[moduleIndex] = capturedModule;
                for (var fileIndex = 0; fileIndex < capturedModule.Metadata.Files.Length; fileIndex++)
                {
                    var executedBitmap = capturedModule.ExecutedBitmaps[fileIndex];
                    if (executedBitmap is null)
                    {
                        continue;
                    }

                    var moduleFile = capturedModule.Metadata.Files[fileIndex];
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
                        using var capturedBitmap = new FileBitmap(executedBitmap);
                        using var currentBitmap = new FileBitmap(bitmap);
                        var mergedBitmap = capturedBitmap | currentBitmap;
                        fileCoverage.Bitmap = mergedBitmap.GetInternalArrayOrToArrayAndDispose();
                    }
                    else
                    {
                        // The accumulator only reads the bitmap, so the per-test payload can reuse it.
                        fileCoverage.Bitmap = executedBitmap;
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
                Log.Debug("Test Coverage: {Json}", JsonHelper.SerializeObject(testCoverage));
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
}

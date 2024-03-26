// <copyright file="InProcCoverageCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollector.InProcDataCollector;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.InProcDataCollector;

namespace Datadog.Trace.Coverage.Collector;

/*
 * To use InProc DataCollector we need to provide the following .runsettings file:
 *
 *  <RunSettings>
 *      <InProcDataCollectionRunSettings>
 *          <InProcDataCollectors>
 *              <InProcDataCollector
 *                  friendlyName='GlobalCoverageCollector'
 *                  uri='InProcDataCollector://Datadog/GlobalCoverageCollector/1.0'
 *                  assemblyQualifiedName='Datadog.Trace.Coverage.Collector.InProcCoverageCollector, Datadog.Trace.Coverage.collector, Version=1.0.0.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb'
 *                  codebase='Datadog.Trace.Coverage.collector.dll'>
 *                  <Configuration>
 *                  </Configuration>
 *              </InProcDataCollector>
 *          </InProcDataCollectors>
 *      </InProcDataCollectionRunSettings>
 *  </RunSettings>
 */

/// <summary>
/// Datadog inproc coverage collector
/// </summary>
public class InProcCoverageCollector : InProcDataCollection
{
    private const string OutputPathKey = "OutputPath";
    private string? _outputPathValue = null;

    /// <summary>
    /// Initialize inproc coverage collector
    /// </summary>
    /// <param name="dataCollectionSink">DataCollectionSink instance</param>
    public void Initialize(IDataCollectionSink dataCollectionSink)
    {
    }

    /// <summary>
    /// Test session start event handler
    /// </summary>
    /// <param name="testSessionStartArgs">Test session start arguments</param>
    public void TestSessionStart(TestSessionStartArgs testSessionStartArgs)
    {
        if (testSessionStartArgs.GetPropertyValue(OutputPathKey) is string outputPath)
        {
            _outputPathValue = outputPath;
        }
    }

    /// <summary>
    /// Test case start event handler
    /// </summary>
    /// <param name="testCaseStartArgs">Test case start arguments</param>
    public void TestCaseStart(TestCaseStartArgs testCaseStartArgs)
    {
    }

    /// <summary>
    /// Test case end event handler
    /// </summary>
    /// <param name="testCaseEndArgs">Test case end arguments</param>
    public void TestCaseEnd(TestCaseEndArgs testCaseEndArgs)
    {
    }

    /// <summary>
    /// Test session end event handler
    /// </summary>
    /// <param name="testSessionEndArgs">Test session end arguments</param>
    public void TestSessionEnd(TestSessionEndArgs testSessionEndArgs)
    {
        if (CoverageReporter.Handler is DefaultWithGlobalCoverageEventHandler coverageHandler)
        {
            var globalCoverage = coverageHandler.GetCodeCoveragePercentage();
            var outputPath = $"coverage-{DateTime.Now:yyyy-MM-dd_HH_mm_ss}-{Guid.NewGuid():n}.json";
            if (!string.IsNullOrEmpty(_outputPathValue))
            {
                outputPath = Path.Combine(_outputPathValue, outputPath);
            }

            using var fileStream = File.OpenWrite(outputPath);
            using var streamWriter = new StreamWriter(fileStream);
            using var jsonWriter = new JsonTextWriter(streamWriter) { CloseOutput = true };
            var jsonSerializer = new JsonSerializer();
            jsonSerializer.Serialize(jsonWriter, globalCoverage);
        }
    }
}

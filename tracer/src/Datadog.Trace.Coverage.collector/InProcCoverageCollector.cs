// <copyright file="InProcCoverageCollector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
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
    private StandaloneCoverageReconciliation? _standaloneReconciliation;

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

        if (CoverageReporter.Handler is DefaultWithGlobalCoverageEventHandler coverageHandler)
        {
            var outputDirectory = _outputPathValue ?? Environment.CurrentDirectory;
            if (coverageHandler.RegisterCollectorOutputDirectory(outputDirectory))
            {
                var coordinatorDirectory = outputDirectory;
                foreach (var registration in coverageHandler.OutputRegistrations)
                {
                    if (registration.IsCoordinator)
                    {
                        coordinatorDirectory = registration.Directory;
                        break;
                    }
                }

                _standaloneReconciliation = StandaloneCoverageReconciliation.TryCreate(
                    coordinatorDirectory,
                    TestOptimization.Instance.RunId);
            }
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
        var standaloneReconciliation = _standaloneReconciliation;
        _standaloneReconciliation = null;
        if (standaloneReconciliation is null)
        {
            CoverageReporter.FinalizeGlobalCoverage();
            return;
        }

        var completionRegistered = false;
        try
        {
            CoverageReporter.FinalizeGlobalCoverage(
                complete =>
                {
                    try
                    {
                        if (complete)
                        {
                            standaloneReconciliation.TryPublish();
                        }
                    }
                    finally
                    {
                        standaloneReconciliation.Dispose();
                    }
                });
            completionRegistered = true;
        }
        finally
        {
            if (!completionRegistered)
            {
                standaloneReconciliation.Dispose();
            }
        }
    }
}

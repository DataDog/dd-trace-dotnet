// <copyright file="TestOptimizationImpactedTestsDetectionFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal class TestOptimizationImpactedTestsDetectionFeature : ITestOptimizationImpactedTestsDetectionFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationImpactedTestsDetectionFeature));
    private readonly CIEnvironmentValues _environmentValues;
    private readonly string? _defaultBranch;
    private ImpactedTestsModule? _impactedTestsModule;

    private TestOptimizationImpactedTestsDetectionFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, CIEnvironmentValues environmentValues)
    {
        _environmentValues = environmentValues;
        _defaultBranch = clientSettingsResponse.DefaultBranch;
        if (settings.ImpactedTestsDetectionEnabled == null && clientSettingsResponse.ImpactedTestsEnabled.HasValue)
        {
            Log.Information("TestOptimizationImpactedTestsDetectionFeature: Impacted tests detection has been changed to {Value} by the settings api.", clientSettingsResponse.ImpactedTestsEnabled.Value);
            settings.SetImpactedTestsEnabled(clientSettingsResponse.ImpactedTestsEnabled.Value);
        }

        if (settings.ImpactedTestsDetectionEnabled == true)
        {
            Log.Information("TestOptimizationImpactedTestsDetectionFeature: Impacted tests detection is enabled.");
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationImpactedTestsDetectionFeature: Impacted tests detection is disabled.");
            _impactedTestsModule = ImpactedTestsModule.CreateNoOp();
            Enabled = false;
        }
    }

    public bool Enabled { get; }

    public ImpactedTestsModule ImpactedTestsAnalyzer
    {
        get
        {
            if (_impactedTestsModule is not null)
            {
                return _impactedTestsModule;
            }

            return _impactedTestsModule = ImpactedTestsModule.CreateInstance(_environmentValues, _defaultBranch);
        }
    }

    public static ITestOptimizationImpactedTestsDetectionFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, CIEnvironmentValues environmentValues)
        => new TestOptimizationImpactedTestsDetectionFeature(settings, clientSettingsResponse, environmentValues);
}

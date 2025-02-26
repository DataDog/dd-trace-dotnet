// <copyright file="CiVisibilityImpactedTestsDetectionFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal class CiVisibilityImpactedTestsDetectionFeature : ICiVisibilityImpactedTestsDetectionFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CiVisibilityImpactedTestsDetectionFeature));
    private readonly Task<TestOptimizationClient.ImpactedTestsDetectionResponse> _impactedTestsDetectionFilesTask;

    private CiVisibilityImpactedTestsDetectionFeature(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(settings));
        }

        if (testOptimizationClient is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(testOptimizationClient));
        }

        if (settings.ImpactedTestsDetectionEnabled == null && clientSettingsResponse.ImpactedTestsEnabled.HasValue)
        {
            Log.Information("CiVisibility: Impacted Tests Detection has been changed to {Value} by the settings api.", clientSettingsResponse.ImpactedTestsEnabled.Value);
            settings.SetImpactedTestsEnabled(clientSettingsResponse.ImpactedTestsEnabled.Value);
        }

        if (settings.ImpactedTestsDetectionEnabled == true)
        {
            Log.Information("CiVisibilityImpactedTestsDetectionFeature: Impacted tests detection is enabled.");
            _impactedTestsDetectionFilesTask = Task.Run(() => InternalGetImpactedTestsDetectionFilesAsync(testOptimizationClient));
            Enabled = true;
        }
        else
        {
            Log.Information("CiVisibilityImpactedTestsDetectionFeature: Impacted tests detection is disabled.");
            _impactedTestsDetectionFilesTask = Task.FromResult(default(TestOptimizationClient.ImpactedTestsDetectionResponse));
            Enabled = false;
        }

        return;

        static async Task<TestOptimizationClient.ImpactedTestsDetectionResponse> InternalGetImpactedTestsDetectionFilesAsync(ITestOptimizationClient testOptimizationClient)
        {
            Log.Debug("CiVisibilityImpactedTestsDetectionFeature: Getting impacted tests detection modified files...");
            var response = await testOptimizationClient.GetImpactedTestsDetectionFilesAsync().ConfigureAwait(false);
            Log.Debug("CiVisibilityImpactedTestsDetectionFeature: Impacted tests detection modified files received.");
            return response;
        }
    }

    public bool Enabled { get; }

    public TestOptimizationClient.ImpactedTestsDetectionResponse ImpactedTestsDetectionResponse
        => _impactedTestsDetectionFilesTask.SafeGetResult();

    public static ICiVisibilityImpactedTestsDetectionFeature Create(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new CiVisibilityImpactedTestsDetectionFeature(settings, clientSettingsResponse, testOptimizationClient);
}

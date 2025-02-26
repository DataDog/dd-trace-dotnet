// <copyright file="TestOptimizationEarlyFlakeDetectionFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal class TestOptimizationEarlyFlakeDetectionFeature : ITestOptimizationEarlyFlakeDetectionFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationEarlyFlakeDetectionFeature));
    private readonly Task<TestOptimizationClient.EarlyFlakeDetectionResponse> _earlyFlakeDetectionSettingsTask;

    private TestOptimizationEarlyFlakeDetectionFeature(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(settings));
        }

        if (testOptimizationClient is null)
        {
            ThrowHelper.ThrowArgumentNullException(nameof(testOptimizationClient));
        }

        EarlyFlakeDetectionSettings = clientSettingsResponse.EarlyFlakeDetection;

        if (settings.EarlyFlakeDetectionEnabled == true || clientSettingsResponse.EarlyFlakeDetection.Enabled == true)
        {
            Log.Information("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection is enabled.");
            settings.SetEarlyFlakeDetectionEnabled(true);
            _earlyFlakeDetectionSettingsTask = Task.Run(() => InternalGetEarlyFlakeDetectionSettingsAsync(testOptimizationClient));
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection is disabled.");
            settings.SetEarlyFlakeDetectionEnabled(false);
            _earlyFlakeDetectionSettingsTask = Task.FromResult(new TestOptimizationClient.EarlyFlakeDetectionResponse());
            Enabled = false;
        }

        return;

        static async Task<TestOptimizationClient.EarlyFlakeDetectionResponse> InternalGetEarlyFlakeDetectionSettingsAsync(ITestOptimizationClient testOptimizationClient)
        {
            Log.Debug("TestOptimizationEarlyFlakeDetectionFeature: Getting early flake detection data...");
            var response = await testOptimizationClient.GetEarlyFlakeDetectionTestsAsync().ConfigureAwait(false);
            Log.Debug("TestOptimizationEarlyFlakeDetectionFeature: Early flake detection data received.");
            return response;
        }
    }

    public bool Enabled { get; }

    public TestOptimizationClient.EarlyFlakeDetectionSettingsResponse EarlyFlakeDetectionSettings { get; }

    public TestOptimizationClient.EarlyFlakeDetectionResponse? EarlyFlakeDetectionResponse
        => _earlyFlakeDetectionSettingsTask.SafeGetResult();

    public static ITestOptimizationEarlyFlakeDetectionFeature Create(CIVisibilitySettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new TestOptimizationEarlyFlakeDetectionFeature(settings, clientSettingsResponse, testOptimizationClient);

    public bool IsAnEarlyFlakeDetectionTest(string moduleName, string testSuite, string testName)
    {
        if (EarlyFlakeDetectionResponse is { Tests: { } efdTests } &&
            efdTests.TryGetValue(moduleName, out var efdResponseSuites) &&
            efdResponseSuites?.TryGetValue(testSuite, out var efdResponseTests) == true &&
            efdResponseTests is not null)
        {
            foreach (var test in efdResponseTests)
            {
                if (test == testName)
                {
                    Log.Debug("TestOptimizationEarlyFlakeDetectionFeature: Test is included in the early flake detection response. [ModuleName: {ModuleName}, TestSuite: {TestSuite}, TestName: {TestName}]", moduleName, testSuite, testName);
                    return true;
                }
            }
        }

        Log.Debug("TestOptimizationEarlyFlakeDetectionFeature: Test is not in the early flake detection response. [ModuleName: {ModuleName}, TestSuite: {TestSuite}, TestName: {TestName}]", moduleName, testSuite, testName);
        return false;
    }
}

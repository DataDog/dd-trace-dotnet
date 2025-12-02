// <copyright file="TestOptimizationKnownTestsFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationKnownTestsFeature : ITestOptimizationKnownTestsFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationKnownTestsFeature));
    private readonly bool _enabled;
    private readonly Task<TestOptimizationClient.KnownTestsResponse> _knownTestsTask;

    private TestOptimizationKnownTestsFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings.KnownTestsEnabled == true || clientSettingsResponse.KnownTestsEnabled == true)
        {
            Log.Information("TestOptimizationKnownTestsFeature: Known tests is enabled.");
            settings.SetKnownTestsEnabled(true);
            _knownTestsTask = Task.Run(() => InternalGetKnownTestsAsync(testOptimizationClient));
            _enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationKnownTestsFeature: Known tests is disabled.");
            settings.SetKnownTestsEnabled(false);
            settings.SetEarlyFlakeDetectionEnabled(false);
            _knownTestsTask = Task.FromResult(default(TestOptimizationClient.KnownTestsResponse));
            _enabled = false;
        }

        return;

        static async Task<TestOptimizationClient.KnownTestsResponse> InternalGetKnownTestsAsync(ITestOptimizationClient testOptimizationClient)
        {
            Log.Debug("TestOptimizationKnownTestsFeature: Getting early flake detection data...");
            var response = await testOptimizationClient.GetKnownTestsAsync().ConfigureAwait(false);
            Log.Debug("TestOptimizationKnownTestsFeature: Early flake detection data received.");
            return response;
        }
    }

    public bool Enabled => _enabled && KnownTests.Tests is not null; // Ensure that the known tests response was not empty

    public TestOptimizationClient.KnownTestsResponse KnownTests
        => _knownTestsTask.SafeGetResult();

    public static ITestOptimizationKnownTestsFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new TestOptimizationKnownTestsFeature(settings, clientSettingsResponse, testOptimizationClient);

    public bool IsAKnownTest(string moduleName, string testSuite, string testName)
    {
        if (KnownTests is { Tests: { } knownTests } &&
            knownTests.TryGetValue(moduleName, out var knownTestsSuites) &&
            knownTestsSuites?.TryGetValue(testSuite, out var knownTestsArray) == true &&
            knownTestsArray is not null)
        {
            foreach (var test in knownTestsArray)
            {
                if (test == testName)
                {
                    Log.Debug("TestOptimizationKnownTestsFeature: Test is a known tests. [ModuleName: {ModuleName}, TestSuite: {TestSuite}, TestName: {TestName}]", moduleName, testSuite, testName);
                    return true;
                }
            }
        }

        Log.Debug("TestOptimizationKnownTestsFeature: Test is not a known test. [ModuleName: {ModuleName}, TestSuite: {TestSuite}, TestName: {TestName}]", moduleName, testSuite, testName);
        return false;
    }
}

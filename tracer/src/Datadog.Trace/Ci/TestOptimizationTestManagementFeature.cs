// <copyright file="TestOptimizationTestManagementFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationTestManagementFeature : ITestOptimizationTestManagementFeature
{
    public const int TestManagementAttemptToFixRetryCountDefault = 10;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationTestManagementFeature));
    private readonly bool _enabled;
    private readonly Task<TestOptimizationClient.TestManagementResponse> _testManagementTask;

    private TestOptimizationTestManagementFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings.TestManagementEnabled == true || clientSettingsResponse.TestManagement.Enabled == true)
        {
            Log.Information("TestOptimizationTestManagementFeature: Test management is enabled.");
            settings.SetTestManagementEnabled(true);
            _testManagementTask = Task.Run(() => InternalGetTestManagementTestsAsync(testOptimizationClient));
            _enabled = true;
            TestManagementAttemptToFixRetryCount = settings.TestManagementAttemptToFixRetryCount ?? clientSettingsResponse.TestManagement.AttemptToFixRetries ?? TestManagementAttemptToFixRetryCountDefault;
        }
        else
        {
            Log.Information("TestOptimizationTestManagementFeature: Test management is disabled.");
            settings.SetTestManagementEnabled(false);
            _testManagementTask = Task.FromResult(new TestOptimizationClient.TestManagementResponse());
            _enabled = false;
            TestManagementAttemptToFixRetryCount = settings.TestManagementAttemptToFixRetryCount ?? TestManagementAttemptToFixRetryCountDefault;
        }

        return;

        static async Task<TestOptimizationClient.TestManagementResponse> InternalGetTestManagementTestsAsync(ITestOptimizationClient testOptimizationClient)
        {
            Log.Debug("TestOptimizationTestManagementFeature: Getting test management data...");
            var response = await testOptimizationClient.GetTestManagementTests().ConfigureAwait(false);
            Log.Debug("TestOptimizationTestManagementFeature: Test management data received.");
            return response;
        }
    }

    public bool Enabled => _enabled && TestManagement.Modules is not null; // Ensure that the test management response was not empty

    public TestOptimizationClient.TestManagementResponse TestManagement
        => _testManagementTask.SafeGetResult();

    public int TestManagementAttemptToFixRetryCount { get; }

    public static ITestOptimizationTestManagementFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new TestOptimizationTestManagementFeature(settings, clientSettingsResponse, testOptimizationClient);

    public TestOptimizationClient.TestManagementResponseTestPropertiesAttributes GetTestProperties(string moduleName, string testSuite, string testName)
    {
        if (TestManagement is { Modules: { } modules } &&
            modules.TryGetValue(moduleName, out var module) &&
            module?.Suites?.TryGetValue(testSuite, out var testSuiteProperties) == true &&
            testSuiteProperties?.Tests?.TryGetValue(testName, out var testProperties) == true)
        {
            Log.Debug("TestOptimizationTestManagementFeature: Get test properties found for: [{ModuleName}, {TestSuite}, {TestName}]", moduleName, testSuite, testName);
            return testProperties.Properties;
        }

        Log.Debug("TestOptimizationTestManagementFeature: Get test properties not found for: [{ModuleName}, {TestSuite}, {TestName}]", moduleName, testSuite, testName);
        return TestOptimizationClient.TestManagementResponseTestPropertiesAttributes.Default;
    }
}

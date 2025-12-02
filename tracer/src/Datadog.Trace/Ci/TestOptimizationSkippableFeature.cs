// <copyright file="TestOptimizationSkippableFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationSkippableFeature : ITestOptimizationSkippableFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationSkippableFeature));
    private readonly Task<SkippableTestsDictionary> _skippableTestsTask;

    private TestOptimizationSkippableFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        if (settings.TestsSkippingEnabled == null && clientSettingsResponse.TestsSkipping.HasValue)
        {
            Log.Information("TestOptimizationSkippableFeature: Tests Skipping has been changed to {Value} by settings api.", clientSettingsResponse.TestsSkipping.Value);
            settings.SetTestsSkippingEnabled(clientSettingsResponse.TestsSkipping.Value);
        }

        if (settings.TestsSkippingEnabled == true)
        {
            Log.Information("TestOptimizationSkippableFeature: Test skipping is enabled.");
            _skippableTestsTask = Task.Run(() => InternalGetSkippableTestsAsync(testOptimizationClient));
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationSkippableFeature: Test skipping is disabled.");
            _skippableTestsTask = Task.FromResult(new SkippableTestsDictionary());
            Enabled = false;
        }

        return;

        static async Task<SkippableTestsDictionary> InternalGetSkippableTestsAsync(ITestOptimizationClient testOptimizationClient)
        {
            // For ITR we need the git metadata upload before consulting the skippable tests.
            // If ITR is disabled we just need to make sure the git upload task has completed before leaving this method.
            await testOptimizationClient.UploadRepositoryChangesAsync().ConfigureAwait(false);

            Log.Debug("TestOptimizationSkippableFeature: Getting skippable tests...");
            var skippeableTests = await testOptimizationClient.GetSkippableTestsAsync().ConfigureAwait(false);
            Log.Information<string?, int>("TestOptimizationSkippableFeature: CorrelationId = {CorrelationId}, SkippableTests = {Length}.", skippeableTests.CorrelationId, skippeableTests.Tests.Count);

            var skippableTestsBySuiteAndName = new SkippableTestsDictionary();
            foreach (var item in skippeableTests.Tests)
            {
                if (!skippableTestsBySuiteAndName.TryGetValue(item.Suite, out var suite))
                {
                    suite = new Dictionary<string, IList<SkippableTest>>();
                    skippableTestsBySuiteAndName[item.Suite] = suite;
                }

                if (!suite.TryGetValue(item.Name, out var name))
                {
                    name = new List<SkippableTest>();
                    suite[item.Name] = name;
                }

                name.Add(item);
            }

            skippableTestsBySuiteAndName.CorrelationId = skippeableTests.CorrelationId;
            Log.Debug("TestOptimizationSkippableFeature: SkippableTests dictionary has been built.");
            return skippableTestsBySuiteAndName;
        }
    }

    public bool Enabled { get; }

    public static ITestOptimizationSkippableFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new TestOptimizationSkippableFeature(settings, clientSettingsResponse, testOptimizationClient);

    public void WaitForSkippableTaskToFinish()
    {
        try
        {
            _skippableTestsTask.SafeWait();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error waiting for skippable tests task to finish.");
        }
    }

    public IList<SkippableTest> GetSkippableTestsFromSuiteAndName(string suite, string name)
    {
        WaitForSkippableTaskToFinish();
        return InternalGetSkippableTestsFromSuiteAndName(suite, name);
    }

    private IList<SkippableTest> InternalGetSkippableTestsFromSuiteAndName(string suite, string name)
    {
        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        if (skippableTestsBySuiteAndName.TryGetValue(suite, out var testsInSuite) &&
            testsInSuite.TryGetValue(name, out var tests))
        {
            return tests;
        }

        return [];
    }

    public bool HasSkippableTests()
    {
        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return skippableTestsBySuiteAndName.Count > 0;
    }

    public string? GetCorrelationId()
    {
        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return skippableTestsBySuiteAndName.CorrelationId;
    }

    internal sealed class SkippableTestsDictionary : Dictionary<string, Dictionary<string, IList<SkippableTest>>>
    {
        public string? CorrelationId { get; set; }
    }
}

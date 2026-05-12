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
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

internal sealed class TestOptimizationSkippableFeature : ITestOptimizationSkippableFeature
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestOptimizationSkippableFeature));
    private readonly TestOptimizationSettings _settings;
    private readonly Task<SkippableTestsDictionary>? _skippableTestsTask;
    private int _itrSkippedTests;

    private TestOptimizationSkippableFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        _settings = settings;
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
            _skippableTestsTask = null;
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
            var tests = skippeableTests.Tests ?? Array.Empty<SkippableTest>();
            Log.Information<string?, int, bool>(
                "TestOptimizationSkippableFeature: CorrelationId = {CorrelationId}, SkippableTests = {Length}, CoverageBackfillSafe = {CoverageBackfillSafe}.",
                skippeableTests.CorrelationId,
                tests.Count,
                skippeableTests.IsCoverageBackfillSafe);

            var skippableTestsBySuiteAndName = new SkippableTestsDictionary();
            foreach (var item in tests)
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
            skippableTestsBySuiteAndName.BackfillData = skippeableTests.Coverage ?? CoverageBackfillData.Missing;
            skippableTestsBySuiteAndName.IsCoverageBackfillSafe = skippeableTests.IsCoverageBackfillSafe;
            skippableTestsBySuiteAndName.HasAmbiguousCoverageScope = HasAmbiguousCoverageScope(tests);
            CoverageBackfillDataStore.Persist(TestOptimization.Instance, skippableTestsBySuiteAndName.BackfillData);
            Log.Debug("TestOptimizationSkippableFeature: SkippableTests dictionary has been built. CorrelationId: {CorrelationId}", skippableTestsBySuiteAndName.CorrelationId);
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
            _skippableTestsTask?.SafeWait();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error waiting for skippable tests task to finish.");
        }
    }

    public IList<SkippableTest> GetSkippableTestsFromSuiteAndName(string suite, string name)
    {
        if (_skippableTestsTask is null)
        {
            return [];
        }

        try
        {
            var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
            if (skippableTestsBySuiteAndName.TryGetValue(suite, out var testsInSuite) &&
                testsInSuite.TryGetValue(name, out var tests))
            {
                return tests;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error waiting for skippable tests task to finish.");
        }

        return [];
    }

    public bool HasSkippableTests()
    {
        if (_skippableTestsTask is null)
        {
            return false;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return skippableTestsBySuiteAndName.Count > 0;
    }

    public string? GetCorrelationId()
    {
        if (_skippableTestsTask is null)
        {
            return null;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return skippableTestsBySuiteAndName.CorrelationId;
    }

    /// <inheritdoc />
    public CoverageBackfillData GetCoverageBackfillData()
    {
        if (_skippableTestsTask is null)
        {
            return CoverageBackfillData.Missing;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return skippableTestsBySuiteAndName.BackfillData;
    }

    /// <inheritdoc />
    public bool IsCoverageBackfillRequired()
    {
        return CoverageBackfillCapability.IsCoverageBackfillRequired(_settings);
    }

    /// <inheritdoc />
    public bool IsCoverageBackfillSafe()
    {
        if (_skippableTestsTask is null)
        {
            return false;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        var coverageBackfillData = skippableTestsBySuiteAndName.BackfillData;
        return skippableTestsBySuiteAndName.IsCoverageBackfillSafe &&
               !skippableTestsBySuiteAndName.HasAmbiguousCoverageScope &&
               coverageBackfillData.IsPresent &&
               coverageBackfillData.IsValid;
    }

    /// <inheritdoc />
    public bool CanSkipWithCoverageBackfill(SkippableTest skippableTest, out string reason)
    {
        reason = string.Empty;
        if (!IsCoverageBackfillRequired())
        {
            return true;
        }

        if (skippableTest.MissingLineCodeCoverage)
        {
            reason = "backend marked the test as missing line coverage";
            return false;
        }

        if (!CoverageBackfillCapability.IsActiveCoverageModeBackfillable(_settings, out reason))
        {
            return false;
        }

        if (_skippableTestsTask is null)
        {
            reason = "skippable-tests response is unavailable";
            return false;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        if (skippableTestsBySuiteAndName.HasAmbiguousCoverageScope)
        {
            reason = "duplicate skippable candidates make the backend coverage aggregate ambiguous";
            return false;
        }

        if (!skippableTestsBySuiteAndName.IsCoverageBackfillSafe)
        {
            reason = "backend coverage aggregate was invalidated by local filtering";
            return false;
        }

        var coverageBackfillData = skippableTestsBySuiteAndName.BackfillData;
        if (!coverageBackfillData.IsPresent)
        {
            reason = "skippable-tests response did not include meta.coverage";
            return false;
        }

        if (!coverageBackfillData.IsValid)
        {
            reason = coverageBackfillData.Error ?? "backend coverage could not be decoded";
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public void RecordTestSkippedByItr()
    {
        Interlocked.Increment(ref _itrSkippedTests);
        CoverageBackfillDataStore.RecordActualItrSkip();
    }

    /// <inheritdoc />
    public bool HasSkippedTestsByItr()
    {
        return Volatile.Read(ref _itrSkippedTests) > 0;
    }

    private static bool HasAmbiguousCoverageScope(ICollection<SkippableTest> tests)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            var key = $"{test.Suite}\0{test.Name}\0{test.RawParameters}";
            if (!seen.Add(key))
            {
                return true;
            }
        }

        return false;
    }

    internal sealed class SkippableTestsDictionary : Dictionary<string, Dictionary<string, IList<SkippableTest>>>
    {
        /// <summary>
        /// Gets or sets the backend correlation id associated with this skippable-test response.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the backend coverage data associated with this skippable-test response.
        /// </summary>
        public CoverageBackfillData BackfillData { get; set; } = CoverageBackfillData.Missing;

        /// <summary>
        /// Gets or sets a value indicating whether the backend coverage aggregate still matches the local candidate set.
        /// </summary>
        public bool IsCoverageBackfillSafe { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether local suite/name/parameter matching cannot uniquely scope backend coverage.
        /// </summary>
        public bool HasAmbiguousCoverageScope { get; set; }
    }
}

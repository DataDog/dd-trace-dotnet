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
    private readonly ITestOptimizationClient _testOptimizationClient;
    private readonly Task<SkippableTestsDictionary>? _skippableTestsTask;
    private readonly Dictionary<string, Task<SkippableTestsDictionary>> _scopedSkippableTestsTasks = new(StringComparer.Ordinal);
    private readonly HashSet<string> _actualSkippedScopes = new(StringComparer.Ordinal);
    private readonly bool _coverageBackfillRequiresScopedRequests;
    private int _itrSkippedTests;

    private TestOptimizationSkippableFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
    {
        _settings = settings;
        _testOptimizationClient = testOptimizationClient;
        if (settings.TestsSkippingEnabled == null && clientSettingsResponse.TestsSkipping.HasValue)
        {
            Log.Information("TestOptimizationSkippableFeature: Tests Skipping has been changed to {Value} by settings api.", clientSettingsResponse.TestsSkipping.Value);
            settings.SetTestsSkippingEnabled(clientSettingsResponse.TestsSkipping.Value);
        }

        _coverageBackfillRequiresScopedRequests = CoverageBackfillCapability.IsCoverageBackfillRequired(settings);
        if (settings.TestsSkippingEnabled == true)
        {
            Log.Information("TestOptimizationSkippableFeature: Test skipping is enabled.");
            _skippableTestsTask = _coverageBackfillRequiresScopedRequests ? null : Task.Run(() => InternalGetSkippableTestsAsync(testOptimizationClient, default));
            Enabled = true;
        }
        else
        {
            Log.Information("TestOptimizationSkippableFeature: Test skipping is disabled.");
            _skippableTestsTask = null;
            Enabled = false;
        }
    }

    public bool Enabled { get; }

    public static ITestOptimizationSkippableFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient)
        => new TestOptimizationSkippableFeature(settings, clientSettingsResponse, testOptimizationClient);

    private static async Task<SkippableTestsDictionary> InternalGetSkippableTestsAsync(ITestOptimizationClient testOptimizationClient, SkippableTestsRequestScope scope)
    {
        // For ITR we need the git metadata upload before consulting the skippable tests.
        // If ITR is disabled we just need to make sure the git upload task has completed before leaving this method.
        await testOptimizationClient.UploadRepositoryChangesAsync().ConfigureAwait(false);

        Log.Debug("TestOptimizationSkippableFeature: Getting skippable tests...");
        var skippeableTests = await testOptimizationClient.GetSkippableTestsAsync(scope).ConfigureAwait(false);
        var tests = skippeableTests.Tests ?? Array.Empty<SkippableTest>();
        Log.Information<string?, string?, int, bool>(
            "TestOptimizationSkippableFeature: CorrelationId = {CorrelationId}, Scope = {Scope}, SkippableTests = {Length}, CoverageBackfillSafe = {CoverageBackfillSafe}.",
            skippeableTests.CorrelationId,
            scope.TestBundle,
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
        skippableTestsBySuiteAndName.Scope = scope;
        skippableTestsBySuiteAndName.BackfillData = skippeableTests.Coverage ?? CoverageBackfillData.Missing;
        skippableTestsBySuiteAndName.IsCoverageBackfillSafe = skippeableTests.IsCoverageBackfillSafe;
        skippableTestsBySuiteAndName.HasAmbiguousCoverageScope = HasAmbiguousCoverageScope(tests);
        CoverageBackfillDataStore.Persist(TestOptimization.Instance, scope, skippableTestsBySuiteAndName.BackfillData);
        Log.Debug("TestOptimizationSkippableFeature: SkippableTests dictionary has been built. CorrelationId: {CorrelationId}", skippableTestsBySuiteAndName.CorrelationId);
        return skippableTestsBySuiteAndName;
    }

    public void WaitForSkippableTaskToFinish()
    {
        try
        {
            _skippableTestsTask?.SafeWait();
            Task<SkippableTestsDictionary>[] scopedTasks;
            lock (_scopedSkippableTestsTasks)
            {
                scopedTasks = _scopedSkippableTestsTasks.Count == 0 ? [] : [.. _scopedSkippableTestsTasks.Values];
            }

            foreach (var scopedTask in scopedTasks)
            {
                scopedTask.SafeWait();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error waiting for skippable tests task to finish.");
        }
    }

    public IList<SkippableTest> GetSkippableTestsFromSuiteAndName(string suite, string name, string? moduleName = null)
    {
        var skippableTestsTask = GetSkippableTestsTask(moduleName);
        if (skippableTestsTask is null)
        {
            return [];
        }

        try
        {
            var skippableTestsBySuiteAndName = skippableTestsTask.SafeGetResult();
            if (skippableTestsBySuiteAndName.TryGetValue(suite, out var testsInSuite) &&
                testsInSuite.TryGetValue(name, out var tests))
            {
                return FilterByModuleScope(tests, moduleName, skippableTestsBySuiteAndName.Scope.HasTestBundle);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error waiting for skippable tests task to finish.");
        }

        return [];
    }

    /// <summary>
    /// Gets the skippable-tests task for the current execution scope, creating a scoped backend request when coverage backfill requires it.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle name for the currently executing test.</param>
    /// <returns>The matching skippable-tests task, or null when a required scope is unavailable.</returns>
    private Task<SkippableTestsDictionary>? GetSkippableTestsTask(string? moduleName)
    {
        if (!_coverageBackfillRequiresScopedRequests)
        {
            return _skippableTestsTask;
        }

        if (StringUtil.IsNullOrEmpty(moduleName))
        {
            Log.Debug("TestOptimizationSkippableFeature: coverage-active skipping requires a test bundle scope, but none is available.");
            return null;
        }

        lock (_scopedSkippableTestsTasks)
        {
            if (!_scopedSkippableTestsTasks.TryGetValue(moduleName!, out var task))
            {
                var scope = SkippableTestsRequestScope.Create(TestOptimization.Instance, moduleName);
                task = Task.Run(() => InternalGetSkippableTestsAsync(_testOptimizationClient, scope));
                _scopedSkippableTestsTasks[moduleName!] = task;
            }

            return task;
        }
    }

    public bool HasSkippableTests()
    {
        if (_coverageBackfillRequiresScopedRequests)
        {
            foreach (var dictionary in GetCompletedScopedDictionaries())
            {
                if (dictionary.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        if (_skippableTestsTask is null)
        {
            return false;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return skippableTestsBySuiteAndName.Count > 0;
    }

    public string? GetCorrelationId()
    {
        if (_coverageBackfillRequiresScopedRequests)
        {
            foreach (var dictionary in GetCompletedScopedDictionaries())
            {
                if (!StringUtil.IsNullOrEmpty(dictionary.CorrelationId))
                {
                    return dictionary.CorrelationId;
                }
            }

            return null;
        }

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
        if (_coverageBackfillRequiresScopedRequests)
        {
            var coverageMaps = new List<CoverageBackfillData>();
            foreach (var dictionary in GetActualSkippedScopedDictionaries())
            {
                coverageMaps.Add(dictionary.BackfillData);
            }

            return CoverageBackfillData.Merge(coverageMaps);
        }

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
        if (_coverageBackfillRequiresScopedRequests)
        {
            var sawActualSkippedScope = false;
            foreach (var dictionary in GetActualSkippedScopedDictionaries())
            {
                sawActualSkippedScope = true;
                if (!IsDictionaryCoverageBackfillSafe(dictionary))
                {
                    return false;
                }
            }

            return sawActualSkippedScope;
        }

        if (_skippableTestsTask is null)
        {
            return false;
        }

        var skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return IsDictionaryCoverageBackfillSafe(skippableTestsBySuiteAndName);
    }

    /// <inheritdoc />
    public bool CanSkipWithCoverageBackfill(SkippableTest skippableTest, string? moduleName, out string reason)
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

        var skippableTestsTask = GetSkippableTestsTask(moduleName);
        if (skippableTestsTask is null)
        {
            reason = "skippable-tests response is unavailable";
            return false;
        }

        var skippableTestsBySuiteAndName = skippableTestsTask.SafeGetResult();
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
    public void RecordTestSkippedByItr(string? moduleName = null)
    {
        Interlocked.Increment(ref _itrSkippedTests);
        if (_coverageBackfillRequiresScopedRequests && !StringUtil.IsNullOrEmpty(moduleName))
        {
            var skippableTestsTask = GetSkippableTestsTask(moduleName);
            if (skippableTestsTask is not null)
            {
                var dictionary = skippableTestsTask.SafeGetResult();
                lock (_actualSkippedScopes)
                {
                    _actualSkippedScopes.Add(moduleName!);
                }

                CoverageBackfillDataStore.RecordActualItrSkip(dictionary.Scope);
                return;
            }
        }

        CoverageBackfillDataStore.RecordActualItrSkip();
    }

    /// <inheritdoc />
    public bool HasSkippedTestsByItr()
    {
        return Volatile.Read(ref _itrSkippedTests) > 0;
    }

    /// <summary>
    /// Gets whether one backend response can safely backfill coverage for the tests skipped from that same response.
    /// </summary>
    /// <param name="skippableTestsBySuiteAndName">Skippable-tests response indexed for local lookup.</param>
    /// <returns>True when the response has valid coverage and no local ambiguity.</returns>
    private static bool IsDictionaryCoverageBackfillSafe(SkippableTestsDictionary skippableTestsBySuiteAndName)
    {
        var coverageBackfillData = skippableTestsBySuiteAndName.BackfillData;
        return skippableTestsBySuiteAndName.IsCoverageBackfillSafe &&
               !skippableTestsBySuiteAndName.HasAmbiguousCoverageScope &&
               coverageBackfillData.IsPresent &&
               coverageBackfillData.IsValid;
    }

    /// <summary>
    /// Enumerates scoped skippable-test responses that have completed without forcing pending backend requests to block.
    /// </summary>
    /// <returns>Completed scoped skippable-test dictionaries.</returns>
    private IEnumerable<SkippableTestsDictionary> GetCompletedScopedDictionaries()
    {
        Task<SkippableTestsDictionary>[] scopedTasks;
        lock (_scopedSkippableTestsTasks)
        {
            scopedTasks = _scopedSkippableTestsTasks.Count == 0 ? [] : [.. _scopedSkippableTestsTasks.Values];
        }

        foreach (var task in scopedTasks)
        {
            if (task.Status == TaskStatus.RanToCompletion)
            {
                yield return task.Result;
            }
        }
    }

    /// <summary>
    /// Enumerates only scoped skippable-test responses that produced at least one actual ITR skip.
    /// </summary>
    /// <returns>Scoped dictionaries for scopes that actually skipped tests.</returns>
    private IEnumerable<SkippableTestsDictionary> GetActualSkippedScopedDictionaries()
    {
        string[] actualSkippedScopes;
        lock (_actualSkippedScopes)
        {
            actualSkippedScopes = _actualSkippedScopes.Count == 0 ? [] : [.. _actualSkippedScopes];
        }

        foreach (var scope in actualSkippedScopes)
        {
            Task<SkippableTestsDictionary>? task;
            lock (_scopedSkippableTestsTasks)
            {
                _scopedSkippableTestsTasks.TryGetValue(scope, out task);
            }

            if (task is not null)
            {
                yield return task.SafeGetResult();
            }
        }
    }

    internal static bool HasAmbiguousCoverageScope(ICollection<SkippableTest> tests)
    {
        var scopedCandidates = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var unscopedCandidates = new HashSet<string>(StringComparer.Ordinal);
        foreach (var test in tests)
        {
            var key = $"{test.Suite}\0{test.Name}\0{test.RawParameters}";
            if (!test.TryGetModuleScope(out var moduleScope))
            {
                if (!unscopedCandidates.Add(key) || scopedCandidates.ContainsKey(key))
                {
                    return true;
                }

                continue;
            }

            if (unscopedCandidates.Contains(key))
            {
                return true;
            }

            if (!scopedCandidates.TryGetValue(key, out var scopes))
            {
                scopes = new HashSet<string>(StringComparer.Ordinal);
                scopedCandidates[key] = scopes;
            }

            if (!scopes.Add(moduleScope))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Keeps module-scoped backend candidates aligned with the currently executing local test module.
    /// </summary>
    /// <param name="tests">Backend candidates that matched suite and name.</param>
    /// <param name="moduleName">Local test module or bundle name, when available.</param>
    /// <param name="requestScopeMatchesModule">Whether the backend request itself was already scoped to the local module.</param>
    /// <returns>Candidates that are valid for the local module scope.</returns>
    private static IList<SkippableTest> FilterByModuleScope(IList<SkippableTest> tests, string? moduleName, bool requestScopeMatchesModule)
    {
        List<SkippableTest>? filtered = null;
        for (var i = 0; i < tests.Count; i++)
        {
            var test = tests[i];
            if ((requestScopeMatchesModule && !test.TryGetModuleScope(out _)) ||
                test.MatchesModuleScope(moduleName))
            {
                filtered?.Add(test);
                continue;
            }

            filtered ??= CopyBefore(tests, i);
        }

        return filtered ?? tests;

        static List<SkippableTest> CopyBefore(IList<SkippableTest> source, int count)
        {
            var result = new List<SkippableTest>(source.Count);
            for (var i = 0; i < count; i++)
            {
                result.Add(source[i]);
            }

            return result;
        }
    }

    internal sealed class SkippableTestsDictionary : Dictionary<string, Dictionary<string, IList<SkippableTest>>>
    {
        /// <summary>
        /// Gets or sets the backend correlation id associated with this skippable-test response.
        /// </summary>
        public string? CorrelationId { get; set; }

        /// <summary>
        /// Gets or sets the backend request scope used to build this dictionary.
        /// </summary>
        public SkippableTestsRequestScope Scope { get; set; }

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

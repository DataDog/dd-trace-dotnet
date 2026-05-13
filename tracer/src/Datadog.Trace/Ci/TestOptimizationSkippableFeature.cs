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
    // Stores scoped skippable responses that actually produced ITR skips, keyed by the backend scope fingerprint when available.
    private readonly Dictionary<string, SkippableTestsDictionary> _actualSkippedDictionaries = new(StringComparer.Ordinal);
    private readonly bool _coverageBackfillRequiresScopedRequests;
    // Keeps scoped tasks in insertion order so shutdown can wait for them without allocating array snapshots.
    private List<Task<SkippableTestsDictionary>>? _scopedSkippableTestsTaskList;

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

            var scopedTaskIndex = 0;
            while (TryGetScopedTaskAt(scopedTaskIndex, out var scopedTask))
            {
                scopedTask.SafeWait();
                scopedTaskIndex++;
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
                _scopedSkippableTestsTaskList ??= new List<Task<SkippableTestsDictionary>>();
                _scopedSkippableTestsTaskList.Add(task);
            }

            return task;
        }
    }

    public bool HasSkippableTests()
    {
        if (_coverageBackfillRequiresScopedRequests)
        {
            return HasCompletedScopedSkippableTests();
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
            return GetCompletedScopedCorrelationId();
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
            return GetActualSkippedCoverageBackfillData();
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
            return AreActualSkippedDictionariesCoverageBackfillSafe();
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
                RecordActualSkippedDictionary(moduleName!, dictionary);
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
    /// Gets a scoped skippable task by index while holding the scoped-task lock.
    /// The caller can then wait outside the lock without allocating a snapshot of the task collection.
    /// </summary>
    /// <param name="index">The scoped-task insertion index.</param>
    /// <param name="task">The scoped-task instance when the index exists.</param>
    /// <returns><c>true</c> when a task exists for the requested index; otherwise, <c>false</c>.</returns>
    private bool TryGetScopedTaskAt(int index, out Task<SkippableTestsDictionary> task)
    {
        lock (_scopedSkippableTestsTasks)
        {
            if (_scopedSkippableTestsTaskList is { Count: > 0 } scopedTasks && index < scopedTasks.Count)
            {
                task = scopedTasks[index];
                return true;
            }
        }

        task = null!;
        return false;
    }

    /// <summary>
    /// Checks completed scoped skippable responses without copying the mutable task collection.
    /// </summary>
    /// <returns><c>true</c> when any completed scoped response contains skippable tests; otherwise, <c>false</c>.</returns>
    private bool HasCompletedScopedSkippableTests()
    {
        lock (_scopedSkippableTestsTasks)
        {
            if (_scopedSkippableTestsTaskList is not { Count: > 0 } scopedTasks)
            {
                return false;
            }

            foreach (var task in scopedTasks)
            {
                if (task.Status == TaskStatus.RanToCompletion && task.Result.Count > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the first completed scoped correlation id without copying the mutable task collection.
    /// </summary>
    /// <returns>The first completed scoped response correlation id, or <c>null</c> when no scoped response has completed.</returns>
    private string? GetCompletedScopedCorrelationId()
    {
        lock (_scopedSkippableTestsTasks)
        {
            if (_scopedSkippableTestsTaskList is not { Count: > 0 } scopedTasks)
            {
                return null;
            }

            foreach (var task in scopedTasks)
            {
                if (task.Status == TaskStatus.RanToCompletion)
                {
                    var correlationId = task.Result.CorrelationId;
                    if (!StringUtil.IsNullOrEmpty(correlationId))
                    {
                        return correlationId;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Records a scoped skippable response that produced at least one actual ITR skip.
    /// </summary>
    /// <param name="moduleName">The test module that requested the scoped response.</param>
    /// <param name="dictionary">The resolved skippable response for the test module.</param>
    private void RecordActualSkippedDictionary(string moduleName, SkippableTestsDictionary dictionary)
    {
        var key = dictionary.Scope.Fingerprint;
        if (key is null || key.Length == 0)
        {
            key = moduleName;
        }

        lock (_actualSkippedDictionaries)
        {
            _actualSkippedDictionaries[key] = dictionary;
        }
    }

    /// <summary>
    /// Gets merged coverage backfill data from the scoped responses that actually produced ITR skips.
    /// </summary>
    /// <returns>The merged coverage backfill data, or missing coverage data when no scoped ITR skip was recorded.</returns>
    private CoverageBackfillData GetActualSkippedCoverageBackfillData()
    {
        lock (_actualSkippedDictionaries)
        {
            if (_actualSkippedDictionaries.Count == 0)
            {
                return CoverageBackfillData.Missing;
            }

            if (_actualSkippedDictionaries.Count == 1)
            {
                foreach (var dictionary in _actualSkippedDictionaries.Values)
                {
                    return dictionary.BackfillData is { IsPresent: true, IsValid: true } ? dictionary.BackfillData : CoverageBackfillData.Missing;
                }
            }

            var coverageMaps = new List<CoverageBackfillData>(_actualSkippedDictionaries.Count);
            foreach (var dictionary in _actualSkippedDictionaries.Values)
            {
                coverageMaps.Add(dictionary.BackfillData);
            }

            return CoverageBackfillData.Merge(coverageMaps);
        }
    }

    /// <summary>
    /// Checks whether all scoped responses that produced actual ITR skips can safely backfill coverage.
    /// </summary>
    /// <returns><c>true</c> when scoped backfill data exists and all entries are safe to merge; otherwise, <c>false</c>.</returns>
    private bool AreActualSkippedDictionariesCoverageBackfillSafe()
    {
        lock (_actualSkippedDictionaries)
        {
            if (_actualSkippedDictionaries.Count == 0)
            {
                return false;
            }

            foreach (var dictionary in _actualSkippedDictionaries.Values)
            {
                if (!IsDictionaryCoverageBackfillSafe(dictionary))
                {
                    return false;
                }
            }

            return true;
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

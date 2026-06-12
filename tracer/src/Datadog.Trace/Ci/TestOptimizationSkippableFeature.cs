// <copyright file="TestOptimizationSkippableFeature.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

    /// <summary>
    /// Reused empty lookup result returned when no skippable response is available for a local test query.
    /// </summary>
    private static readonly IList<SkippableTest> EmptySkippableTests = Array.Empty<SkippableTest>();

    private readonly ITestOptimization _testOptimization;
    private readonly ITestOptimizationClient _testOptimizationClient;
    private readonly Task<SkippableTestsDictionary>? _skippableTestsTask;
    private readonly Dictionary<string, Task<SkippableTestsDictionary>> _scopedSkippableTestsTasks = new(StringComparer.Ordinal);
    // Stores scoped skippable responses that actually produced ITR skips, keyed by the backend scope fingerprint when available.
    private readonly Dictionary<string, ActualSkippedDictionaryState> _actualSkippedDictionaries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ulong, byte> _itrSkippedSessions = new();
    private readonly bool _coverageBackfillRequiresScopedRequests;
    private readonly bool _coverageBackfillRequiresBackendConfigurationValidation;
    private readonly string _coverageBackfillUnsupportedReason;
    // Keeps scoped tasks in insertion order so shutdown can wait for them without allocating array snapshots.
    private List<Task<SkippableTestsDictionary>>? _scopedSkippableTestsTaskList;

    private TestOptimizationSkippableFeature(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient, ITestOptimization testOptimization)
    {
        _testOptimization = testOptimization;
        _testOptimizationClient = testOptimizationClient;
        if (settings.TestsSkippingEnabled == null && clientSettingsResponse.TestsSkipping.HasValue)
        {
            Log.Information("TestOptimizationSkippableFeature: Tests Skipping has been changed to {Value} by settings api.", clientSettingsResponse.TestsSkipping.Value);
            settings.SetTestsSkippingEnabled(clientSettingsResponse.TestsSkipping.Value);
        }

        _coverageBackfillRequiresScopedRequests = CoverageBackfillCapability.IsCoverageBackfillRequired(settings);
        var requiresBackendConfigurationValidation = false;
        _coverageBackfillUnsupportedReason = string.Empty;
        if (_coverageBackfillRequiresScopedRequests &&
            !CoverageBackfillCapability.IsActiveCoverageModeBackfillableForSkippableResponse(settings, out var unsupportedReason, out requiresBackendConfigurationValidation))
        {
            _coverageBackfillUnsupportedReason = unsupportedReason;
        }

        _coverageBackfillRequiresBackendConfigurationValidation = requiresBackendConfigurationValidation;

        if (settings.TestsSkippingEnabled == true)
        {
            Log.Information("TestOptimizationSkippableFeature: Test skipping is enabled.");
            _skippableTestsTask = _coverageBackfillRequiresScopedRequests ? null : Task.Run(() => InternalGetSkippableTestsAsync(testOptimizationClient, testOptimization, default));
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

    public static ITestOptimizationSkippableFeature Create(TestOptimizationSettings settings, TestOptimizationClient.SettingsResponse clientSettingsResponse, ITestOptimizationClient testOptimizationClient, ITestOptimization testOptimization)
        => new TestOptimizationSkippableFeature(settings, clientSettingsResponse, testOptimizationClient, testOptimization);

    private static async Task<SkippableTestsDictionary> InternalGetSkippableTestsAsync(ITestOptimizationClient testOptimizationClient, ITestOptimization testOptimization, SkippableTestsRequestScope scope)
    {
        // For ITR we need the git metadata upload before consulting the skippable tests.
        // If ITR is disabled we just need to make sure the git upload task has completed before leaving this method.
        await testOptimizationClient.UploadRepositoryChangesAsync().ConfigureAwait(false);

        Log.Debug("TestOptimizationSkippableFeature: Getting skippable tests...");
        var skippableTests = await testOptimizationClient.GetSkippableTestsAsync(scope).ConfigureAwait(false);
        var tests = skippableTests.Tests ?? Array.Empty<SkippableTest>();
        Log.Information<string?, string?, int, bool>(
            "TestOptimizationSkippableFeature: CorrelationId = {CorrelationId}, Scope = {Scope}, SkippableTests = {Length}, CoverageBackfillSafe = {CoverageBackfillSafe}.",
            skippableTests.CorrelationId,
            scope.TestBundle,
            tests.Count,
            skippableTests.IsCoverageBackfillSafe);

        var skippableTestsBySuiteAndName = BuildSkippableTestsDictionary(scope, skippableTests, tests);
        CoverageBackfillDataStore.Persist(testOptimization, scope, skippableTestsBySuiteAndName.BackfillData);
        Log.Debug("TestOptimizationSkippableFeature: SkippableTests dictionary has been built. CorrelationId: {CorrelationId}", skippableTestsBySuiteAndName.CorrelationId);
        return skippableTestsBySuiteAndName;
    }

    /// <summary>
    /// Indexes a skippable-tests response by suite and name while preserving metadata needed for ITR decisions and coverage backfill.
    /// </summary>
    /// <param name="scope">Request scope used to retrieve the response.</param>
    /// <param name="response">Raw skippable-tests response returned by the client.</param>
    /// <param name="tests">Skippable tests from the response, or an empty collection when the response omitted them.</param>
    /// <returns>A lookup dictionary enriched with backend response metadata.</returns>
    private static SkippableTestsDictionary BuildSkippableTestsDictionary(SkippableTestsRequestScope scope, TestOptimizationClient.SkippableTestsResponse response, ICollection<SkippableTest> tests)
    {
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

        skippableTestsBySuiteAndName.CorrelationId = response.CorrelationId;
        skippableTestsBySuiteAndName.Scope = scope;
        skippableTestsBySuiteAndName.BackfillData = response.Coverage ?? CoverageBackfillData.Missing;
        skippableTestsBySuiteAndName.IsCoverageBackfillSafe = response.IsCoverageBackfillSafe;
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
            return EmptySkippableTests;
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

        return EmptySkippableTests;
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

        if (StringUtil.IsNullOrWhiteSpace(moduleName))
        {
            Log.Debug("TestOptimizationSkippableFeature: coverage-active skipping requires a test bundle scope, but none is available.");
            return null;
        }

        var scopeModuleName = moduleName!;
        lock (_scopedSkippableTestsTasks)
        {
            if (!_scopedSkippableTestsTasks.TryGetValue(scopeModuleName, out var task))
            {
                var scope = SkippableTestsRequestScope.Create(_testOptimization, scopeModuleName);
                task = Task.Run(() => InternalGetSkippableTestsAsync(_testOptimizationClient, _testOptimization, scope));
                _scopedSkippableTestsTasks[scopeModuleName] = task;
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

        return TryGetUnscopedSkippableTests(out var skippableTestsBySuiteAndName) && skippableTestsBySuiteAndName.Count > 0;
    }

    public string? GetCorrelationId(string? moduleName = null)
    {
        if (_coverageBackfillRequiresScopedRequests)
        {
            return GetCompletedScopedCorrelationId(moduleName) ?? GetActualSkippedCorrelationId(moduleName);
        }

        return TryGetUnscopedSkippableTests(out var skippableTestsBySuiteAndName) ? skippableTestsBySuiteAndName.CorrelationId : null;
    }

    /// <inheritdoc />
    public CoverageBackfillData GetCoverageBackfillData()
    {
        if (_coverageBackfillRequiresScopedRequests)
        {
            return GetActualSkippedCoverageBackfillData();
        }

        return TryGetUnscopedSkippableTests(out var skippableTestsBySuiteAndName) ? skippableTestsBySuiteAndName.BackfillData : CoverageBackfillData.Missing;
    }

    /// <inheritdoc />
    public bool IsCoverageBackfillRequired()
    {
        return _coverageBackfillRequiresScopedRequests;
    }

    /// <inheritdoc />
    public bool IsCoverageBackfillSafe()
    {
        if (_coverageBackfillRequiresScopedRequests)
        {
            return AreActualSkippedDictionariesCoverageBackfillSafe();
        }

        return TryGetUnscopedSkippableTests(out var skippableTestsBySuiteAndName) && IsDictionaryCoverageBackfillSafe(skippableTestsBySuiteAndName);
    }

    /// <inheritdoc />
    public bool CanSkipWithCoverageBackfill(SkippableTest skippableTest, string? moduleName, out string reason)
    {
        reason = string.Empty;
        if (!IsCoverageBackfillRequired())
        {
            return true;
        }

        if (!StringUtil.IsNullOrEmpty(_coverageBackfillUnsupportedReason))
        {
            reason = _coverageBackfillUnsupportedReason;
            return false;
        }

        if (_coverageBackfillRequiresBackendConfigurationValidation &&
            !CanSkipWithBackendConfigurationScope(skippableTest, moduleName, out reason))
        {
            return false;
        }

        if (skippableTest.MissingLineCodeCoverage == true)
        {
            reason = "backend marked the test as missing line coverage";
            return false;
        }

        return true;
    }

    /// <inheritdoc />
    public void RecordTestSkipCoverageBackfill(string? moduleName = null)
    {
        var sessionId = GetCurrentSessionId();
        if (!_coverageBackfillRequiresScopedRequests || StringUtil.IsNullOrWhiteSpace(moduleName))
        {
            CoverageBackfillDataStore.RecordActualItrSkip(sessionId);
            return;
        }

        try
        {
            var skippableTestsTask = GetSkippableTestsTask(moduleName);
            if (skippableTestsTask is null)
            {
                CoverageBackfillDataStore.RecordActualItrSkip(sessionId);
                return;
            }

            var dictionary = skippableTestsTask.SafeGetResult();
            CoverageBackfillDataStore.RecordActualItrSkip(sessionId, dictionary.Scope);
            if (IsDictionaryCoverageBackfillSafeForCurrentRun(dictionary))
            {
                CoverageBackfillDataStore.RecordBackfillableItrSkipScope(sessionId, dictionary.Scope);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error recording scoped ITR skip coverage backfill state.");
            CoverageBackfillDataStore.RecordActualItrSkip(sessionId);
        }
    }

    /// <inheritdoc />
    public void RecordTestSkipCoverageBackfill(SkippableTest skippableTest, string? moduleName = null)
    {
        var sessionId = GetCurrentSessionId();
        if (!_coverageBackfillRequiresScopedRequests || StringUtil.IsNullOrWhiteSpace(moduleName))
        {
            CoverageBackfillDataStore.RecordActualItrSkip(sessionId);
            return;
        }

        try
        {
            var skippableTestsTask = GetSkippableTestsTask(moduleName);
            if (skippableTestsTask is null)
            {
                CoverageBackfillDataStore.RecordActualItrSkip(sessionId);
                return;
            }

            var dictionary = skippableTestsTask.SafeGetResult();
            if (!ContainsCoverageBackfillCandidate(dictionary, skippableTest))
            {
                CoverageBackfillDataStore.RecordActualItrSkip(sessionId, dictionary.Scope);
                return;
            }

            RecordActualSkipCoverageState(sessionId, moduleName, dictionary);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error recording scoped ITR skip coverage backfill state.");
            CoverageBackfillDataStore.RecordActualItrSkip(sessionId);
        }
    }

    /// <inheritdoc />
    public void RecordTestSkippedByItr(ulong sessionId, string? moduleName = null)
    {
        if (sessionId != 0)
        {
            _itrSkippedSessions.TryAdd(sessionId, 0);
        }
    }

    /// <inheritdoc />
    public bool HasSkippedTestsByItr(ulong sessionId)
    {
        return sessionId != 0 && _itrSkippedSessions.ContainsKey(sessionId);
    }

    /// <summary>
    /// Gets whether one backend response has usable coverage data for ITR backfill.
    /// </summary>
    /// <param name="skippableTestsBySuiteAndName">Skippable-tests response indexed for local lookup.</param>
    /// <returns>True when the response has present, valid backend coverage.</returns>
    private static bool IsDictionaryCoverageBackfillSafe(SkippableTestsDictionary skippableTestsBySuiteAndName)
    {
        var coverageBackfillData = skippableTestsBySuiteAndName.BackfillData;
        return skippableTestsBySuiteAndName.IsCoverageBackfillSafe &&
               coverageBackfillData.IsPresent &&
               coverageBackfillData.IsValid;
    }

    private static bool HasHomogeneousBackendConfigurations(SkippableTestsDictionary skippableTestsBySuiteAndName, out string reason)
    {
        var hasConfigurations = false;
        TestsConfigurations firstConfigurations = default;

        foreach (var testsInSuite in skippableTestsBySuiteAndName.Values)
        {
            foreach (var tests in testsInSuite.Values)
            {
                foreach (var candidate in tests)
                {
                    if (candidate.Configurations is not { } candidateConfigurations)
                    {
                        reason = "backend skippable response did not include configurations for every target-framework scoped candidate";
                        return false;
                    }

                    if (!hasConfigurations)
                    {
                        firstConfigurations = candidateConfigurations;
                        hasConfigurations = true;
                    }
                    else if (!TestsConfigurationsEqual(firstConfigurations, candidateConfigurations))
                    {
                        reason = "backend skippable response included multiple configurations for the selected target-framework scope";
                        return false;
                    }
                }
            }
        }

        if (!hasConfigurations)
        {
            reason = "backend skippable response did not include configurations for the selected target-framework scope";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static TestsConfigurations GetFirstBackendConfigurations(SkippableTestsDictionary skippableTestsBySuiteAndName)
    {
        foreach (var testsInSuite in skippableTestsBySuiteAndName.Values)
        {
            foreach (var tests in testsInSuite.Values)
            {
                foreach (var candidate in tests)
                {
                    if (candidate.Configurations is { } candidateConfigurations)
                    {
                        return candidateConfigurations;
                    }
                }
            }
        }

        return default;
    }

    private static bool TestsConfigurationsEqual(TestsConfigurations first, TestsConfigurations second)
        => string.Equals(first.OSPlatform, second.OSPlatform, StringComparison.Ordinal) &&
           string.Equals(first.OSVersion, second.OSVersion, StringComparison.Ordinal) &&
           string.Equals(first.OSArchitecture, second.OSArchitecture, StringComparison.Ordinal) &&
           string.Equals(first.RuntimeName, second.RuntimeName, StringComparison.Ordinal) &&
           string.Equals(first.RuntimeVersion, second.RuntimeVersion, StringComparison.Ordinal) &&
           string.Equals(first.RuntimeArchitecture, second.RuntimeArchitecture, StringComparison.Ordinal) &&
           string.Equals(first.TestBundle, second.TestBundle, StringComparison.Ordinal) &&
           CustomConfigurationsEqual(first.Custom, second.Custom);

    private static bool CustomConfigurationsEqual(Dictionary<string, string>? first, Dictionary<string, string>? second)
    {
        if (first is null || first.Count == 0)
        {
            return second is null || second.Count == 0;
        }

        if (second is null || first.Count != second.Count)
        {
            return false;
        }

        foreach (var item in first)
        {
            if (!second.TryGetValue(item.Key, out var value) ||
                !string.Equals(item.Value, value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ContainsCoverageBackfillCandidate(SkippableTestsDictionary skippableTestsBySuiteAndName, SkippableTest expectedCandidate)
    {
        foreach (var testsInSuite in skippableTestsBySuiteAndName.Values)
        {
            foreach (var tests in testsInSuite.Values)
            {
                foreach (var candidate in tests)
                {
                    if (IsSameSkippableCandidate(candidate, expectedCandidate))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool IsSameSkippableCandidate(SkippableTest first, SkippableTest second)
    {
        return string.Equals(GetCoverageBackfillCandidateKey(first), GetCoverageBackfillCandidateKey(second), StringComparison.Ordinal);
    }

    private static string GetCoverageBackfillCandidateKey(SkippableTest candidate)
    {
        return $"{candidate.Suite}\0{candidate.Name}\0{candidate.RawParameters}\0{GetModuleScopeOrEmpty(candidate)}";
    }

    private static string GetModuleScopeOrEmpty(SkippableTest candidate)
    {
        return candidate.TryGetModuleScope(out var moduleScope) ? moduleScope : string.Empty;
    }

    private bool IsDictionaryCoverageBackfillSafeForCurrentRun(SkippableTestsDictionary skippableTestsBySuiteAndName)
        => IsDictionaryCoverageBackfillSafe(skippableTestsBySuiteAndName) &&
           (!_coverageBackfillRequiresBackendConfigurationValidation ||
            HasHomogeneousBackendConfigurations(skippableTestsBySuiteAndName, out _));

    private bool CanSkipWithBackendConfigurationScope(SkippableTest skippableTest, string? moduleName, out string reason)
    {
        if (!TryGetCoverageBackfillDictionary(moduleName, out var skippableTestsBySuiteAndName))
        {
            reason = "backend configurations could not be retrieved for the selected target-framework scope";
            return false;
        }

        if (!HasHomogeneousBackendConfigurations(skippableTestsBySuiteAndName, out reason))
        {
            return false;
        }

        if (skippableTest.Configurations is not { } candidateConfigurations)
        {
            reason = "backend skippable candidate did not include configurations for the selected target-framework scope";
            return false;
        }

        if (!TestsConfigurationsEqual(candidateConfigurations, GetFirstBackendConfigurations(skippableTestsBySuiteAndName)))
        {
            reason = "backend skippable candidate configurations do not match the selected target-framework scope";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Gets the skippable-tests response that owns the current backfill skip decision without turning request failures into skip denials.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle name for the currently executing test.</param>
    /// <param name="skippableTestsBySuiteAndName">The completed response dictionary, when one is available.</param>
    /// <returns><c>true</c> when a response dictionary was retrieved; otherwise, <c>false</c>.</returns>
    private bool TryGetCoverageBackfillDictionary(string? moduleName, out SkippableTestsDictionary skippableTestsBySuiteAndName)
    {
        var skippableTestsTask = GetSkippableTestsTask(moduleName);
        if (skippableTestsTask is null)
        {
            skippableTestsBySuiteAndName = null!;
            return false;
        }

        try
        {
            skippableTestsBySuiteAndName = skippableTestsTask.SafeGetResult();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestOptimizationSkippableFeature: Error waiting for skippable tests task to finish.");
            skippableTestsBySuiteAndName = null!;
            return false;
        }
    }

    /// <summary>
    /// Gets the unscoped skippable-tests response, if the feature created one during initialization.
    /// </summary>
    /// <param name="skippableTestsBySuiteAndName">The completed unscoped response dictionary.</param>
    /// <returns><c>true</c> when the unscoped response task exists and completed successfully.</returns>
    private bool TryGetUnscopedSkippableTests(out SkippableTestsDictionary skippableTestsBySuiteAndName)
    {
        if (_skippableTestsTask is null)
        {
            skippableTestsBySuiteAndName = null!;
            return false;
        }

        skippableTestsBySuiteAndName = _skippableTestsTask.SafeGetResult();
        return true;
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
    /// Gets the completed scoped correlation id for a local module without creating a late backend request.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle name.</param>
    /// <returns>The matching scoped response correlation id, or <c>null</c> when no matching completed response exists.</returns>
    private string? GetCompletedScopedCorrelationId(string? moduleName)
    {
        if (StringUtil.IsNullOrWhiteSpace(moduleName))
        {
            return null;
        }

        lock (_scopedSkippableTestsTasks)
        {
            if (!_scopedSkippableTestsTasks.TryGetValue(moduleName!, out var task) ||
                task.Status != TaskStatus.RanToCompletion)
            {
                return null;
            }

            var correlationId = task.Result.CorrelationId;
            return StringUtil.IsNullOrEmpty(correlationId) ? null : correlationId;
        }
    }

    /// <summary>
    /// Gets a completed scoped correlation id from dictionaries that already recorded an actual ITR skip.
    /// </summary>
    /// <param name="moduleName">Local test module or bundle name.</param>
    /// <returns>The matching actual-skip response correlation id, or <c>null</c> when no match exists.</returns>
    private string? GetActualSkippedCorrelationId(string? moduleName)
    {
        if (StringUtil.IsNullOrEmpty(moduleName))
        {
            return null;
        }

        lock (_actualSkippedDictionaries)
        {
            foreach (var state in _actualSkippedDictionaries.Values)
            {
                var dictionary = state.Dictionary;
                if (string.Equals(dictionary.Scope.TestBundle, moduleName, StringComparison.Ordinal))
                {
                    return StringUtil.IsNullOrEmpty(dictionary.CorrelationId) ? null : dictionary.CorrelationId;
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
    private ActualSkippedDictionaryState GetOrAddActualSkippedDictionary(string moduleName, SkippableTestsDictionary dictionary)
    {
        var key = dictionary.Scope.Fingerprint;
        if (key is null || key.Length == 0)
        {
            key = moduleName;
        }

        lock (_actualSkippedDictionaries)
        {
            if (_actualSkippedDictionaries.TryGetValue(key, out var state))
            {
                return state;
            }

            state = new ActualSkippedDictionaryState(dictionary);
            _actualSkippedDictionaries[key] = state;
            return state;
        }
    }

    /// <summary>
    /// Persists the backend coverage scope for a test that is being skipped by ITR.
    /// </summary>
    /// <param name="sessionId">Test session span id that owns the skipped test.</param>
    /// <param name="moduleName">The local module or bundle that produced the skip decision.</param>
    /// <param name="dictionary">The resolved skippable response for the skipped test.</param>
    private void RecordActualSkipCoverageState(ulong sessionId, string? moduleName, SkippableTestsDictionary dictionary)
    {
        lock (_actualSkippedDictionaries)
        {
            GetOrAddActualSkippedDictionary(moduleName ?? string.Empty, dictionary);
        }

        CoverageBackfillDataStore.RecordActualItrSkip(sessionId, dictionary.Scope);
        if (IsDictionaryCoverageBackfillSafeForCurrentRun(dictionary))
        {
            CoverageBackfillDataStore.RecordBackfillableItrSkipScope(sessionId, dictionary.Scope);
        }
    }

    private ulong GetCurrentSessionId()
    {
        var moduleSessionId = TestModule.Current?.Tags.SessionId ?? 0;
        if (moduleSessionId != 0)
        {
            return moduleSessionId;
        }

        return TestSession.Current?.Tags.SessionId ?? 0;
    }

    /// <summary>
    /// Gets merged coverage backfill data from scoped responses that actually produced ITR skips.
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
                foreach (var state in _actualSkippedDictionaries.Values)
                {
                    var dictionary = state.Dictionary;
                    return IsDictionaryCoverageBackfillSafeForCurrentRun(dictionary) ? dictionary.BackfillData : CoverageBackfillData.Missing;
                }
            }

            var coverageMaps = new List<CoverageBackfillData>(_actualSkippedDictionaries.Count);
            foreach (var state in _actualSkippedDictionaries.Values)
            {
                var dictionary = state.Dictionary;
                if (IsDictionaryCoverageBackfillSafeForCurrentRun(dictionary))
                {
                    coverageMaps.Add(dictionary.BackfillData);
                }
            }

            return coverageMaps.Count == 0 ? CoverageBackfillData.Missing : CoverageBackfillData.Merge(coverageMaps);
        }
    }

    /// <summary>
    /// Checks whether any scoped response that produced actual ITR skips has usable backend coverage.
    /// </summary>
    /// <returns><c>true</c> when scoped backfill data exists and is safe to merge; otherwise, <c>false</c>.</returns>
    private bool AreActualSkippedDictionariesCoverageBackfillSafe()
    {
        lock (_actualSkippedDictionaries)
        {
            if (_actualSkippedDictionaries.Count == 0)
            {
                return false;
            }

            foreach (var state in _actualSkippedDictionaries.Values)
            {
                if (IsDictionaryCoverageBackfillSafeForCurrentRun(state.Dictionary))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Keeps module-scoped backend candidates aligned with the currently executing local test module.
    /// </summary>
    /// <param name="tests">Backend candidates that matched suite and name.</param>
    /// <param name="moduleName">Local test module or bundle name, when available.</param>
    /// <param name="requestScopeMatchesModule">Whether the backend request itself was already scoped to the local module.</param>
    /// <returns>Candidates that are valid for the local module scope.</returns>
    private IList<SkippableTest> FilterByModuleScope(IList<SkippableTest> tests, string? moduleName, bool requestScopeMatchesModule)
    {
        var filtered = new List<SkippableTest>(tests.Count);
        for (var i = 0; i < tests.Count; i++)
        {
            var test = tests[i];
            if ((requestScopeMatchesModule && !test.TryGetModuleScope(out _)) ||
                test.MatchesModuleScope(moduleName))
            {
                filtered.Add(test);
            }
        }

        return filtered.Count == tests.Count ? tests : filtered;
    }

    private sealed class ActualSkippedDictionaryState
    {
        public ActualSkippedDictionaryState(SkippableTestsDictionary dictionary)
        {
            Dictionary = dictionary;
        }

        public SkippableTestsDictionary Dictionary { get; }
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
    }
}

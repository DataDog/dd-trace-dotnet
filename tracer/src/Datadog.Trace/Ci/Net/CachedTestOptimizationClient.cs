// <copyright file="CachedTestOptimizationClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;

namespace Datadog.Trace.Ci.Net;

internal sealed class CachedTestOptimizationClient : ITestOptimizationClient
{
    private readonly ITestOptimizationClient _client;
    private readonly Lazy<Task<TestOptimizationClient.SettingsResponse>> _settingsWithoutFrameworkInfo;
    private readonly Lazy<Task<TestOptimizationClient.SettingsResponse>> _settingsWithFrameworkInfo;
    private readonly Lazy<Task<TestOptimizationClient.KnownTestsResponse>> _knownTests;
    private readonly Lazy<Task<TestOptimizationClient.SearchCommitResponse>> _commits;
    private readonly Lazy<Task<TestOptimizationClient.SkippableTestsResponse>> _skippableTests;
    private readonly Lazy<Task<TestOptimizationClient.TestManagementResponse>> _testManagementTests;
    private readonly Lazy<Task<long>> _uploadRepositoryChanges;

    public CachedTestOptimizationClient(ITestOptimizationClient client)
    {
        _client = client;
        _settingsWithoutFrameworkInfo = new(() => _client.GetSettingsAsync(true));
        _settingsWithFrameworkInfo = new(() => _client.GetSettingsAsync(false));
        _knownTests = new(_client.GetKnownTestsAsync);
        _commits = new(_client.GetCommitsAsync);
        _skippableTests = new(_client.GetSkippableTestsAsync);
        _uploadRepositoryChanges = new(_client.UploadRepositoryChangesAsync);
        _testManagementTests = new(_client.GetTestManagementTests);
    }

    public async Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
    {
        if (skipFrameworkInfo)
        {
            return await _settingsWithoutFrameworkInfo.Value.ConfigureAwait(false);
        }

        return await _settingsWithFrameworkInfo.Value.ConfigureAwait(false);
    }

    public async Task<TestOptimizationClient.KnownTestsResponse> GetKnownTestsAsync()
        => await _knownTests.Value.ConfigureAwait(false);

    public async Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync()
        => await _commits.Value.ConfigureAwait(false);

    public async Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync()
        => await _skippableTests.Value.ConfigureAwait(false);

    public async Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
        => await _client.SendPackFilesAsync(commitSha, commitsToInclude, commitsToExclude).ConfigureAwait(false);

    public async Task<long> UploadRepositoryChangesAsync()
        => await _uploadRepositoryChanges.Value.ConfigureAwait(false);

    public async Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests()
        => await _testManagementTests.Value.ConfigureAwait(false);
}

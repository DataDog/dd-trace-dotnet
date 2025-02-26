// <copyright file="NoopTestOptimizationClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.Net;

internal sealed class NoopTestOptimizationClient : ITestOptimizationClient
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<NoopTestOptimizationClient>();

    public Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
    {
        Log.Debug("NoopTestOptimizationClient: Getting settings...");
        return Task.FromResult<TestOptimizationClient.SettingsResponse>(default);
    }

    public Task<TestOptimizationClient.EarlyFlakeDetectionResponse> GetEarlyFlakeDetectionTestsAsync()
    {
        Log.Debug("NoopTestOptimizationClient: Getting early flake detection tests...");
        return Task.FromResult<TestOptimizationClient.EarlyFlakeDetectionResponse>(default);
    }

    public Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync()
    {
        Log.Debug("NoopTestOptimizationClient: Getting commits...");
        return Task.FromResult<TestOptimizationClient.SearchCommitResponse>(default);
    }

    public Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync()
    {
        Log.Debug("NoopTestOptimizationClient: Getting skippable tests...");
        return Task.FromResult<TestOptimizationClient.SkippableTestsResponse>(default);
    }

    public Task<TestOptimizationClient.ImpactedTestsDetectionResponse> GetImpactedTestsDetectionFilesAsync()
    {
        Log.Debug("NoopTestOptimizationClient: Getting impacted tests detection files...");
        return Task.FromResult<TestOptimizationClient.ImpactedTestsDetectionResponse>(default);
    }

    public Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
    {
        Log.Debug("NoopTestOptimizationClient: Sending pack files...");
        return Task.FromResult<long>(0);
    }

    public Task<long> UploadRepositoryChangesAsync()
    {
        Log.Debug("NoopTestOptimizationClient: Uploading repository changes...");
        return Task.FromResult<long>(0);
    }
}

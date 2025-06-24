// <copyright file="ITestOptimizationClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Threading.Tasks;

namespace Datadog.Trace.Ci.Net;

internal interface ITestOptimizationClient
{
    Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false);

    Task<TestOptimizationClient.KnownTestsResponse> GetKnownTestsAsync();

    Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync();

    Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync();

    Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude);

    Task<long> UploadRepositoryChangesAsync();

    Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests();
}

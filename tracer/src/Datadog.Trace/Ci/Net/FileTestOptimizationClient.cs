// <copyright file="FileTestOptimizationClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.VendoredMicrosoftCode.System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Net;

internal sealed class FileTestOptimizationClient : ITestOptimizationClient
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FileTestOptimizationClient));
    private readonly ITestOptimizationClient _testOptimizationClient;
    private readonly string _cacheFolder;

    internal FileTestOptimizationClient(ITestOptimizationClient testOptimizationClient, ITestOptimization testOptimization)
    {
        _testOptimizationClient = testOptimizationClient;
        var workingDirectory = testOptimization.CIValues.WorkspacePath ?? Environment.CurrentDirectory;
        var cacheFolder = Path.Combine(workingDirectory, ".dd", testOptimization.RunId, "http");
        if (!Directory.Exists(cacheFolder))
        {
            Directory.CreateDirectory(cacheFolder);
        }

        _cacheFolder = cacheFolder;
    }

    public async Task<TestOptimizationClient.SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
    {
        using var cd = CodeDuration.Create();
        const string keySkipFrameworkInfoFalse = "getSettings-false.json";
        const string keySkipFrameworkInfoTrue = "getSettings-true.json";
        var key = skipFrameworkInfo ? keySkipFrameworkInfoTrue : keySkipFrameworkInfoFalse;

        if (TryReadPayload<TestOptimizationClient.SettingsResponse>(key, out var payload))
        {
            return payload;
        }

        var response = await _testOptimizationClient.GetSettingsAsync(skipFrameworkInfo).ConfigureAwait(false);
        if (response.RequireGit == false)
        {
            // we store the value if RequireGit is false (means that another settings request needs to be made with updated data)
            WritePayload(key, response);
        }

        return response;
    }

    public async Task<TestOptimizationClient.KnownTestsResponse> GetKnownTestsAsync()
    {
        using var cd = CodeDuration.Create();
        const string key = "getKnownTests.json";
        if (TryReadPayload<TestOptimizationClient.KnownTestsResponse>(key, out var payload))
        {
            return payload;
        }

        var response = await _testOptimizationClient.GetKnownTestsAsync().ConfigureAwait(false);
        WritePayload(key, response);
        return response;
    }

    public async Task<TestOptimizationClient.SearchCommitResponse> GetCommitsAsync()
    {
        using var cd = CodeDuration.Create();
        const string key = "getCommits.json";
        if (TryReadPayload<TestOptimizationClient.SearchCommitResponse>(key, out var payload))
        {
            return payload;
        }

        var response = await _testOptimizationClient.GetCommitsAsync().ConfigureAwait(false);
        WritePayload(key, response);
        return response;
    }

    public async Task<TestOptimizationClient.SkippableTestsResponse> GetSkippableTestsAsync()
    {
        using var cd = CodeDuration.Create();
        const string key = "getSkippableTests.json";
        if (TryReadPayload<TestOptimizationClient.SkippableTestsResponse>(key, out var payload))
        {
            return payload;
        }

        var response = await _testOptimizationClient.GetSkippableTestsAsync().ConfigureAwait(false);
        WritePayload(key, response);
        return response;
    }

    public async Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
    {
        return await _testOptimizationClient.SendPackFilesAsync(commitSha, commitsToInclude, commitsToExclude).ConfigureAwait(false);
    }

    public async Task<long> UploadRepositoryChangesAsync()
    {
        return await _testOptimizationClient.UploadRepositoryChangesAsync().ConfigureAwait(false);
    }

    public async Task<TestOptimizationClient.TestManagementResponse> GetTestManagementTests()
    {
        using var cd = CodeDuration.Create();
        const string key = "getTestManagement.json";
        if (TryReadPayload<TestOptimizationClient.TestManagementResponse>(key, out var payload))
        {
            return payload!;
        }

        var response = await _testOptimizationClient.GetTestManagementTests().ConfigureAwait(false);
        WritePayload(key, response);
        return response;
    }

    private bool TryReadPayload<T>(string name, [NotNullWhen(true)] out T? payload)
    {
        try
        {
            var file = Path.Combine(_cacheFolder, name);
            if (File.Exists(file))
            {
                var value = File.ReadAllText(file);
                payload = JsonConvert.DeserializeObject<T>(value);
                Log.Debug("FileTestOptimizationClient: Loaded from: {File}", file);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FileTestOptimizationClient: error on TryReadPayload.");
        }

        payload = default;
        return false;
    }

    private void WritePayload(string name, object value)
    {
        var file = Path.Combine(_cacheFolder, name);
        Log.Debug("FileTestOptimizationClient: Writing to: {File}", file);
        File.WriteAllText(file, JsonConvert.SerializeObject(value));
    }
}

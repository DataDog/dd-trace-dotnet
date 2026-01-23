// <copyright file="TestOptimizationClient.GetCommitsAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Vendors.Newtonsoft.Json;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Datadog.Trace.Ci.Net;

internal sealed partial class TestOptimizationClient
{
    private const string SearchCommitsUrlPath = "api/v2/git/repository/search_commits";
    private const string CommitType = "commit";
    private Uri? _searchCommitsUrl;

    public async Task<SearchCommitResponse> GetCommitsAsync()
    {
        var localCommits = GitCommandHelper.GetLocalCommits(_workingDirectory);
        if (localCommits.Length == 0)
        {
            Log.Debug("TestOptimizationClient: Local commits not found. (since 1 month ago)");
            return new SearchCommitResponse(null, null, false);
        }

        Log.Debug<int>("TestOptimizationClient: Local commits = {Count}. Searching commits...", localCommits.Length);

        _searchCommitsUrl ??= GetUriFromPath(SearchCommitsUrlPath);
        var commitRequests = new Data<object>[localCommits.Length];
        for (var i = 0; i < localCommits.Length; i++)
        {
            commitRequests[i] = new Data<object>(localCommits[i], CommitType, null);
        }

        var jsonQuery = JsonConvert.SerializeObject(new DataArrayEnvelope<Data<object>>(commitRequests, _repositoryUrl), SerializerSettings);
        Log.Debug("TestOptimizationClient: Commits.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<SearchCommitsCallbacks>(_searchCommitsUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Search commit request failed.");
            throw;
        }

        Log.Debug("TestOptimizationClient: Commits.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return new SearchCommitResponse(localCommits, null, false);
        }

        var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<Data<object>>>(queryResponse);
        if (deserializedResult.Data is null || deserializedResult.Data.Length == 0)
        {
            return new SearchCommitResponse(localCommits, null, true);
        }

        var stringArray = new string[deserializedResult.Data.Length];
        for (var i = 0; i < deserializedResult.Data.Length; i++)
        {
            var value = deserializedResult.Data[i].Id;
            if (value is not null)
            {
                if (ShaRegex.Matches(value).Count != 1)
                {
                    ThrowHelper.ThrowException($"The value '{value}' is not a valid Sha.");
                }

                stringArray[i] = value;
            }
        }

        return new SearchCommitResponse(localCommits, stringArray, true);
    }

    private readonly struct SearchCommitsCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommits(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsSearchCommitsMs(MetricTags.CIVisibilityResponseCompressed.Uncompressed, totalMs);
        }
    }

    internal readonly struct SearchCommitResponse
    {
        [JsonProperty("localCommits")]
        public readonly string[] LocalCommits;

        [JsonProperty("remoteCommits")]
        public readonly string[] RemoteCommits;

        [JsonProperty("isOk")]
        public readonly bool IsOk;

        [JsonProperty("hasCommits")]
        public readonly bool HasCommits;

        [JsonProperty("missingCommits")]
        public readonly string[] MissingCommits;

        public SearchCommitResponse(string[]? localCommits, string[]? remoteCommits, bool isOk)
        {
            LocalCommits = localCommits ?? [];
            RemoteCommits = remoteCommits ?? [];
            IsOk = isOk;
            HasCommits = LocalCommits.Length > 0;
            MissingCommits = LocalCommits.Except(RemoteCommits).ToArray();
        }
    }
}

// <copyright file="TestOptimizationClient.GetImpactedTestsDetectionFilesAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
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
    private const string ImpactedTestsDetectionUrlPath = "api/v2/ci/tests/diffs";
    private const string ImpactedTestsDetectionType = "ci_app_tests_diffs_request";
    private Uri? _impactedTestsDetectionTestsUrl;

    public async Task<ImpactedTestsDetectionResponse> GetImpactedTestsDetectionFilesAsync()
    {
        Log.Debug("TestOptimizationClient: Getting impacted tests detection modified files...");
        if (!EnsureRepositoryUrl() || !EnsureCommitSha())
        {
            return default;
        }

        _impactedTestsDetectionTestsUrl ??= GetUriFromPath(ImpactedTestsDetectionUrlPath);
        var query = new DataEnvelope<Data<ImpactedTestsDetectionQuery>>(
            new Data<ImpactedTestsDetectionQuery>(
                _commitSha,
                ImpactedTestsDetectionType,
                new ImpactedTestsDetectionQuery(_serviceName, _environment, _repositoryUrl, _branchName, _commitSha)),
            null);

        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        Log.Debug("TestOptimizationClient: ITD.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<ImpactedTestsDetectionCallbacks>(_impactedTestsDetectionTestsUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequestErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Impacted tests file diffs request failed.");
            throw;
        }

        Log.Debug("TestOptimizationClient: ITD.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return default;
        }

        var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<ImpactedTestsDetectionResponse>?>>(queryResponse);
        var finalResponse = deserializedResult.Data?.Attributes ?? default;

        // Count the number of tests for telemetry
        var filesCount = 0;
        if (finalResponse.Files is { Length: > 0 } files)
        {
            filesCount = files.Length;
        }

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityImpactedTestsDetectionResponseFiles(filesCount);
        return finalResponse;
    }

    private readonly struct ImpactedTestsDetectionCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityImpactedTestsDetectionResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, responseLength);
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequestErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequestErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityImpactedTestsDetectionRequestMs(totalMs);
        }
    }

    private readonly struct ImpactedTestsDetectionQuery
    {
        [JsonProperty("service")]
        public readonly string Service;

        [JsonProperty("env")]
        public readonly string Environment;

        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("branch")]
        public readonly string Branch;

        [JsonProperty("sha")]
        public readonly string Sha;

        public ImpactedTestsDetectionQuery(string service, string environment, string repositoryUrl, string branch, string sha)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Branch = branch;
            Sha = sha;
        }
    }

    internal readonly struct ImpactedTestsDetectionResponse
    {
        [JsonProperty("base_sha")]
        public readonly string? BaseSha;

        [JsonProperty("files")]
        public readonly string[]? Files;
    }
}

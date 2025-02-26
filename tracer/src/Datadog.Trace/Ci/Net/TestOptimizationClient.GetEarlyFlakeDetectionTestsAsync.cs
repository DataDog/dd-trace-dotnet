// <copyright file="TestOptimizationClient.GetEarlyFlakeDetectionTestsAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
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
    private const string EfdTestsUrlPath = "api/v2/ci/libraries/tests";
    private const string EfdTestsType = "ci_app_libraries_tests_request";
    private Uri? _efdTestsUrl;

    public async Task<EarlyFlakeDetectionResponse> GetEarlyFlakeDetectionTestsAsync()
    {
        Log.Debug("TestOptimizationClient: Getting early flake detection tests...");
        if (!EnsureRepositoryUrl() || !EnsureCommitSha())
        {
            return default;
        }

        _efdTestsUrl ??= GetUriFromPath(EfdTestsUrlPath);
        var query = new DataEnvelope<Data<EarlyFlakeDetectionQuery>>(
            new Data<EarlyFlakeDetectionQuery>(
                _commitSha,
                EfdTestsType,
                new EarlyFlakeDetectionQuery(_serviceName, _environment, _repositoryUrl, GetTestConfigurations())),
            null);

        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        Log.Debug("TestOptimizationClient: Efd.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<EfdCallbacks>(_efdTestsUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequestErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Early flake detection tests request failed.");
            throw;
        }

        Log.Debug("TestOptimizationClient: Efd.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return default;
        }

        var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<EarlyFlakeDetectionResponse>?>>(queryResponse);
        var finalResponse = deserializedResult.Data?.Attributes ?? default;

        // Count the number of tests for telemetry
        var testsCount = 0;
        if (finalResponse.Tests is { Count: > 0 } modulesDictionary)
        {
            foreach (var suitesDictionary in modulesDictionary.Values)
            {
                if (suitesDictionary?.Count > 0)
                {
                    foreach (var testsArray in suitesDictionary.Values)
                    {
                        testsCount += testsArray?.Length ?? 0;
                    }
                }
            }
        }

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityEarlyFlakeDetectionResponseTests(testsCount);
        return finalResponse;
    }

    private readonly struct EfdCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEarlyFlakeDetectionResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, responseLength);
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequestErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequestErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEarlyFlakeDetectionRequestMs(totalMs);
        }
    }

    private readonly struct EarlyFlakeDetectionQuery
    {
        [JsonProperty("service")]
        public readonly string Service;

        [JsonProperty("env")]
        public readonly string Environment;

        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("configurations")]
        public readonly TestsConfigurations Configurations;

        public EarlyFlakeDetectionQuery(string service, string environment, string repositoryUrl, TestsConfigurations configurations)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Configurations = configurations;
        }
    }

    public readonly struct EarlyFlakeDetectionResponse
    {
        [JsonProperty("tests")]
        public readonly EfdResponseModules? Tests;

        public class EfdResponseSuites : Dictionary<string, string[]?>
        {
        }

        public class EfdResponseModules : Dictionary<string, EfdResponseSuites?>
        {
        }
    }
}

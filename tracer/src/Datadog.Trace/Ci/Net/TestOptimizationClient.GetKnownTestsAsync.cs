// <copyright file="TestOptimizationClient.GetKnownTestsAsync.cs" company="Datadog">
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
    private const string KnownTestsUrlPath = "api/v2/ci/libraries/tests";
    private const string KnownTestsType = "ci_app_libraries_tests_request";
    private Uri? _knownTestsUrl;

    public async Task<KnownTestsResponse> GetKnownTestsAsync()
    {
        Log.Debug("TestOptimizationClient: Getting known tests...");
        if (!EnsureRepositoryUrl() || !EnsureCommitSha())
        {
            return default;
        }

        _knownTestsUrl ??= GetUriFromPath(KnownTestsUrlPath);
        var query = new DataEnvelope<Data<KnownTestsQuery>>(
            new Data<KnownTestsQuery>(
                _commitSha,
                KnownTestsType,
                new KnownTestsQuery(_serviceName, _environment, _repositoryUrl, GetTestConfigurations())),
            null);

        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        Log.Debug("TestOptimizationClient: KnownTests.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<KnownTestsCallbacks>(_knownTestsUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityKnownTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Known tests request failed.");
            throw;
        }

        Log.Debug("TestOptimizationClient: KnownTests.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return default;
        }

        var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<KnownTestsResponse>?>>(queryResponse);
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

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityKnownTestsResponseTests(testsCount);
        return finalResponse;
    }

    private readonly struct KnownTestsCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityKnownTestsRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityKnownTestsResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, responseLength);
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityKnownTestsRequestErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityKnownTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityKnownTestsRequestMs(totalMs);
        }
    }

    private readonly struct KnownTestsQuery
    {
        [JsonProperty("service")]
        public readonly string Service;

        [JsonProperty("env")]
        public readonly string Environment;

        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("configurations")]
        public readonly TestsConfigurations Configurations;

        public KnownTestsQuery(string service, string environment, string repositoryUrl, TestsConfigurations configurations)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Configurations = configurations;
        }
    }

    public readonly struct KnownTestsResponse
    {
        [JsonProperty("tests")]
        public readonly KnownTestsModules? Tests;

        public sealed class KnownTestsSuites : Dictionary<string, string[]?>
        {
        }

        public sealed class KnownTestsModules : Dictionary<string, KnownTestsSuites?>
        {
        }
    }
}

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
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Datadog.Trace.Ci.Net;

internal sealed partial class TestOptimizationClient
{
    private const string KnownTestsUrlPath = "api/v2/ci/libraries/tests";
    private const string KnownTestsType = "ci_app_libraries_tests_request";
    private const int MaxKnownTestsPages = 10_000;
    private Uri? _knownTestsUrl;

    public async Task<KnownTestsResponse> GetKnownTestsAsync()
    {
        Log.Debug("TestOptimizationClient: Getting known tests...");
        if (!EnsureRepositoryUrl() || !EnsureCommitSha())
        {
            return default;
        }

        _knownTestsUrl ??= GetUriFromPath(KnownTestsUrlPath);

        var configurations = GetTestConfigurations();
        KnownTestsResponse.KnownTestsModules? aggregateTests = null;
        string? pageState = null;
        var pageNumber = 0;

        do
        {
            pageNumber++;
            var query = new DataEnvelope<Data<KnownTestsQuery>>(
                new Data<KnownTestsQuery>(
                    _commitSha,
                    KnownTestsType,
                    new KnownTestsQuery(_serviceName, _environment, _repositoryUrl, configurations, new PageInfoRequest(pageState))),
                null);

            var jsonQuery = JsonHelper.SerializeObject(query, SerializerSettings);
            Log.Debug("TestOptimizationClient: KnownTests.JSON RQ (page {PageNumber}) = {Json}", pageNumber, jsonQuery);

            string? queryResponse;
            try
            {
                queryResponse = await SendJsonRequestAsync<KnownTestsCallbacks>(_knownTestsUrl, jsonQuery).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityKnownTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
                Log.Error<int>(ex, "TestOptimizationClient: Known tests request failed on page {PageNumber}.", pageNumber);
                throw;
            }

            Log.Debug("TestOptimizationClient: KnownTests.JSON RS (page {PageNumber}) = {Json}", pageNumber, queryResponse);
            if (string.IsNullOrEmpty(queryResponse))
            {
                break;
            }

            var deserializedResult = JsonHelper.DeserializeObject<DataEnvelope<Data<KnownTestsPageResponse>?>>(queryResponse);
            var pageResponse = deserializedResult.Data?.Attributes;

            if (pageResponse is null)
            {
                break;
            }

            // Merge page tests into aggregate
            MergeKnownTests(ref aggregateTests, pageResponse.Value.Tests);

            // Check pagination
            var pageInfo = pageResponse.Value.PageInfo;
            if (pageInfo is not { HasNext: true })
            {
                // No page_info or has_next is false — we're done
                break;
            }

            if (string.IsNullOrEmpty(pageInfo.Value.Cursor))
            {
                Log.Warning<int>("TestOptimizationClient: Known tests response has has_next=true but no cursor on page {PageNumber}. Aborting pagination.", pageNumber);
                return default;
            }

            pageState = pageInfo.Value.Cursor;
        }
        while (pageNumber < MaxKnownTestsPages);

        if (pageNumber >= MaxKnownTestsPages)
        {
            Log.Warning<int>("TestOptimizationClient: Known tests pagination exceeded maximum of {MaxPages} pages. Aborting.", MaxKnownTestsPages);
            return default;
        }

        var finalResponse = new KnownTestsResponse(aggregateTests);

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

    private static void MergeKnownTests(ref KnownTestsResponse.KnownTestsModules? aggregate, KnownTestsResponse.KnownTestsModules? page)
    {
        if (page is null or { Count: 0 })
        {
            return;
        }

        aggregate ??= new KnownTestsResponse.KnownTestsModules();

        foreach (var moduleEntry in page)
        {
            if (moduleEntry.Value is null)
            {
                continue;
            }

            if (!aggregate.TryGetValue(moduleEntry.Key, out var existingSuites) || existingSuites is null)
            {
                existingSuites = new KnownTestsResponse.KnownTestsSuites();
                aggregate[moduleEntry.Key] = existingSuites;
            }

            foreach (var suiteEntry in moduleEntry.Value)
            {
                if (suiteEntry.Value is null or { Length: 0 })
                {
                    continue;
                }

                if (!existingSuites.TryGetValue(suiteEntry.Key, out var existingTests) || existingTests is null)
                {
                    existingSuites[suiteEntry.Key] = suiteEntry.Value;
                }
                else
                {
                    var merged = new string[existingTests.Length + suiteEntry.Value.Length];
                    existingTests.CopyTo(merged, 0);
                    suiteEntry.Value.CopyTo(merged, existingTests.Length);
                    existingSuites[suiteEntry.Key] = merged;
                }
            }
        }
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

        [JsonProperty("page_info")]
        public readonly PageInfoRequest PageInfo;

        public KnownTestsQuery(string service, string environment, string repositoryUrl, TestsConfigurations configurations, PageInfoRequest pageInfo)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Configurations = configurations;
            PageInfo = pageInfo;
        }
    }

    private readonly struct PageInfoRequest
    {
        [JsonProperty("page_state")]
        public readonly string? PageState;

        public PageInfoRequest(string? pageState)
        {
            PageState = pageState;
        }
    }

    private readonly struct PageInfoResponse
    {
        [JsonProperty("cursor")]
        public readonly string? Cursor;

        [JsonProperty("size")]
        public readonly int Size;

        [JsonProperty("has_next")]
        public readonly bool HasNext;
    }

    /// <summary>
    /// Internal response type for deserializing individual pages, which includes page_info.
    /// </summary>
    private readonly struct KnownTestsPageResponse
    {
        [JsonProperty("tests")]
        public readonly KnownTestsResponse.KnownTestsModules? Tests;

        [JsonProperty("page_info")]
        public readonly PageInfoResponse? PageInfo;
    }

    public readonly struct KnownTestsResponse
    {
        [JsonProperty("tests")]
        public readonly KnownTestsModules? Tests;

        public KnownTestsResponse(KnownTestsModules? tests)
        {
            Tests = tests;
        }

        public sealed class KnownTestsSuites : Dictionary<string, string[]?>
        {
        }

        public sealed class KnownTestsModules : Dictionary<string, KnownTestsSuites?>
        {
        }
    }
}

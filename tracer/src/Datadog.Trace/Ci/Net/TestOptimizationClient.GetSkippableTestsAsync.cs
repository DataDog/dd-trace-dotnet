// <copyright file="TestOptimizationClient.GetSkippableTestsAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

// ReSharper disable ConvertToPrimaryConstructor
// ReSharper disable NotAccessedField.Local
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value

namespace Datadog.Trace.Ci.Net;

internal sealed partial class TestOptimizationClient
{
    private const string SkippableUrlPath = "api/v2/ci/tests/skippable";
    private const string SkippableType = "test_params";
    private Uri? _skippableTestsUrl;

    public async Task<SkippableTestsResponse> GetSkippableTestsAsync()
    {
        Log.Debug("TestOptimizationClient: Getting skippable tests...");
        if (!EnsureRepositoryUrl() || !EnsureCommitSha())
        {
            return default;
        }

        _skippableTestsUrl ??= GetUriFromPath(SkippableUrlPath);
        var query = new DataEnvelope<Data<SkippableTestsQuery>>(
            new Data<SkippableTestsQuery>(
                null,
                SkippableType,
                new SkippableTestsQuery(_serviceName, _environment, _repositoryUrl, _commitSha, GetTestConfigurations(), "test")),
            null);

        var jsonQuery = JsonHelper.SerializeObject(query, SerializerSettings);
        Log.Debug("TestOptimizationClient: Skippable.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<SkippableCallbacks>(_skippableTestsUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Get skippable tests request failed.");
            throw;
        }

        Log.Debug<int>("TestOptimizationClient: Skippable.JSON RS length = {Length}", queryResponse?.Length ?? 0);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return default;
        }

        var deserializedResult = JsonHelper.DeserializeObject<DataArrayEnvelope<Data<SkippableTest>>>(queryResponse!);
        var coverageBackfillData = CoverageBackfillData.FromBackendCoverage(deserializedResult.Meta?.Coverage);
        if (coverageBackfillData.IsPresent)
        {
            Log.Debug<int, int, bool>(
                "TestOptimizationClient: Skippable coverage map received. Files={Files}, Bytes={Bytes}, Valid={Valid}",
                coverageBackfillData.ExecutedLinesByRelativePath.Count,
                coverageBackfillData.TotalBitmapBytes,
                coverageBackfillData.IsValid);
        }

        if (deserializedResult.Data is null || deserializedResult.Data.Length == 0)
        {
            return new SkippableTestsResponse(deserializedResult.Meta?.CorrelationId, [], coverageBackfillData, isCoverageBackfillSafe: coverageBackfillData.IsValid);
        }

        var testAttributes = new List<SkippableTest>(deserializedResult.Data.Length);
        var customConfigurations = _customConfigurations;
        var filteredOutTests = false;
        for (var i = 0; i < deserializedResult.Data.Length; i++)
        {
            var includeItem = true;
            var item = deserializedResult.Data[i].Attributes;
            if (item.Configurations?.Custom is { } itemCustomConfiguration)
            {
                if (customConfigurations is null)
                {
                    filteredOutTests = true;
                    continue;
                }

                foreach (var rsCustomConfigurationItem in itemCustomConfiguration)
                {
                    if (!customConfigurations.TryGetValue(rsCustomConfigurationItem.Key, out var customConfigValue) ||
                        rsCustomConfigurationItem.Value != customConfigValue)
                    {
                        includeItem = false;
                        break;
                    }
                }
            }

            if (includeItem)
            {
                testAttributes.Add(item);
            }
            else
            {
                filteredOutTests = true;
            }
        }

        if (Log.IsEnabled(LogEventLevel.Debug) && deserializedResult.Data.Length != testAttributes.Count)
        {
            Log.Debug<int, int>("TestOptimizationClient: Skippable tests filtered by local configuration. Original={Original}, Filtered={Filtered}", deserializedResult.Data.Length, testAttributes.Count);
        }

        TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsResponseTests(testAttributes.Count);
        var isCoverageBackfillSafe = coverageBackfillData.IsValid && (!filteredOutTests || !coverageBackfillData.IsPresent);
        return new SkippableTestsResponse(deserializedResult.Meta?.CorrelationId, testAttributes, coverageBackfillData, isCoverageBackfillSafe);
    }

    private readonly struct SkippableCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityITRSkippableTestsResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, responseLength);
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityITRSkippableTestsRequestMs(totalMs);
        }
    }

    private readonly struct SkippableTestsQuery
    {
        [JsonProperty("service")]
        public readonly string Service;

        [JsonProperty("env")]
        public readonly string Environment;

        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("sha")]
        public readonly string Sha;

        [JsonProperty("configurations")]
        public readonly TestsConfigurations? Configurations;

        /// <summary>
        /// Test granularity used by the local tracer when applying backend skippable candidates.
        /// </summary>
        [JsonProperty("test_level")]
        public readonly string TestLevel;

        /// <summary>
        /// Initializes a new instance of the <see cref="SkippableTestsQuery"/> struct.
        /// </summary>
        /// <param name="service">Service name used to query skippable tests.</param>
        /// <param name="environment">Environment name used to query skippable tests.</param>
        /// <param name="repositoryUrl">Repository URL used to scope skippable tests.</param>
        /// <param name="sha">Commit SHA used to scope skippable tests.</param>
        /// <param name="configurations">Runtime and custom test configurations.</param>
        /// <param name="testLevel">Test granularity applied by the local tracer.</param>
        public SkippableTestsQuery(string service, string environment, string repositoryUrl, string sha, TestsConfigurations? configurations, string testLevel)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Sha = sha;
            Configurations = configurations;
            TestLevel = testLevel;
        }
    }

    public readonly struct SkippableTestsResponse
    {
        [JsonProperty("correlationId")]
        public readonly string? CorrelationId;

        [JsonProperty("tests")]
        public readonly ICollection<SkippableTest> Tests;

        /// <summary>
        /// Decoded backend coverage used to correct coverage when ITR skips tests.
        /// </summary>
        [JsonProperty("coverage")]
        public readonly CoverageBackfillData Coverage;

        /// <summary>
        /// Indicates whether the coverage aggregate still matches the filtered local skippable-test response.
        /// </summary>
        [JsonProperty("coverage_backfill_safe")]
        public readonly bool IsCoverageBackfillSafe;

        public SkippableTestsResponse()
        {
            CorrelationId = null;
            Tests = [];
            Coverage = CoverageBackfillData.Missing;
            IsCoverageBackfillSafe = false;
        }

        public SkippableTestsResponse(string? correlationId, ICollection<SkippableTest> tests, CoverageBackfillData? coverageBackfillData = null, bool isCoverageBackfillSafe = false)
        {
            CorrelationId = correlationId;
            Tests = tests;
            Coverage = coverageBackfillData ?? CoverageBackfillData.Missing;
            IsCoverageBackfillSafe = isCoverageBackfillSafe;
        }
    }
}

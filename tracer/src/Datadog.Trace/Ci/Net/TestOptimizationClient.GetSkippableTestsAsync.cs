// <copyright file="TestOptimizationClient.GetSkippableTestsAsync.cs" company="Datadog">
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
                new SkippableTestsQuery(_serviceName, _environment, _repositoryUrl, _commitSha, GetTestConfigurations())),
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

        Log.Debug("TestOptimizationClient: Skippable.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return default;
        }

        var deserializedResult = JsonHelper.DeserializeObject<DataArrayEnvelope<Data<SkippableTest>>>(queryResponse);
        if (deserializedResult.Data is null || deserializedResult.Data.Length == 0)
        {
            return new SkippableTestsResponse(deserializedResult.Meta?.CorrelationId, []);
        }

        var testAttributes = new List<SkippableTest>(deserializedResult.Data.Length);
        var customConfigurations = _customConfigurations;
        for (var i = 0; i < deserializedResult.Data.Length; i++)
        {
            var includeItem = true;
            var item = deserializedResult.Data[i].Attributes;
            if (item.Configurations?.Custom is { } itemCustomConfiguration)
            {
                if (customConfigurations is null)
                {
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
        }

        if (Log.IsEnabled(LogEventLevel.Debug) && deserializedResult.Data.Length != testAttributes.Count)
        {
            Log.Debug("TestOptimizationClient: Skippable.JSON Filtered = {Json}", JsonHelper.SerializeObject(testAttributes));
        }

        TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsResponseTests(testAttributes.Count);
        return new SkippableTestsResponse(deserializedResult.Meta?.CorrelationId, testAttributes);
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

        public SkippableTestsQuery(string service, string environment, string repositoryUrl, string sha, TestsConfigurations? configurations)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Sha = sha;
            Configurations = configurations;
        }
    }

    public readonly struct SkippableTestsResponse
    {
        [JsonProperty("correlationId")]
        public readonly string? CorrelationId;

        [JsonProperty("tests")]
        public readonly ICollection<SkippableTest> Tests;

        public SkippableTestsResponse()
        {
            CorrelationId = null;
            Tests = [];
        }

        public SkippableTestsResponse(string? correlationId, ICollection<SkippableTest> tests)
        {
            CorrelationId = correlationId;
            Tests = tests;
        }
    }
}

// <copyright file="TestOptimizationClient.GetTestManagementTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Net;

internal sealed partial class TestOptimizationClient
{
    private const string TestManagementUrlPath = "api/v2/test/libraries/test-management/tests";
    private const string TestManagementType = "ci_app_libraries_tests_request";
    private Uri? _testManagementUrl;

    public async Task<TestManagementResponse> GetTestManagementTests()
    {
        Log.Debug("TestOptimizationClient: Getting test management tests...");
        if (!EnsureRepositoryUrl() || !EnsureCommitSha())
        {
            return new TestManagementResponse();
        }

        var commitSha = _testOptimization.CIValues.HeadCommit ?? _commitSha;
        var commitMessage = _testOptimization.CIValues.HeadMessage ?? _testOptimization.CIValues.Message ?? string.Empty;
        var branch = _testOptimization.CIValues.Branch;
        _testManagementUrl ??= GetUriFromPath(TestManagementUrlPath);
        var query = new DataEnvelope<Data<TestManagementQuery>>(
            new Data<TestManagementQuery>(
                commitSha,
                TestManagementType,
                new TestManagementQuery(_repositoryUrl, commitSha, null, commitMessage, branch)),
            null);

        var jsonQuery = JsonHelper.SerializeObject(query, SerializerSettings);
        Log.Debug("TestOptimizationClient: TestManagement.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<TestManagementCallbacks>(_testManagementUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityTestManagementTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Test management tests request failed.");
            throw;
        }

        Log.Debug("TestOptimizationClient: TestManagement.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            Log.Debug("TestOptimizationClient: TestManagement response has 0 tests.");
            return new TestManagementResponse();
        }

        var deserializedResult = JsonHelper.DeserializeObject<DataEnvelope<Data<TestManagementResponse>?>>(queryResponse);
        var finalResponse = deserializedResult.Data?.Attributes ?? new TestManagementResponse();

        // Count the number of tests for telemetry
        var testsCount = 0;
        if (finalResponse.Modules is { Count: > 0 } modulesDictionary)
        {
            foreach (var suites in modulesDictionary.Values)
            {
                if (suites?.Suites is { Count: > 0 })
                {
                    foreach (var testsArray in suites.Suites.Values)
                    {
                        testsCount += testsArray?.Tests?.Count ?? 0;
                    }
                }
            }
        }

        Log.Debug<int>("TestOptimizationClient: TestManagement response has {Count} test(s).", testsCount);
        TelemetryFactory.Metrics.RecordDistributionCIVisibilityTestManagementTestsResponseTests(testsCount);
        return finalResponse;
    }

    private readonly struct TestManagementCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityTestManagementTestsRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityTestManagementTestsResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, responseLength);
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityTestManagementTestsRequestErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityTestManagementTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityTestManagementTestsRequestMs(totalMs);
        }
    }

    private readonly struct TestManagementQuery
    {
        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("sha")]
        public readonly string CommitSha;

        [JsonProperty("module")]
        public readonly string? Module;

        [JsonProperty("commit_message")]
        public readonly string CommitMessage;

        [JsonProperty("branch")]
        public readonly string? Branch;

        public TestManagementQuery(string repositoryUrl, string commitSha, string? module, string commitMessage, string? branch)
        {
            RepositoryUrl = repositoryUrl;
            CommitSha = commitSha;
            Module = module;
            CommitMessage = commitMessage;
            Branch = branch;
        }
    }

#pragma warning disable SA1201

    public sealed class TestManagementResponse
    {
        public TestManagementResponse()
        {
            Modules = null;
        }

        public TestManagementResponse(Dictionary<string, TestManagementResponseSuites> modules)
        {
            Modules = modules;
        }

        [JsonProperty("modules")]
        public Dictionary<string, TestManagementResponseSuites>? Modules { get; private set; }
    }

    public sealed class TestManagementResponseSuites
    {
        public TestManagementResponseSuites()
        {
            Suites = null;
        }

        public TestManagementResponseSuites(Dictionary<string, TestManagementResponseTests> suites)
        {
            Suites = suites;
        }

        [JsonProperty("suites")]
        public Dictionary<string, TestManagementResponseTests>? Suites { get; private set; }
    }

    public sealed class TestManagementResponseTests
    {
        public TestManagementResponseTests()
        {
            Tests = null;
        }

        public TestManagementResponseTests(Dictionary<string, TestManagementResponseTestProperties> tests)
        {
            Tests = tests;
        }

        [JsonProperty("tests")]
        public Dictionary<string, TestManagementResponseTestProperties>? Tests { get; private set; }
    }

    public sealed class TestManagementResponseTestProperties
    {
        public TestManagementResponseTestProperties()
        {
            Properties = new TestManagementResponseTestPropertiesAttributes();
        }

        public TestManagementResponseTestProperties(TestManagementResponseTestPropertiesAttributes properties)
        {
            Properties = properties;
        }

        [JsonProperty("properties")]
        public TestManagementResponseTestPropertiesAttributes Properties { get; private set; }
    }

    public sealed class TestManagementResponseTestPropertiesAttributes
    {
        public TestManagementResponseTestPropertiesAttributes()
        {
        }

        public TestManagementResponseTestPropertiesAttributes(bool quarantined, bool disabled, bool attemptToFix)
        {
            Quarantined = quarantined;
            Disabled = disabled;
            AttemptToFix = attemptToFix;
        }

        public static TestManagementResponseTestPropertiesAttributes Default { get; } = new(false, false, false);

        [JsonProperty("quarantined")]
        public bool Quarantined { get; private set; }

        [JsonProperty("disabled")]
        public bool Disabled { get; private set; }

        [JsonProperty("attempt_to_fix")]
        public bool AttemptToFix { get; private set; }
    }

#pragma warning restore SA1201
}

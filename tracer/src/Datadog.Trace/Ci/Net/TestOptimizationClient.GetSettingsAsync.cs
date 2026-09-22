// <copyright file="TestOptimizationClient.GetSettingsAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Configuration;
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
    private const string SettingsUrlPath = "api/v2/libraries/tests/services/setting";
    private const string SettingsType = "ci_app_test_service_libraries_settings";
    private Uri? _settingsUrl;

    public static SettingsResponse CreateSettingsResponseFromTestOptimizationSettings(TestOptimizationSettings settings, ITestOptimizationTracerManagement? tracerManagement)
    {
        if (!settings.IntelligentTestRunnerEnabled ||
            (!settings.Agentless && tracerManagement?.EventPlatformProxySupport == EventPlatformProxySupport.None))
        {
            // No additional features should be enabled
            return new SettingsResponse(
                codeCoverage: false,
                testsSkipping: false,
                requireGit: false,
                impactedTestsEnabled: false,
                flakyTestRetries: false,
                earlyFlakeDetection: new EarlyFlakeDetectionSettingsResponse(
                    enabled: false,
                    slowTestRetries: new SlowTestRetriesSettingsResponse(),
                    faultySessionThreshold: 0),
                knownTestsEnabled: false,
                testManagement: new TestManagementSettingsResponse(
                    enabled: false,
                    attemptToFixRetries: 0),
                dynamicInstrumentationEnabled: false);
        }

        return new SettingsResponse(
            codeCoverage: settings.CodeCoverageEnabled,
            testsSkipping: settings.TestsSkippingEnabled,
            requireGit: false,
            impactedTestsEnabled: settings.ImpactedTestsDetectionEnabled,
            flakyTestRetries: settings.FlakyRetryEnabled,
            earlyFlakeDetection: new EarlyFlakeDetectionSettingsResponse(
                enabled: settings.EarlyFlakeDetectionEnabled,
                slowTestRetries: new SlowTestRetriesSettingsResponse(),
                faultySessionThreshold: 0),
            knownTestsEnabled: settings.KnownTestsEnabled,
            testManagement: new TestManagementSettingsResponse(
                enabled: settings.TestManagementEnabled,
                attemptToFixRetries: settings.TestManagementAttemptToFixRetryCount),
            dynamicInstrumentationEnabled: settings.DynamicInstrumentationEnabled);
    }

    public async Task<SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
    {
        Log.Debug("TestOptimizationClient: Getting settings...");
        if (!EnsureRepositoryUrl() || !EnsureBranchName() || !EnsureCommitSha())
        {
            return default;
        }

        _settingsUrl ??= GetUriFromPath(SettingsUrlPath);
        var query = new DataEnvelope<Data<SettingsQuery>>(
            new Data<SettingsQuery>(
                _commitSha,
                SettingsType,
                new SettingsQuery(_serviceName, _environment, _repositoryUrl, _branchName, _commitSha, GetTestConfigurations(skipFrameworkInfo))),
            null);

        var jsonQuery = JsonHelper.SerializeObject(query, SerializerSettings);
        Log.Debug("TestOptimizationClient: Settings.JSON RQ = {Json}", jsonQuery);

        string? queryResponse;
        try
        {
            queryResponse = await SendJsonRequestAsync<SettingsCallbacks>(_settingsUrl, jsonQuery).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsErrors(MetricTags.CIVisibilityErrorType.Network);
            Log.Error(ex, "TestOptimizationClient: Get settings request failed.");
            throw;
        }

        Log.Debug("TestOptimizationClient: Settings.JSON RS = {Json}", queryResponse);
        if (string.IsNullOrEmpty(queryResponse))
        {
            return default;
        }

        var deserializedResult = JsonHelper.DeserializeObject<DataEnvelope<Data<SettingsResponse>?>>(queryResponse);
        var settingsResponse = deserializedResult.Data?.Attributes ?? default;
        TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsResponse(
            settingsResponse.CodeCoverage == true ? MetricTags.CIVisibilitySettingsResponse_CoverageFeature.Enabled : MetricTags.CIVisibilitySettingsResponse_CoverageFeature.Disabled,
            settingsResponse.TestsSkipping == true ? MetricTags.CIVisibilitySettingsResponse_ItrSkippingFeature.Enabled : MetricTags.CIVisibilitySettingsResponse_ItrSkippingFeature.Disabled,
            settingsResponse.KnownTestsEnabled == true ? MetricTags.CIVisibilitySettingsResponse_KnownTestsFeature.Enabled : MetricTags.CIVisibilitySettingsResponse_KnownTestsFeature.Disabled,
            settingsResponse.EarlyFlakeDetection.Enabled == true ? MetricTags.CIVisibilitySettingsResponse_EarlyFlakeDetectionFeature.Enabled : MetricTags.CIVisibilitySettingsResponse_EarlyFlakeDetectionFeature.Disabled,
            settingsResponse.FlakyTestRetries == true ? MetricTags.CIVisibilitySettingsResponse_FlakyTestRetriesFeature.Enabled : MetricTags.CIVisibilitySettingsResponse_FlakyTestRetriesFeature.Disabled,
            settingsResponse.TestManagement.Enabled == true ? MetricTags.CIVisibilitySettingsResponse_TestManagementFeature.Enabled : MetricTags.CIVisibilitySettingsResponse_TestManagementFeature.Disabled);

        return settingsResponse;
    }

    private readonly struct SettingsCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettings(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsSettingsMs(totalMs);
        }
    }

    private readonly struct SettingsQuery
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

        [JsonProperty("configurations")]
        public readonly TestsConfigurations Configurations;

        public SettingsQuery(string service, string environment, string repositoryUrl, string branch, string sha, TestsConfigurations configurations)
        {
            Service = service;
            Environment = environment;
            RepositoryUrl = repositoryUrl;
            Branch = branch;
            Sha = sha;
            Configurations = configurations;
        }
    }

    public readonly struct SettingsResponse
    {
        [JsonProperty("code_coverage")]
        public readonly bool? CodeCoverage;

        [JsonProperty("tests_skipping")]
        public readonly bool? TestsSkipping;

        [JsonProperty("require_git")]
        public readonly bool? RequireGit;

        [JsonProperty("impacted_tests_enabled")]
        public readonly bool? ImpactedTestsEnabled;

        [JsonProperty("flaky_test_retries_enabled")]
        public readonly bool? FlakyTestRetries;

        [JsonProperty("early_flake_detection")]
        public readonly EarlyFlakeDetectionSettingsResponse EarlyFlakeDetection;

        [JsonProperty("known_tests_enabled")]
        public readonly bool? KnownTestsEnabled;

        [JsonProperty("test_management")]
        public readonly TestManagementSettingsResponse TestManagement;

        [JsonProperty("default_branch")]
        public readonly string? DefaultBranch;

        [JsonProperty("di_enabled")]
        public readonly bool? DynamicInstrumentationEnabled;

        public SettingsResponse()
        {
        }

        public SettingsResponse(bool? codeCoverage, bool? testsSkipping, bool? requireGit, bool? impactedTestsEnabled, bool? flakyTestRetries, EarlyFlakeDetectionSettingsResponse earlyFlakeDetection, bool? knownTestsEnabled, TestManagementSettingsResponse testManagement, bool? dynamicInstrumentationEnabled)
        {
            CodeCoverage = codeCoverage;
            TestsSkipping = testsSkipping;
            RequireGit = requireGit;
            ImpactedTestsEnabled = impactedTestsEnabled;
            FlakyTestRetries = flakyTestRetries;
            EarlyFlakeDetection = earlyFlakeDetection;
            KnownTestsEnabled = knownTestsEnabled;
            TestManagement = testManagement;
            DynamicInstrumentationEnabled = dynamicInstrumentationEnabled;
        }
    }

    public readonly struct EarlyFlakeDetectionSettingsResponse
    {
        [JsonProperty("enabled")]
        public readonly bool? Enabled;

        [JsonProperty("slow_test_retries")]
        public readonly SlowTestRetriesSettingsResponse SlowTestRetries;

        [JsonProperty("faulty_session_threshold")]
        public readonly int? FaultySessionThreshold;

        public EarlyFlakeDetectionSettingsResponse()
        {
        }

        public EarlyFlakeDetectionSettingsResponse(bool? enabled, SlowTestRetriesSettingsResponse slowTestRetries, int? faultySessionThreshold)
        {
            Enabled = enabled;
            SlowTestRetries = slowTestRetries;
            FaultySessionThreshold = faultySessionThreshold;
        }
    }

    public readonly struct SlowTestRetriesSettingsResponse
    {
        [JsonProperty("5s")]
        public readonly int? FiveSeconds;

        [JsonProperty("10s")]
        public readonly int? TenSeconds;

        [JsonProperty("30s")]
        public readonly int? ThirtySeconds;

        [JsonProperty("5m")]
        public readonly int? FiveMinutes;

        public SlowTestRetriesSettingsResponse()
        {
        }

        public SlowTestRetriesSettingsResponse(int? fiveSeconds, int? tenSeconds, int? thirtySeconds, int? fiveMinutes)
        {
            FiveSeconds = fiveSeconds;
            TenSeconds = tenSeconds;
            ThirtySeconds = thirtySeconds;
            FiveMinutes = fiveMinutes;
        }
    }

    public readonly struct TestManagementSettingsResponse
    {
        [JsonProperty("enabled")]
        public readonly bool? Enabled;

        [JsonProperty("attempt_to_fix_retries")]
        public readonly int? AttemptToFixRetries;

        public TestManagementSettingsResponse()
        {
        }

        public TestManagementSettingsResponse(bool? enabled, int? attemptToFixRetries)
        {
            Enabled = enabled;
            AttemptToFixRetries = attemptToFixRetries;
        }
    }
}

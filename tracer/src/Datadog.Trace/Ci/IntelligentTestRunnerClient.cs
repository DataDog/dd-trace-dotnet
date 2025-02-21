// <copyright file="IntelligentTestRunnerClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Ci.CiEnvironment;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Ci;

#pragma warning disable CS0649

/// <summary>
/// Intelligent Test Runner Client
/// </summary>
internal class IntelligentTestRunnerClient
{
    private const string ApiKeyHeader = "DD-API-KEY";
    private const string EvpSubdomainHeader = "X-Datadog-EVP-Subdomain";

    private const int MaxRetries = 5;
    private const int MaxPackFileSizeInMb = 3;

    private const string CommitType = "commit";
    private const string TestParamsType = "test_params";
    private const string SettingsType = "ci_app_test_service_libraries_settings";
    private const string EarlyFlakeDetectionRequestType = "ci_app_libraries_tests_request";
    private const string ImpactedTestsDetectionRequestType = "ci_app_tests_diffs_request";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IntelligentTestRunnerClient));
    private static readonly Regex ShaRegex = new("[0-9a-f]+", RegexOptions.Compiled);
    private static readonly JsonSerializerSettings SerializerSettings = new() { DefaultValueHandling = DefaultValueHandling.Ignore };

    private readonly string _id;
    private readonly CIVisibilitySettings _settings;
    private readonly string? _workingDirectory;
    private readonly string _environment;
    private readonly string _serviceName;
    private readonly Dictionary<string, string>? _customConfigurations;
    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly Uri _settingsUrl;
    private readonly Uri _searchCommitsUrl;
    private readonly Uri _packFileUrl;
    private readonly Uri _skippableTestsUrl;
    private readonly Uri _earlyFlakeDetectionTestsUrl;
    private readonly Uri _impactedTestsDetectionTestsUrl;
    private readonly EventPlatformProxySupport _eventPlatformProxySupport;
    private readonly string _repositoryUrl;
    private readonly string _branchName;
    private readonly string _commitSha;

    public IntelligentTestRunnerClient(string? workingDirectory, CIVisibilitySettings? settings = null)
    {
        _id = RandomIdGenerator.Shared.NextSpanId().ToString(CultureInfo.InvariantCulture);
        _settings = settings ?? CIVisibility.Settings;

        _workingDirectory = workingDirectory;
        _environment = TraceUtil.NormalizeTag(_settings.TracerSettings.Environment ?? "none") ?? "none";
        _serviceName = NormalizerTraceProcessor.NormalizeService(_settings.TracerSettings.ServiceName) ?? string.Empty;
        _customConfigurations = null;

        // Extract custom tests configurations from DD_TAGS
        _customConfigurations = GetCustomTestsConfigurations(_settings.TracerSettings.GlobalTags);

        _repositoryUrl = GetRepositoryUrl();
        _commitSha = GetCommitSha();
        _branchName = GetBranchName();

        _apiRequestFactory = CIVisibility.GetRequestFactory(_settings.TracerSettings, TimeSpan.FromSeconds(45));

        const string settingsUrlPath = "api/v2/libraries/tests/services/setting";
        const string searchCommitsUrlPath = "api/v2/git/repository/search_commits";
        const string packFileUrlPath = "api/v2/git/repository/packfile";
        const string skippableTestsUrlPath = "api/v2/ci/tests/skippable";
        const string efdTestsUrlPath = "api/v2/ci/libraries/tests";
        const string itdTestsUrlPath = "api/v2/ci/tests/diffs";

        if (_settings.Agentless)
        {
            _eventPlatformProxySupport = EventPlatformProxySupport.None;
            var agentlessUrl = _settings.AgentlessUrl;
            if (!string.IsNullOrWhiteSpace(agentlessUrl))
            {
                _settingsUrl = new UriBuilder(agentlessUrl) { Path = settingsUrlPath }.Uri;
                _searchCommitsUrl = new UriBuilder(agentlessUrl) { Path = searchCommitsUrlPath }.Uri;
                _packFileUrl = new UriBuilder(agentlessUrl) { Path = packFileUrlPath }.Uri;
                _skippableTestsUrl = new UriBuilder(agentlessUrl) { Path = skippableTestsUrlPath }.Uri;
                _earlyFlakeDetectionTestsUrl = new UriBuilder(agentlessUrl) { Path = efdTestsUrlPath }.Uri;
                _impactedTestsDetectionTestsUrl = new UriBuilder(agentlessUrl) { Path = itdTestsUrlPath }.Uri;
            }
            else
            {
                _settingsUrl = new UriBuilder(
                    scheme: "https",
                    host: "api." + _settings.Site,
                    port: 443,
                    pathValue: settingsUrlPath).Uri;

                _searchCommitsUrl = new UriBuilder(
                    scheme: "https",
                    host: "api." + _settings.Site,
                    port: 443,
                    pathValue: searchCommitsUrlPath).Uri;

                _packFileUrl = new UriBuilder(
                    scheme: "https",
                    host: "api." + _settings.Site,
                    port: 443,
                    pathValue: packFileUrlPath).Uri;

                _skippableTestsUrl = new UriBuilder(
                    scheme: "https",
                    host: "api." + _settings.Site,
                    port: 443,
                    pathValue: skippableTestsUrlPath).Uri;

                _earlyFlakeDetectionTestsUrl = new UriBuilder(
                    scheme: "https",
                    host: "api." + _settings.Site,
                    port: 443,
                    pathValue: efdTestsUrlPath).Uri;

                _impactedTestsDetectionTestsUrl = new UriBuilder(
                    scheme: "https",
                    host: "api." + _settings.Site,
                    port: 443,
                    pathValue: itdTestsUrlPath).Uri;
            }
        }
        else
        {
            // Use Agent EVP Proxy
            _eventPlatformProxySupport = CIVisibility.EventPlatformProxySupport;
            switch (_eventPlatformProxySupport)
            {
                case EventPlatformProxySupport.V2:
                    _settingsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{settingsUrlPath}");
                    _searchCommitsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{searchCommitsUrlPath}");
                    _packFileUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{packFileUrlPath}");
                    _skippableTestsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{skippableTestsUrlPath}");
                    _earlyFlakeDetectionTestsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{efdTestsUrlPath}");
                    _impactedTestsDetectionTestsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v2/{itdTestsUrlPath}");
                    break;
                case EventPlatformProxySupport.V4:
                    _settingsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{settingsUrlPath}");
                    _searchCommitsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{searchCommitsUrlPath}");
                    _packFileUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{packFileUrlPath}");
                    _skippableTestsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{skippableTestsUrlPath}");
                    _earlyFlakeDetectionTestsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{efdTestsUrlPath}");
                    _impactedTestsDetectionTestsUrl = _apiRequestFactory.GetEndpoint($"evp_proxy/v4/{itdTestsUrlPath}");
                    break;
                default:
                    throw new NotSupportedException("Event platform proxy not supported by the agent.");
            }
        }
    }

    internal static Dictionary<string, string>? GetCustomTestsConfigurations(IReadOnlyDictionary<string, string> globalTags)
    {
        if (globalTags is null)
        {
            return null;
        }

        Dictionary<string, string>? customConfiguration = null;
        foreach (var tag in globalTags)
        {
            const string testConfigKey = "test.configuration.";
            if (tag.Key.StartsWith(testConfigKey, StringComparison.OrdinalIgnoreCase))
            {
                var key = tag.Key.Substring(testConfigKey.Length);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                customConfiguration ??= new Dictionary<string, string>();
                customConfiguration[key] = tag.Value;
            }
        }

        return customConfiguration;
    }

    public async Task<long> UploadRepositoryChangesAsync()
    {
        Log.Debug("ITR: Uploading Repository Changes...");

        // Let's first try get the commit data from local and remote
        var initialCommitData = await GetCommitsAsync().ConfigureAwait(false);

        // Let's check if we could retrieve commit data
        if (!initialCommitData.IsOk)
        {
            return 0;
        }

        // If:
        //   - We have local commits
        //   - There are not missing commits (backend has the total number of local commits already)
        // Then we are good to go with it, we don't need to check if we need to unshallow or anything and just go with that.
        if (initialCommitData is { HasCommits: true, MissingCommits.Length: 0 })
        {
            Log.Debug("ITR: Initial commit data has everything already, we don't need to upload anything.");
            return 0;
        }

        // There's some missing commits on the backend, first we need to check if we need to unshallow before sending anything...

        try
        {
            // We need to check if the git clone is a shallow one before uploading anything.
            // In the case is a shallow clone we need to reconfigure it to upload the git tree
            // without blobs so no content will be downloaded.
            var gitRevParseShallowOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "rev-parse --is-shallow-repository", MetricTags.CIVisibilityCommands.CheckShallow);
            if (gitRevParseShallowOutput is null)
            {
                Log.Warning("ITR: 'git rev-parse --is-shallow-repository' command is null");
                return 0;
            }

            var isShallow = gitRevParseShallowOutput.Output.IndexOf("true", StringComparison.OrdinalIgnoreCase) > -1;
            if (!isShallow)
            {
                // Repo is not in a shallow state, we continue with the pack files upload with the initial commit data we retrieved earlier.
                Log.Debug("ITR: Repository is not in a shallow state, uploading changes...");
                return await SendObjectsPackFileAsync(initialCommitData.LocalCommits[0], initialCommitData.MissingCommits, initialCommitData.RemoteCommits).ConfigureAwait(false);
            }

            Log.Debug("ITR: Unshallowing the repository...");

            // The git repo is a shallow clone, we need to double check if there are more than just 1 commit in the logs.
            var gitShallowLogOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "log --format=oneline -n 2", MetricTags.CIVisibilityCommands.CheckShallow);
            if (gitShallowLogOutput is null)
            {
                Log.Warning("ITR: 'git log --format=oneline -n 2' command is null");
                return 0;
            }

            // After asking for 2 logs lines, if the git log command returns just one commit sha, we reconfigure the repo
            // to ask for git commits and trees of the last month (no blobs)
            var shallowLogArray = gitShallowLogOutput.Output.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
            if (shallowLogArray.Length == 1)
            {
                // Just one commit SHA. Fetching previous commits

                // ***
                // Let's try to unshallow the repo:
                // `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse HEAD)`
                // ***

                // git config --default origin --get clone.defaultRemoteName
                var originNameOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "config --default origin --get clone.defaultRemoteName", MetricTags.CIVisibilityCommands.GetRemote);
                var originName = originNameOutput?.Output?.Replace("\n", string.Empty).Trim() ?? "origin";

                // git rev-parse HEAD
                var headOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "rev-parse HEAD", MetricTags.CIVisibilityCommands.GetHead);
                var head = headOutput?.Output?.Replace("\n", string.Empty).Trim() ?? _branchName;

                // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse HEAD)
                Log.Information("ITR: The current repo is a shallow clone, refetching data for {OriginName}|{Head}", originName, head);
                var gitUnshallowOutput = GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow);

                if (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0)
                {
                    // ***
                    // The previous command has a drawback: if the local HEAD is a commit that has not been pushed to the remote, it will fail.
                    // If this is the case, we fallback to: `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse --abbrev-ref --symbolic-full-name @{upstream})`
                    // This command will attempt to use the tracked branch for the current branch in order to unshallow.
                    // ***

                    // originName = git config --default origin --get clone.defaultRemoteName
                    // git rev-parse --abbrev-ref --symbolic-full-name @{upstream}
                    headOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "rev-parse --abbrev-ref --symbolic-full-name \"@{upstream}\"", MetricTags.CIVisibilityCommands.GetHead);
                    head = headOutput?.Output?.Replace("\n", string.Empty).Trim() ?? _branchName;

                    // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse --abbrev-ref --symbolic-full-name @{upstream})
                    Log.Information("ITR: Previous unshallow command failed, refetching data with fallback 1 for {OriginName}|{Head}", originName, head);
                    gitUnshallowOutput = GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow);
                }

                if (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0)
                {
                    // ***
                    // It could be that the CI is working on a detached HEAD or maybe branch tracking hasn’t been set up.
                    // In that case, this command will also fail, and we will finally fallback to we just unshallow all the things:
                    // `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName)`
                    // ***

                    // originName = git config --default origin --get clone.defaultRemoteName
                    // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName)
                    Log.Information("ITR: Previous unshallow command failed, refetching data with fallback 2 for {OriginName}", originName);
                    GitCommandHelper.RunGitCommand(_workingDirectory, $"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName}", MetricTags.CIVisibilityCommands.Unshallow);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting and reconfiguring git repository for shallow clone.");
        }

        var commitsData = await GetCommitsAsync().ConfigureAwait(false);
        if (!commitsData.IsOk)
        {
            return 0;
        }

        return await SendObjectsPackFileAsync(commitsData.LocalCommits[0], commitsData.MissingCommits, commitsData.RemoteCommits).ConfigureAwait(false);
    }

    public async Task<SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
    {
        Log.Debug("ITR: Getting settings...");
        var framework = FrameworkDescription.Instance;
        if (string.IsNullOrEmpty(_repositoryUrl))
        {
            Log.Warning("ITR: 'git config --get remote.origin.url' command returned null or empty");
            return default;
        }

        if (string.IsNullOrEmpty(_branchName))
        {
            Log.Warning("ITR: 'git branch --show-current' command returned null or empty");
            return default;
        }

        if (string.IsNullOrEmpty(_commitSha))
        {
            Log.Warning("ITR: 'git rev-parse HEAD' command returned null or empty");
            return default;
        }

        var query = new DataEnvelope<Data<SettingsQuery>>(
            new Data<SettingsQuery>(
                _commitSha,
                SettingsType,
                new SettingsQuery(
                    _serviceName,
                    _environment,
                    _repositoryUrl,
                    _branchName,
                    _commitSha,
                    new TestsConfigurations(
                        framework.OSPlatform,
                        CIVisibility.GetOperatingSystemVersion(),
                        framework.OSArchitecture,
                        skipFrameworkInfo ? null : framework.Name,
                        skipFrameworkInfo ? null : framework.ProductVersion,
                        skipFrameworkInfo ? null : framework.ProcessArchitecture,
                        _customConfigurations))),
            default);
        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        var jsonQueryBytes = Encoding.UTF8.GetBytes(jsonQuery);
        Log.Debug("ITR: Settings.JSON RQ = {Json}", jsonQuery);

        return await WithRetries(InternalGetSettingsAsync, jsonQueryBytes, MaxRetries).ConfigureAwait(false);

        async Task<SettingsResponse> InternalGetSettingsAsync(byte[] state, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // We currently always send the request uncompressed
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettings(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
                var request = _apiRequestFactory.Create(_settingsUrl);
                SetRequestHeader(request);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("ITR: Getting settings from: {Url}", _settingsUrl.ToString());
                }

                string? responseContent;
                try
                {
                    using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
                    responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                }
                catch (Exception ex)
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsErrors(MetricTags.CIVisibilityErrorType.Network);
                    Log.Error(ex, "ITR: Get settings request failed.");
                    throw;
                }

                Log.Debug("ITR: Settings.JSON RS = {Json}", responseContent);
                if (string.IsNullOrEmpty(responseContent))
                {
                    return default;
                }

                var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<SettingsResponse>?>>(responseContent);
                var settingsResponse = deserializedResult.Data?.Attributes ?? default;
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsResponse(
                    settingsResponse switch
                    {
                        { CodeCoverage: true, TestsSkipping: true, EarlyFlakeDetection.Enabled: false, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipEnabled_AtrDisabled,
                        { CodeCoverage: true, TestsSkipping: false, EarlyFlakeDetection.Enabled: false, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipDisabled_AtrDisabled,
                        { CodeCoverage: false, TestsSkipping: true, EarlyFlakeDetection.Enabled: false, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipEnabled_AtrDisabled,
                        { CodeCoverage: false, TestsSkipping: false, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipDisabled_EFDEnabled_AtrDisabled,
                        { CodeCoverage: true, TestsSkipping: true, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipEnabled_EFDEnabled_AtrDisabled,
                        { CodeCoverage: true, TestsSkipping: false, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipDisabled_EFDEnabled_AtrDisabled,
                        { CodeCoverage: false, TestsSkipping: true, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: false } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipEnabled_EFDEnabled_AtrDisabled,
                        { CodeCoverage: true, TestsSkipping: true, EarlyFlakeDetection.Enabled: false, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipEnabled_AtrEnabled,
                        { CodeCoverage: true, TestsSkipping: false, EarlyFlakeDetection.Enabled: false, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipDisabled_AtrEnabled,
                        { CodeCoverage: false, TestsSkipping: true, EarlyFlakeDetection.Enabled: false, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipEnabled_AtrEnabled,
                        { CodeCoverage: false, TestsSkipping: false, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipDisabled_EFDEnabled_AtrEnabled,
                        { CodeCoverage: true, TestsSkipping: true, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipEnabled_EFDEnabled_AtrEnabled,
                        { CodeCoverage: true, TestsSkipping: false, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipDisabled_EFDEnabled_AtrEnabled,
                        { CodeCoverage: false, TestsSkipping: true, EarlyFlakeDetection.Enabled: true, FlakyTestRetries: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipEnabled_EFDEnabled_AtrEnabled,
                        _ => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipDisabled_AtrDisabled,
                    });
                return settingsResponse;
            }
            finally
            {
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsSettingsMs(sw.Elapsed.TotalMilliseconds);
            }
        }
    }

    public async Task<SkippableTestsResponse> GetSkippableTestsAsync()
    {
        Log.Debug("ITR: Getting skippable tests...");
        var framework = FrameworkDescription.Instance;
        if (string.IsNullOrEmpty(_repositoryUrl))
        {
            Log.Warning("ITR: 'git config --get remote.origin.url' command returned null or empty");
            return new SkippableTestsResponse();
        }

        if (string.IsNullOrEmpty(_commitSha))
        {
            Log.Warning("ITR: 'git rev-parse HEAD' command returned null or empty");
            return default;
        }

        var query = new DataEnvelope<Data<SkippableTestsQuery>>(
            new Data<SkippableTestsQuery>(
                default,
                TestParamsType,
                new SkippableTestsQuery(
                    _serviceName,
                    _environment,
                    _repositoryUrl,
                    _commitSha,
                    new TestsConfigurations(
                        framework.OSPlatform,
                        CIVisibility.GetOperatingSystemVersion(),
                        framework.OSArchitecture,
                        framework.Name,
                        framework.ProductVersion,
                        framework.ProcessArchitecture,
                        _customConfigurations))),
            default);
        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        var jsonQueryBytes = Encoding.UTF8.GetBytes(jsonQuery);
        Log.Debug("ITR: Skippable.JSON RQ = {Json}", jsonQuery);

        return await WithRetries(InternalGetSkippableTestsAsync, jsonQueryBytes, MaxRetries).ConfigureAwait(false);

        async Task<SkippableTestsResponse> InternalGetSkippableTestsAsync(byte[] state, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // We currently always send the request uncompressed
                TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
                var request = _apiRequestFactory.Create(_skippableTestsUrl);
                SetRequestHeader(request);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("ITR: Searching skippable tests from: {Url}", _skippableTestsUrl.ToString());
                }

                string? responseContent;
                try
                {
                    using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
                    // TODO: Check for compressed responses - if we received one, currently these are not handled and would throw when we attempt to deserialize
                    responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    TelemetryFactory.Metrics.RecordDistributionCIVisibilityITRSkippableTestsResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, Encoding.UTF8.GetByteCount(responseContent ?? string.Empty));
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                }
                catch (Exception ex)
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
                    Log.Error(ex, "ITR: Get skippable tests request failed.");
                    throw;
                }

                Log.Debug("ITR: Skippable.JSON RS = {Json}", responseContent);
                if (string.IsNullOrEmpty(responseContent))
                {
                    return new SkippableTestsResponse();
                }

                var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<Data<SkippableTest>>>(responseContent!);
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
                    Log.Debug("ITR: Skippable.JSON Filtered = {Json}", JsonConvert.SerializeObject(testAttributes));
                }

                var totalSkippableTests = testAttributes.ToArray();
                TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsResponseTests(totalSkippableTests.Length);
                return new SkippableTestsResponse(deserializedResult.Meta?.CorrelationId, totalSkippableTests);
            }
            finally
            {
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityITRSkippableTestsRequestMs(sw.Elapsed.TotalMilliseconds);
            }
        }
    }

    public async Task<long> SendObjectsPackFileAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
    {
        Log.Debug("ITR: Packing and sending delta of commits and tree objects...");

        var packFilesObject = GetObjectsPackFileFromWorkingDirectory(commitsToInclude, commitsToExclude);
        if (packFilesObject.Files.Length == 0)
        {
            return 0;
        }

        if (string.IsNullOrEmpty(_repositoryUrl))
        {
            Log.Warning("ITR: 'git config --get remote.origin.url' command returned null or empty");
            return 0;
        }

        var jsonPushedSha = JsonConvert.SerializeObject(new DataEnvelope<Data<object>>(new Data<object>(commitSha, CommitType, default), _repositoryUrl), SerializerSettings);
        Log.Debug("ITR: ObjPack.JSON RQ = {Json}", jsonPushedSha);
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsObjectsPackFiles(packFilesObject.Files.Length);
        long totalUploadSize = 0;
        foreach (var packFile in packFilesObject.Files)
        {
            if (!Directory.Exists(Path.GetDirectoryName(packFile)) || !File.Exists(packFile))
            {
                // Pack files must be sent in order, if a pack file is missing, we stop the upload of the rest of the pack files
                // Previous pack files will enrich the backend with some of the data.
                Log.Error("ITR: Pack file '{PackFile}' is missing, cancelling upload.", packFile);
                break;
            }

            // Send PackFile content
            Log.Information("ITR: Sending {PackFile}", packFile);
            totalUploadSize += await WithRetries(InternalSendObjectsPackFileAsync, packFile, MaxRetries).ConfigureAwait(false);

            // Delete temporal pack file
            try
            {
                File.Delete(packFile);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ITR: Error deleting pack file: '{PackFile}'", packFile);
            }
        }

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsObjectsPackBytes(totalUploadSize);

        // Delete temporary folder after the upload
        if (!string.IsNullOrEmpty(packFilesObject.TemporaryFolder))
        {
            try
            {
                Directory.Delete(packFilesObject.TemporaryFolder, true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "ITR: Error deleting temporary folder: '{TemporaryFolder}'", packFilesObject.TemporaryFolder);
            }
        }

        Log.Information("ITR: Total pack file upload: {TotalUploadSize} bytes", totalUploadSize);
        return totalUploadSize;

        async Task<long> InternalSendObjectsPackFileAsync(string packFile, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // We currently always send the request uncompressed
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPack(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
                var request = _apiRequestFactory.Create(_packFileUrl);
                SetRequestHeader(request);

                using var fileStream = File.Open(packFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                try
                {
                    using var response = await request.PostAsync(
                                                       [
                                                           new MultipartFormItem("pushedSha", MimeTypes.Json, null, new ArraySegment<byte>(jsonPushedShaBytes)),
                                                           new MultipartFormItem("packfile", "application/octet-stream", null, fileStream)
                                                       ])
                                                      .ConfigureAwait(false);
                    var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPackErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                }
                catch (Exception ex)
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(MetricTags.CIVisibilityErrorType.Network);
                    Log.Error(ex, "ITR: Send object pack file request failed.");
                    throw;
                }

                return new FileInfo(packFile).Length;
            }
            finally
            {
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsObjectsPackMs(sw.Elapsed.TotalMilliseconds);
            }
        }
    }

    public async Task<EarlyFlakeDetectionResponse> GetEarlyFlakeDetectionTestsAsync()
    {
        Log.Debug("ITR: Getting early flake detection tests...");
        var framework = FrameworkDescription.Instance;
        if (string.IsNullOrEmpty(_repositoryUrl))
        {
            Log.Warning("ITR: 'git config --get remote.origin.url' command returned null or empty");
            return default;
        }

        if (string.IsNullOrEmpty(_commitSha))
        {
            Log.Warning("ITR: 'git rev-parse HEAD' command returned null or empty");
            return default;
        }

        var query = new DataEnvelope<Data<EarlyFlakeDetectionQuery>>(
            new Data<EarlyFlakeDetectionQuery>(
                _commitSha,
                EarlyFlakeDetectionRequestType,
                new EarlyFlakeDetectionQuery(
                    _serviceName,
                    _environment,
                    _repositoryUrl,
                    new TestsConfigurations(
                        framework.OSPlatform,
                        CIVisibility.GetOperatingSystemVersion(),
                        framework.OSArchitecture,
                        framework.Name,
                        framework.ProductVersion,
                        framework.ProcessArchitecture,
                        _customConfigurations))),
            default);
        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        var jsonQueryBytes = Encoding.UTF8.GetBytes(jsonQuery);
        Log.Debug("ITR: Efd.JSON RQ = {Json}", jsonQuery);

        return await WithRetries(InternalGetEarlyFlakeDetectionTestsAsync, jsonQueryBytes, MaxRetries).ConfigureAwait(false);

        async Task<EarlyFlakeDetectionResponse> InternalGetEarlyFlakeDetectionTestsAsync(byte[] state, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // We currently always send the request uncompressed
                TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
                var request = _apiRequestFactory.Create(_earlyFlakeDetectionTestsUrl);
                SetRequestHeader(request);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("ITR: Getting early flake detection tests from: {Url}", _earlyFlakeDetectionTestsUrl.ToString());
                }

                string? responseContent;
                try
                {
                    using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
                    responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequestErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                    try
                    {
                        if (response.ContentLength is { } contentLength and > 0)
                        {
                            // TODO: Check for compressed responses - currently these are not handled and will throw when we attempt to deserialize
                            TelemetryFactory.Metrics.RecordDistributionCIVisibilityEarlyFlakeDetectionResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, contentLength);
                        }
                    }
                    catch
                    {
                        // If calling ContentLength throws we just ignore it
                    }
                }
                catch (Exception ex)
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityEarlyFlakeDetectionRequestErrors(MetricTags.CIVisibilityErrorType.Network);
                    Log.Error(ex, "ITR: Early flake detection tests request failed.");
                    throw;
                }

                Log.Debug("ITR: Efd.JSON RS = {Json}", responseContent);
                if (string.IsNullOrEmpty(responseContent))
                {
                    return default;
                }

                var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<EarlyFlakeDetectionResponse>?>>(responseContent);
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
            finally
            {
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityEarlyFlakeDetectionRequestMs(sw.Elapsed.TotalMilliseconds);
            }
        }
    }

    public async Task<ImpactedTestsDetectionResponse> GetImpactedTestsDetectionFilesAsync()
    {
        Log.Debug("ITR: Getting impacted tests detection modified files...");
        var framework = FrameworkDescription.Instance;

        if (string.IsNullOrEmpty(_repositoryUrl))
        {
            Log.Warning("ITR: 'git config --get remote.origin.url' command returned null or empty");
            return default;
        }

        if (string.IsNullOrEmpty(_commitSha))
        {
            Log.Warning("ITR: 'git rev-parse HEAD' command returned null or empty");
            return default;
        }

        var query = new DataEnvelope<Data<ImpactedTestsDetectionQuery>>(
            new Data<ImpactedTestsDetectionQuery>(
                _commitSha,
                ImpactedTestsDetectionRequestType,
                new ImpactedTestsDetectionQuery(
                    _serviceName,
                    _environment,
                    _repositoryUrl,
                    _branchName,
                    _commitSha)),
            default);
        var jsonQuery = JsonConvert.SerializeObject(query, SerializerSettings);
        var jsonQueryBytes = Encoding.UTF8.GetBytes(jsonQuery);
        Log.Debug("ITR: Efd.JSON RQ = {Json}", jsonQuery);

        return await WithRetries(InternalGetImpactedTestsDetectionFilesAsync, jsonQueryBytes, MaxRetries).ConfigureAwait(false);

        async Task<ImpactedTestsDetectionResponse> InternalGetImpactedTestsDetectionFilesAsync(byte[] state, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                // We currently always send the request uncompressed
                TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequest(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
                var request = _apiRequestFactory.Create(_impactedTestsDetectionTestsUrl);
                SetRequestHeader(request);

                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("ITR: Getting Impacted test file diffs: {Url}", _impactedTestsDetectionTestsUrl.ToString());
                }

                string? responseContent;
                try
                {
                    using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
                    responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequestErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                    try
                    {
                        if (response.ContentLength is { } contentLength and > 0)
                        {
                            // TODO: Check for compressed responses - currently these are not handled and will throw when we attempt to deserialize
                            TelemetryFactory.Metrics.RecordDistributionCIVisibilityImpactedTestsDetectionResponseBytes(MetricTags.CIVisibilityResponseCompressed.Uncompressed, contentLength);
                        }
                    }
                    catch
                    {
                        // If calling ContentLength throws we just ignore it
                    }
                }
                catch (Exception ex)
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityImpactedTestsDetectionRequestErrors(MetricTags.CIVisibilityErrorType.Network);
                    Log.Error(ex, "ITR: Impacted tests file diffs request failed.");
                    throw;
                }

                Log.Debug("ITR: Efd.JSON RS = {Json}", responseContent);
                if (string.IsNullOrEmpty(responseContent))
                {
                    return default;
                }

                var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<ImpactedTestsDetectionResponse>?>>(responseContent);
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
            finally
            {
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityImpactedTestsDetectionRequestMs(sw.Elapsed.TotalMilliseconds);
            }
        }
    }

    private async Task<SearchCommitResponse> GetCommitsAsync()
    {
        var gitLogOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "log --format=%H -n 1000 --since=\"1 month ago\"", MetricTags.CIVisibilityCommands.GetLocalCommits);
        if (gitLogOutput is null)
        {
            Log.Warning("ITR: 'git log...' command is null");
            return new SearchCommitResponse(null, null, false);
        }

        var localCommits = gitLogOutput.Output.Split(["\n"], StringSplitOptions.RemoveEmptyEntries);
        if (localCommits.Length == 0)
        {
            Log.Debug("ITR: Local commits not found. (since 1 month ago)");
            return new SearchCommitResponse(null, null, false);
        }

        Log.Debug<int>("ITR: Local commits = {Count}", localCommits.Length);
        var remoteCommitsData = await SearchCommitAsync(localCommits).ConfigureAwait(false);
        return new SearchCommitResponse(localCommits, remoteCommitsData, true);

        async Task<string[]> SearchCommitAsync(string[]? commits)
        {
            if (commits is null)
            {
                return [];
            }

            Log.Debug("ITR: Searching commits...");

            Data<object>[] commitRequests;
            if (commits.Length == 0)
            {
                commitRequests = [];
            }
            else
            {
                commitRequests = new Data<object>[commits.Length];
                for (var i = 0; i < commits.Length; i++)
                {
                    commitRequests[i] = new Data<object>(commits[i], CommitType, null);
                }
            }

            var jsonPushedSha = JsonConvert.SerializeObject(new DataArrayEnvelope<Data<object>>(commitRequests, _repositoryUrl), SerializerSettings);
            Log.Debug("ITR: Commits.JSON RQ = {Json}", jsonPushedSha);
            var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

            return await WithRetries(InternalSearchCommitAsync, jsonPushedShaBytes, MaxRetries).ConfigureAwait(false);

            async Task<string[]> InternalSearchCommitAsync(byte[] state, bool finalTry)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    // We currently always send the request uncompressed
                    TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommits(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
                    var request = _apiRequestFactory.Create(_searchCommitsUrl);
                    SetRequestHeader(request);

                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug("ITR: Searching commits from: {Url}", _searchCommitsUrl.ToString());
                    }

                    string? responseContent;
                    try
                    {
                        using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
                        responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                        if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                        {
                            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(errorType);
                        }

                        CheckResponseStatusCode(response, responseContent, finalTry);
                    }
                    catch (Exception ex)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(MetricTags.CIVisibilityErrorType.Network);
                        Log.Error(ex, "ITR: Search commit request failed.");
                        throw;
                    }

                    Log.Debug("ITR: Commits.JSON RS = {Json}", responseContent);
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        return [];
                    }

                    var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<Data<object>>>(responseContent);
                    if (deserializedResult.Data is null || deserializedResult.Data.Length == 0)
                    {
                        return [];
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

                    return stringArray;
                }
                finally
                {
                    TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsSearchCommitsMs(MetricTags.CIVisibilityResponseCompressed.Uncompressed, sw.Elapsed.TotalMilliseconds);
                }
            }
        }
    }

    private ObjectPackFilesResult GetObjectsPackFileFromWorkingDirectory(string[]? commitsToInclude, string[]? commitsToExclude)
    {
        Log.Debug("ITR: Getting objects...");
        commitsToInclude ??= [];
        commitsToExclude ??= [];
        var temporaryFolder = string.Empty;
        var temporaryPath = Path.GetTempFileName();

        var getObjectsArguments = "rev-list --objects --no-object-names --filter=blob:none --since=\"1 month ago\" HEAD " + string.Join(" ", commitsToExclude.Select(c => "^" + c)) + " " + string.Join(" ", commitsToInclude);
        var getObjectsCommand = GitCommandHelper.RunGitCommand(_workingDirectory, getObjectsArguments, MetricTags.CIVisibilityCommands.GetObjects);
        if (string.IsNullOrEmpty(getObjectsCommand?.Output))
        {
            // If not objects has been returned we skip the pack + upload.
            Log.Debug("ITR: No objects were returned from the git rev-list command.");
            return new ObjectPackFilesResult([], temporaryFolder);
        }

        // Sanitize object list (on some cases we get a "fatal: expected object ID, got garbage" error because the object list has invalid escape chars)
        var objectsOutput = getObjectsCommand!.Output;
        var matches = ShaRegex.Matches(objectsOutput);
        var lstObjectsSha = new List<string>(matches.Count);
        foreach (Match? match in matches)
        {
            if (match is not null)
            {
                lstObjectsSha.Add(match.Value);
            }
        }

        if (lstObjectsSha.Count == 0)
        {
            // If not objects has been returned we skip the pack + upload.
            Log.Debug("ITR: No valid objects were returned from the git rev-list command.");
            return new ObjectPackFilesResult([], temporaryFolder);
        }

        objectsOutput = string.Join("\n", lstObjectsSha) + "\n";

        Log.Debug<int>("ITR: Packing {NumObjects} objects...", lstObjectsSha.Count);
        var getPacksArguments = $"pack-objects --compression=9 --max-pack-size={MaxPackFileSizeInMb}m \"{temporaryPath}\"";
        var packObjectsResultCommand = GitCommandHelper.RunGitCommand(_workingDirectory, getPacksArguments, MetricTags.CIVisibilityCommands.PackObjects, objectsOutput);
        if (packObjectsResultCommand is null)
        {
            Log.Warning("ITR: 'git pack-objects...' command is null");
            return new ObjectPackFilesResult([], temporaryFolder);
        }

        if (packObjectsResultCommand.ExitCode != 0)
        {
            if (packObjectsResultCommand.Error.IndexOf("Cross-device", StringComparison.OrdinalIgnoreCase) != -1)
            {
                // Git can throw a cross device error if the temporal folder is in a different drive than the .git folder (eg. symbolic link)
                // to handle this edge case, we create a temporal folder inside the current folder.

                Log.Warning("ITR: 'git pack-objects...' returned a cross-device error, retrying using a local temporal folder.");
                temporaryFolder = Path.Combine(Environment.CurrentDirectory, ".git_tmp");
                if (!Directory.Exists(temporaryFolder))
                {
                    Directory.CreateDirectory(temporaryFolder);
                }

                temporaryPath = Path.Combine(temporaryFolder, Path.GetFileName(temporaryPath));
                getPacksArguments = $"pack-objects --compression=9 --max-pack-size={MaxPackFileSizeInMb}m \"{temporaryPath}\"";
                packObjectsResultCommand = GitCommandHelper.RunGitCommand(_workingDirectory, getPacksArguments, MetricTags.CIVisibilityCommands.PackObjects, getObjectsCommand!.Output);
                if (packObjectsResultCommand is null)
                {
                    Log.Warning("ITR: 'git pack-objects...' command is null");
                    return new ObjectPackFilesResult([], temporaryFolder);
                }
            }

            if (packObjectsResultCommand.ExitCode != 0)
            {
                Log.Warning("ITR: 'git pack-objects...' command error: {Stderr}", packObjectsResultCommand.Error);
            }
        }

        var packObjectsSha = packObjectsResultCommand.Output.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // We try to return an array with the path in the same order as has been returned by the git command.
        var tempFolder = Path.GetDirectoryName(temporaryPath) ?? string.Empty;
        var tempFile = Path.GetFileName(temporaryPath);
        var lstFiles = new List<string>(packObjectsSha.Length);
        foreach (var pObjSha in packObjectsSha)
        {
            var file = Path.Combine(tempFolder, tempFile + "-" + pObjSha + ".pack");
            if (File.Exists(file))
            {
                lstFiles.Add(file);
            }
            else
            {
                Log.Warning("ITR: The file '{PackFile}' doesn't exist.", file);
            }
        }

        return new ObjectPackFilesResult(lstFiles.ToArray(), temporaryFolder);
    }

    private void SetRequestHeader(IApiRequest request)
    {
        request.AddHeader(HttpHeaderNames.TraceId, _id);
        request.AddHeader(HttpHeaderNames.ParentId, _id);
        if (_eventPlatformProxySupport is EventPlatformProxySupport.V2 or EventPlatformProxySupport.V4)
        {
            request.AddHeader(EvpSubdomainHeader, "api");
        }
        else
        {
            request.AddHeader(ApiKeyHeader, _settings.ApiKey);
        }
    }

    private void CheckResponseStatusCode(IApiResponse response, string? responseContent, bool finalTry)
    {
        // Check if the rate limit header was received.
        if (response.StatusCode == 429 &&
            response.GetHeader("x-ratelimit-reset") is { } strRateLimitDurationInSeconds &&
            int.TryParse(strRateLimitDurationInSeconds, out var rateLimitDurationInSeconds))
        {
            if (rateLimitDurationInSeconds > 30)
            {
                // If 'x-ratelimit-reset' is > 30 seconds we cancel the request.
                throw new RateLimitException();
            }

            throw new RateLimitException(rateLimitDurationInSeconds);
        }

        if (response.StatusCode is < 200 or >= 300 && response.StatusCode != 404 && response.StatusCode != 502)
        {
            if (finalTry)
            {
                Log.Error<int, string>("ITR: Request failed with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent ?? string.Empty);
            }

            throw new WebException($"Status: {response.StatusCode}, Content: {responseContent}");
        }
    }

    private async Task<T> WithRetries<T, TState>(Func<TState, bool, Task<T>> sendDelegate, TState state, int numOfRetries)
    {
        var retryCount = 1;
        var sleepDuration = 100; // in milliseconds

        while (true)
        {
            T response = default!;
            ExceptionDispatchInfo? exceptionDispatchInfo = null;
            var isFinalTry = retryCount >= numOfRetries;

            try
            {
                response = await sendDelegate(state, isFinalTry).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
            }

            // Error handling block
            if (exceptionDispatchInfo is not null)
            {
                var sourceException = exceptionDispatchInfo.SourceException;

                if (isFinalTry ||
                    sourceException is RateLimitException { DelayTimeInSeconds: null } ||
                    sourceException is DirectoryNotFoundException ||
                    sourceException is FileNotFoundException)
                {
                    // stop retrying
                    Log.Error<int>(sourceException, "ITR: An error occurred while sending intelligent test runner data after {Retries} retries.", retryCount);
                    exceptionDispatchInfo.Throw();
                }

                // Before retry
                var isSocketException = false;
                var innerException = sourceException;
                while (innerException != null)
                {
                    if (innerException is SocketException)
                    {
                        isSocketException = true;
                        break;
                    }

                    innerException = innerException.InnerException;
                }

                if (isSocketException)
                {
                    Log.Debug(sourceException, "ITR: Unable to communicate with the server");
                }

                if (sourceException is RateLimitException { DelayTimeInSeconds: { } delayTimeInSeconds })
                {
                    // Execute rate limit retry delay
                    await Task.Delay(TimeSpan.FromSeconds(delayTimeInSeconds)).ConfigureAwait(false);
                }
                else
                {
                    // Execute retry delay
                    await Task.Delay(sleepDuration).ConfigureAwait(false);
                    sleepDuration *= 2;
                }

                retryCount++;
                continue;
            }

            Log.Debug("ITR: Request was completed successfully.");
            return response;
        }
    }

    private string GetRepositoryUrl()
    {
        if (CIEnvironmentValues.Instance.Repository is { Length: > 0 } repository)
        {
            return repository;
        }

        var gitOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "config --get remote.origin.url", MetricTags.CIVisibilityCommands.GetRepository);
        return gitOutput?.Output.Replace("\n", string.Empty) ?? string.Empty;
    }

    private string GetBranchName()
    {
        if (CIEnvironmentValues.Instance.Branch is { Length: > 0 } branch)
        {
            return branch;
        }

        var gitOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "branch --show-current", MetricTags.CIVisibilityCommands.GetBranch);
        var res = gitOutput?.Output.Replace("\n", string.Empty) ?? string.Empty;

        if (string.IsNullOrEmpty(res))
        {
            Log.Warning("ITR: empty branch indicates a detached head at commit {Commit}", _commitSha);
            res = $"auto:git-detached-head";
        }

        return res;
    }

    private string GetCommitSha()
    {
        var gitOutput = GitCommandHelper.RunGitCommand(_workingDirectory, "rev-parse HEAD", MetricTags.CIVisibilityCommands.GetHead);
        var gitSha = gitOutput?.Output.Replace("\n", string.Empty) ?? string.Empty;
        if (string.IsNullOrEmpty(gitSha) && CIEnvironmentValues.Instance.Commit is { Length: > 0 } commitSha)
        {
            return commitSha;
        }

        return gitSha;
    }

    private readonly struct SearchCommitResponse
    {
        public readonly string[] LocalCommits;
        public readonly string[] RemoteCommits;
        public readonly bool IsOk;

        public SearchCommitResponse(string[]? localCommits, string[]? remoteCommits, bool isOk)
        {
            LocalCommits = localCommits ?? [];
            RemoteCommits = remoteCommits ?? [];
            IsOk = isOk;
        }

        public bool HasCommits => LocalCommits.Length > 0;

        public string[] MissingCommits => LocalCommits.Except(RemoteCommits).ToArray();
    }

    private readonly struct DataEnvelope<T>
    {
        [JsonProperty("data")]
        public readonly T? Data;

        [JsonProperty("meta")]
        public readonly Metadata? Meta;

        public DataEnvelope(T? data, string? repositoryUrl)
        {
            Data = data;
            Meta = repositoryUrl is null ? default(Metadata?) : new Metadata(repositoryUrl);
        }
    }

    private readonly struct DataArrayEnvelope<T>
    {
        [JsonProperty("data")]
        public readonly T[] Data;

        [JsonProperty("meta")]
        public readonly Metadata? Meta;

        public DataArrayEnvelope(T[] data, string? repositoryUrl)
        {
            Data = data;
            Meta = repositoryUrl is null ? default(Metadata?) : new Metadata(repositoryUrl);
        }
    }

    private readonly struct Metadata
    {
        [JsonProperty("repository_url")]
        public readonly string RepositoryUrl;

        [JsonProperty("correlation_id")]
        public readonly string? CorrelationId;

        public Metadata(string repositoryUrl)
        {
            RepositoryUrl = repositoryUrl;
            CorrelationId = null;
        }

        public Metadata(string repositoryUrl, string? correlationId)
        {
            RepositoryUrl = repositoryUrl;
            CorrelationId = correlationId;
        }
    }

    private readonly struct Data<T>
    {
        [JsonProperty("id")]
        public readonly string? Id;

        [JsonProperty("type")]
        public readonly string Type;

        [JsonProperty("attributes")]
        public readonly T? Attributes;

        public Data(string? id, string type, T? attributes)
        {
            Id = id;
            Type = type;
            Attributes = attributes;
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
        public readonly string? CorrelationId;
        public readonly SkippableTest[] Tests;

        public SkippableTestsResponse()
        {
            CorrelationId = null;
            Tests = [];
        }

        public SkippableTestsResponse(string? correlationId, SkippableTest[] tests)
        {
            CorrelationId = correlationId;
            Tests = tests;
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
    }

    public readonly struct EarlyFlakeDetectionSettingsResponse
    {
        [JsonProperty("enabled")]
        public readonly bool? Enabled;

        [JsonProperty("slow_test_retries")]
        public readonly SlowTestRetriesSettingsResponse SlowTestRetries;

        [JsonProperty("faulty_session_threshold")]
        public readonly int? FaultySessionThreshold;
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
    }

    public readonly struct EarlyFlakeDetectionQuery
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

    public readonly struct ImpactedTestsDetectionQuery
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

    public readonly struct ImpactedTestsDetectionResponse
    {
        [JsonProperty("base_sha")]
        public readonly string? BaseSha;

        [JsonProperty("files")]
        public readonly string[]? Files;
    }

    private class ObjectPackFilesResult
    {
        public ObjectPackFilesResult(string[] files, string temporaryFolder)
        {
            Files = files;
            TemporaryFolder = temporaryFolder;
        }

        public string[] Files { get; }

        public string TemporaryFolder { get; }
    }

    private class RateLimitException : Exception
    {
        public RateLimitException()
            : base("Server rate limiting response received. Cancelling request.")
        {
            DelayTimeInSeconds = null;
        }

        public RateLimitException(int delayTimeInSeconds)
            : base($"Server rate limiting response received. Waiting for {delayTimeInSeconds} seconds")
        {
            DelayTimeInSeconds = delayTimeInSeconds;
        }

        public int? DelayTimeInSeconds { get; private set; }
    }
}

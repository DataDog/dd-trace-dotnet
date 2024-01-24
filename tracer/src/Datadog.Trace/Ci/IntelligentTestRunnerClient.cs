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
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
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

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IntelligentTestRunnerClient));
    private static readonly Regex ShaRegex = new("[0-9a-f]+", RegexOptions.Compiled);
    private static readonly JsonSerializerSettings SerializerSettings = new() { DefaultValueHandling = DefaultValueHandling.Ignore };

    private readonly string _id;
    private readonly CIVisibilitySettings _settings;
    private readonly string _workingDirectory;
    private readonly string _environment;
    private readonly string _serviceName;
    private readonly Dictionary<string, string>? _customConfigurations;
    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly Uri _settingsUrl;
    private readonly Uri _searchCommitsUrl;
    private readonly Uri _packFileUrl;
    private readonly Uri _skippableTestsUrl;
    private readonly bool _useEvpProxy;
    private readonly Task<string> _getRepositoryUrlTask;
    private readonly Task<string> _getBranchNameTask;
    private readonly Task<ProcessHelpers.CommandOutput?> _getShaTask;

    public IntelligentTestRunnerClient(string workingDirectory, CIVisibilitySettings? settings = null)
    {
        _id = RandomIdGenerator.Shared.NextSpanId().ToString(CultureInfo.InvariantCulture);
        _settings = settings ?? CIVisibility.Settings;

        _workingDirectory = workingDirectory;
        _environment = TraceUtil.NormalizeTag(_settings.TracerSettings.EnvironmentInternal ?? "none") ?? "none";
        _serviceName = NormalizerTraceProcessor.NormalizeService(_settings.TracerSettings.ServiceNameInternal) ?? string.Empty;
        _customConfigurations = null;

        // Extract custom tests configurations from DD_TAGS
        _customConfigurations = GetCustomTestsConfigurations(_settings.TracerSettings.GlobalTagsInternal);

        _getRepositoryUrlTask = GetRepositoryUrlAsync();
        _getBranchNameTask = GetBranchNameAsync();
        _getShaTask = ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "rev-parse HEAD", _workingDirectory));
        _apiRequestFactory = CIVisibility.GetRequestFactory(new ImmutableTracerSettings(_settings.TracerSettings, true), TimeSpan.FromSeconds(45));

        const string settingsUrlPath = "api/v2/libraries/tests/services/setting";
        const string searchCommitsUrlPath = "api/v2/git/repository/search_commits";
        const string packFileUrlPath = "api/v2/git/repository/packfile";
        const string skippableTestsUrlPath = "api/v2/ci/tests/skippable";

        if (_settings.Agentless)
        {
            _useEvpProxy = false;
            var agentlessUrl = _settings.AgentlessUrl;
            if (!string.IsNullOrWhiteSpace(agentlessUrl))
            {
                _settingsUrl = new UriBuilder(agentlessUrl) { Path = settingsUrlPath }.Uri;
                _searchCommitsUrl = new UriBuilder(agentlessUrl) { Path = searchCommitsUrlPath }.Uri;
                _packFileUrl = new UriBuilder(agentlessUrl) { Path = packFileUrlPath }.Uri;
                _skippableTestsUrl = new UriBuilder(agentlessUrl) { Path = skippableTestsUrlPath }.Uri;
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
            }
        }
        else
        {
            // Use Agent EVP Proxy
            _useEvpProxy = true;
            var agentUrl = _settings.TracerSettings.ExporterInternal.AgentUriInternal;
            _settingsUrl = UriHelpers.Combine(agentUrl, $"evp_proxy/v2/{settingsUrlPath}");
            _searchCommitsUrl = UriHelpers.Combine(agentUrl, $"evp_proxy/v2/{searchCommitsUrlPath}");
            _packFileUrl = UriHelpers.Combine(agentUrl, $"evp_proxy/v2/{packFileUrlPath}");
            _skippableTestsUrl = UriHelpers.Combine(agentUrl, $"evp_proxy/v2/{skippableTestsUrlPath}");
        }
    }

    internal static Dictionary<string, string>? GetCustomTestsConfigurations(IDictionary<string, string> globalTags)
    {
        Dictionary<string, string>? customConfiguration = null;
        if (globalTags is not null)
        {
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
            var gitRevParseShallowOutput = await RunGitCommandAsync("rev-parse --is-shallow-repository", MetricTags.CIVisibilityCommands.CheckShallow).ConfigureAwait(false);
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
            var gitShallowLogOutput = await RunGitCommandAsync("log --format=oneline -n 2", MetricTags.CIVisibilityCommands.CheckShallow).ConfigureAwait(false);
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

                ProcessHelpers.CommandOutput? gitUnshallowOutput;

                // ***
                // Let's try to unshallow the repo:
                // `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse HEAD)`
                // ***

                // git config --default origin --get clone.defaultRemoteName
                var originNameOutput = await RunGitCommandAsync("config --default origin --get clone.defaultRemoteName", MetricTags.CIVisibilityCommands.GetRemote).ConfigureAwait(false);
                var originName = originNameOutput?.Output?.Replace("\n", string.Empty).Trim() ?? "origin";

                // git rev-parse HEAD
                var headOutput = await RunGitCommandAsync("rev-parse HEAD", MetricTags.CIVisibilityCommands.GetHead).ConfigureAwait(false);
                var head = headOutput?.Output?.Replace("\n", string.Empty).Trim() ?? await _getBranchNameTask.ConfigureAwait(false);

                // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse HEAD)
                Log.Information("ITR: The current repo is a shallow clone, refetching data for {OriginName}|{Head}", originName, head);
                gitUnshallowOutput = await RunGitCommandAsync($"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow).ConfigureAwait(false);

                if (gitUnshallowOutput is null || gitUnshallowOutput.ExitCode != 0)
                {
                    // ***
                    // The previous command has a drawback: if the local HEAD is a commit that has not been pushed to the remote, it will fail.
                    // If this is the case, we fallback to: `git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse --abbrev-ref --symbolic-full-name @{upstream})`
                    // This command will attempt to use the tracked branch for the current branch in order to unshallow.
                    // ***

                    // originName = git config --default origin --get clone.defaultRemoteName
                    // git rev-parse --abbrev-ref --symbolic-full-name @{upstream}
                    headOutput = await RunGitCommandAsync("rev-parse --abbrev-ref --symbolic-full-name \"@{upstream}\"", MetricTags.CIVisibilityCommands.GetHead).ConfigureAwait(false);
                    head = headOutput?.Output?.Replace("\n", string.Empty).Trim() ?? await _getBranchNameTask.ConfigureAwait(false);

                    // git fetch --shallow-since="1 month ago" --update-shallow --filter="blob:none" --recurse-submodules=no $(git config --default origin --get clone.defaultRemoteName) $(git rev-parse --abbrev-ref --symbolic-full-name @{upstream})
                    Log.Information("ITR: Previous unshallow command failed, refetching data with fallback 1 for {OriginName}|{Head}", originName, head);
                    gitUnshallowOutput = await RunGitCommandAsync($"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName} {head}", MetricTags.CIVisibilityCommands.Unshallow).ConfigureAwait(false);
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
                    await RunGitCommandAsync($"fetch --shallow-since=\"1 month ago\" --update-shallow --filter=\"blob:none\" --recurse-submodules=no {originName}", MetricTags.CIVisibilityCommands.Unshallow).ConfigureAwait(false);
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
        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var branchName = await _getBranchNameTask.ConfigureAwait(false);
        var currentShaCommand = await _getShaTask.ConfigureAwait(false);
        if (currentShaCommand is null)
        {
            Log.Warning("ITR: 'git rev-parse HEAD' command is null");
            return default;
        }

        var currentSha = currentShaCommand.Output.Replace("\n", string.Empty);

        var query = new DataEnvelope<Data<SettingsQuery>>(
            new Data<SettingsQuery>(
                currentSha,
                SettingsType,
                new SettingsQuery(
                    _serviceName,
                    _environment,
                    repository,
                    branchName,
                    currentSha,
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
        Log.Debug("ITR: JSON RQ = {Json}", jsonQuery);

        return await WithRetries(InternalGetSettingsAsync, jsonQueryBytes, MaxRetries).ConfigureAwait(false);

        async Task<SettingsResponse> InternalGetSettingsAsync(byte[] state, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettings();
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
                catch
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsErrors(MetricTags.CIVisibilityErrorType.Network);
                    throw;
                }

                Log.Debug("ITR: JSON RS = {Json}", responseContent);
                if (string.IsNullOrEmpty(responseContent))
                {
                    return default;
                }

                var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<SettingsResponse>?>>(responseContent);
                var settingsResponse = deserializedResult.Data?.Attributes ?? default;
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSettingsResponse(settingsResponse switch
                {
                    { CodeCoverage: true, TestsSkipping: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipEnabled,
                    { CodeCoverage: true, TestsSkipping: !true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageEnabled_ItrSkipDisabled,
                    { CodeCoverage: !true, TestsSkipping: true } => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipEnabled,
                    _ => MetricTags.CIVisibilityITRSettingsResponse.CoverageDisabled_ItrSkipDisabled,
                });
                return settingsResponse;
            }
            finally
            {
                TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsSettingsMs(sw.Elapsed.TotalMilliseconds);
            }
        }
    }

    public async Task<SkippableTest[]> GetSkippableTestsAsync()
    {
        Log.Debug("ITR: Getting skippable tests...");
        var framework = FrameworkDescription.Instance;
        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var currentShaCommand = await _getShaTask.ConfigureAwait(false);
        if (currentShaCommand is null)
        {
            Log.Warning("ITR: 'git rev-parse HEAD' command is null");
            return Array.Empty<SkippableTest>();
        }

        var currentSha = currentShaCommand.Output.Replace("\n", string.Empty);

        var query = new DataEnvelope<Data<SkippableTestsQuery>>(
            new Data<SkippableTestsQuery>(
                default,
                TestParamsType,
                new SkippableTestsQuery(
                    _serviceName,
                    _environment,
                    repository,
                    currentSha,
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
        Log.Debug("ITR: JSON RQ = {Json}", jsonQuery);

        return await WithRetries(InternalGetSkippableTestsAsync, jsonQueryBytes, MaxRetries).ConfigureAwait(false);

        async Task<SkippableTest[]> InternalGetSkippableTestsAsync(byte[] state, bool finalTry)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequest();
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
                    responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    TelemetryFactory.Metrics.RecordDistributionCIVisibilityITRSkippableTestsResponseBytes(Encoding.UTF8.GetByteCount(responseContent ?? string.Empty));
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                }
                catch
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsRequestErrors(MetricTags.CIVisibilityErrorType.Network);
                    throw;
                }

                Log.Debug("ITR: JSON RS = {Json}", responseContent);
                if (string.IsNullOrEmpty(responseContent))
                {
                    return Array.Empty<SkippableTest>();
                }

                var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<Data<SkippableTest>>>(responseContent!);
                if (deserializedResult.Data is null || deserializedResult.Data.Length == 0)
                {
                    return Array.Empty<SkippableTest>();
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
                    Log.Debug("ITR: JSON Filtered = {Json}", JsonConvert.SerializeObject(testAttributes));
                }

                var totalSkippableTests = testAttributes.ToArray();
                TelemetryFactory.Metrics.RecordCountCIVisibilityITRSkippableTestsResponseTests(totalSkippableTests.Length);
                return totalSkippableTests;
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

        var packFilesObject = await GetObjectsPackFileFromWorkingDirectoryAsync(commitsToInclude, commitsToExclude).ConfigureAwait(false);
        if (packFilesObject is null || packFilesObject.Files.Length == 0)
        {
            return 0;
        }

        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var jsonPushedSha = JsonConvert.SerializeObject(new DataEnvelope<Data<object>>(new Data<object>(commitSha, CommitType, default), repository), SerializerSettings);
        Log.Debug("ITR: JSON RQ = {Json}", jsonPushedSha);
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsObjectsPackFiles(packFilesObject.Files.Length);
        long totalUploadSize = 0;
        foreach (var packFile in packFilesObject.Files)
        {
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
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPack();
                var request = _apiRequestFactory.Create(_packFileUrl);
                SetRequestHeader(request);

                var multipartRequest = (IMultipartApiRequest)request;
                using var fileStream = File.Open(packFile, FileMode.Open, FileAccess.Read, FileShare.Read);

                try
                {
                    using var response = await multipartRequest.PostAsync([
                                                                    new MultipartFormItem("pushedSha", MimeTypes.Json, null, new ArraySegment<byte>(jsonPushedShaBytes)),
                                                                    new MultipartFormItem("packfile", "application/octet-stream", null, fileStream)])
                                                               .ConfigureAwait(false);
                    var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
                    if (TelemetryHelper.GetErrorTypeFromStatusCode(response.StatusCode) is { } errorType)
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPackErrors(errorType);
                    }

                    CheckResponseStatusCode(response, responseContent, finalTry);
                }
                catch
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(MetricTags.CIVisibilityErrorType.Network);
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

    private async Task<SearchCommitResponse> GetCommitsAsync()
    {
        var gitLogOutput = await RunGitCommandAsync("log --format=%H -n 1000 --since=\"1 month ago\"", MetricTags.CIVisibilityCommands.GetLocalCommits).ConfigureAwait(false);
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
                return Array.Empty<string>();
            }

            Log.Debug("ITR: Searching commits...");

            Data<object>[] commitRequests;
            if (commits.Length == 0)
            {
                commitRequests = Array.Empty<Data<object>>();
            }
            else
            {
                commitRequests = new Data<object>[commits.Length];
                for (var i = 0; i < commits.Length; i++)
                {
                    commitRequests[i] = new Data<object>(commits[i], CommitType, default);
                }
            }

            var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
            var jsonPushedSha = JsonConvert.SerializeObject(new DataArrayEnvelope<Data<object>>(commitRequests, repository), SerializerSettings);
            Log.Debug("ITR: JSON RQ = {Json}", jsonPushedSha);
            var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

            return await WithRetries(InternalSearchCommitAsync, jsonPushedShaBytes, MaxRetries).ConfigureAwait(false);

            async Task<string[]> InternalSearchCommitAsync(byte[] state, bool finalTry)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommits();
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
                    catch
                    {
                        TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsSearchCommitsErrors(MetricTags.CIVisibilityErrorType.Network);
                        throw;
                    }

                    Log.Debug("ITR: JSON RS = {Json}", responseContent);
                    if (string.IsNullOrEmpty(responseContent))
                    {
                        return Array.Empty<string>();
                    }

                    var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<Data<object>>>(responseContent);
                    if (deserializedResult.Data is null || deserializedResult.Data.Length == 0)
                    {
                        return Array.Empty<string>();
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
                    TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsSearchCommitsMs(sw.Elapsed.TotalMilliseconds);
                }
            }
        }
    }

    private async Task<ObjectPackFilesResult> GetObjectsPackFileFromWorkingDirectoryAsync(string[]? commitsToInclude, string[]? commitsToExclude)
    {
        Log.Debug("ITR: Getting objects...");
        commitsToInclude ??= Array.Empty<string>();
        commitsToExclude ??= Array.Empty<string>();
        var temporaryFolder = string.Empty;
        var temporaryPath = Path.GetTempFileName();

        var getObjectsArguments = "rev-list --objects --no-object-names --filter=blob:none --since=\"1 month ago\" HEAD " + string.Join(" ", commitsToExclude.Select(c => "^" + c)) + " " + string.Join(" ", commitsToInclude);
        var getObjectsCommand = await RunGitCommandAsync(getObjectsArguments, MetricTags.CIVisibilityCommands.GetObjects).ConfigureAwait(false);
        if (string.IsNullOrEmpty(getObjectsCommand?.Output))
        {
            // If not objects has been returned we skip the pack + upload.
            Log.Debug("ITR: No objects were returned from the git rev-list command.");
            return new ObjectPackFilesResult(Array.Empty<string>(), temporaryFolder);
        }

        Log.Debug("ITR: Packing objects...");
        var getPacksArguments = $"pack-objects --compression=9 --max-pack-size={MaxPackFileSizeInMb}m \"{temporaryPath}\"";
        var packObjectsResultCommand = await RunGitCommandAsync(getPacksArguments, MetricTags.CIVisibilityCommands.PackObjects, getObjectsCommand!.Output).ConfigureAwait(false);
        if (packObjectsResultCommand is null)
        {
            Log.Warning("ITR: 'git pack-objects...' command is null");
            return new ObjectPackFilesResult(Array.Empty<string>(), temporaryFolder);
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
                packObjectsResultCommand = await RunGitCommandAsync(getPacksArguments, MetricTags.CIVisibilityCommands.PackObjects, getObjectsCommand!.Output).ConfigureAwait(false);
                if (packObjectsResultCommand is null)
                {
                    Log.Warning("ITR: 'git pack-objects...' command is null");
                    return new ObjectPackFilesResult(Array.Empty<string>(), temporaryFolder);
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
        if (_useEvpProxy)
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
        // Because there's an integration to Http requests we need to make sure that if the AssemblyResolver.ctor of the TestPlatform started then it has finished
        // before continuing to avoid a race condition.
        await ClrProfiler.AutoInstrumentation.Testing.AssemblyResolverCtorIntegration.WaitForCallToBeCompletedAsync().ConfigureAwait(false);

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

                if (isFinalTry || sourceException is RateLimitException { DelayTimeInSeconds: null })
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
                    Log.Debug(sourceException, "Unable to communicate with the server");
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

            Log.Debug("Request was completed successfully.");
            return response;
        }
    }

    private async Task<string> GetRepositoryUrlAsync()
    {
        if (CIEnvironmentValues.Instance.Repository is { Length: > 0 } repository)
        {
            return repository;
        }

        var gitOutput = await RunGitCommandAsync("config --get remote.origin.url", MetricTags.CIVisibilityCommands.GetRepository).ConfigureAwait(false);
        return gitOutput?.Output.Replace("\n", string.Empty) ?? string.Empty;
    }

    private async Task<string> GetBranchNameAsync()
    {
        if (CIEnvironmentValues.Instance.Branch is { Length: > 0 } branch)
        {
            return branch;
        }

        var gitOutput = await RunGitCommandAsync("branch --show-current", MetricTags.CIVisibilityCommands.GetBranch).ConfigureAwait(false);
        return gitOutput?.Output.Replace("\n", string.Empty) ?? string.Empty;
    }

    private async Task<ProcessHelpers.CommandOutput?> RunGitCommandAsync(string arguments, MetricTags.CIVisibilityCommands ciVisibilityCommand, string? input = null)
    {
        // Because there's an integration to Process.Start we need to make sure that if the AssemblyResolver.ctor of the TestPlatform started then it has finished
        // before continuing to avoid a race condition.
        await ClrProfiler.AutoInstrumentation.Testing.AssemblyResolverCtorIntegration.WaitForCallToBeCompletedAsync().ConfigureAwait(false);

        TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommand(ciVisibilityCommand);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var gitOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", arguments, _workingDirectory), input).ConfigureAwait(false);
        TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitCommandMs(ciVisibilityCommand, sw.Elapsed.TotalMilliseconds);
        if (gitOutput is null)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(ciVisibilityCommand, MetricTags.CIVisibilityExitCodes.Unknown);
            Log.Warning("ITR: 'git {Arguments}' command is null", arguments);
        }
        else if (gitOutput.ExitCode != 0)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitCommandErrors(MetricTags.CIVisibilityCommands.GetRepository, TelemetryHelper.GetTelemetryExitCodeFromExitCode(gitOutput.ExitCode));
        }

        return gitOutput;
    }

    private readonly struct SearchCommitResponse
    {
        public readonly string[] LocalCommits;
        public readonly string[] RemoteCommits;
        public readonly bool IsOk;

        public SearchCommitResponse(string[]? localCommits, string[]? remoteCommits, bool isOk)
        {
            LocalCommits = localCommits ?? Array.Empty<string>();
            RemoteCommits = remoteCommits ?? Array.Empty<string>();
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

        public Metadata(string repositoryUrl)
        {
            RepositoryUrl = repositoryUrl;
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
#pragma warning disable CS0649 // Readonly field is never assigned to - this is created using JSON deserialization
        [JsonProperty("code_coverage")]
        public readonly bool? CodeCoverage;

        [JsonProperty("tests_skipping")]
        public readonly bool? TestsSkipping;

        [JsonProperty("require_git")]
        public readonly bool? RequireGit;
#pragma warning restore CS0649
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

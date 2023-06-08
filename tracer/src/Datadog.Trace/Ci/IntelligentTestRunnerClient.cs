// <copyright file="IntelligentTestRunnerClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
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
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
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
    private const string ApplicationKeyHeader = "DD-APPLICATION-KEY";
    private const string EvpSubdomainHeader = "X-Datadog-EVP-Subdomain";
    private const string EvpNeedsApplicationKeyHeader = "X-Datadog-NeedsAppKey";

    private const int MaxRetries = 5;
    private const int MaxPackFileSizeInMb = 3;

    private const string CommitType = "commit";
    private const string TestParamsType = "test_params";
    private const string SettingsType = "ci_app_test_service_libraries_settings";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IntelligentTestRunnerClient));
    private static readonly Regex ShaRegex = new Regex("[0-9a-f]+", RegexOptions.Compiled);
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
        _apiRequestFactory = CIVisibility.GetRequestFactory(_settings.TracerSettings.Build(), TimeSpan.FromSeconds(45));

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

        try
        {
            // We need to check if the git clone is a shallow one before uploading anything.
            // In the case is a shallow clone we need to reconfigure it to upload the git tree
            // without blobs so no content will be downloaded.
            var gitRevParseShallowOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "rev-parse --is-shallow-repository", _workingDirectory)).ConfigureAwait(false);
            if (gitRevParseShallowOutput is null)
            {
                Log.Warning("ITR: 'git rev-parse --is-shallow-repository' command is null");
                return 0;
            }

            if (gitRevParseShallowOutput.Output.IndexOf("true", StringComparison.OrdinalIgnoreCase) > -1)
            {
                // The git repo is a shallow clone, we need to double check if there are more than just 1 commit in the logs.
                var gitShallowLogOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "log --format=oneline -n 2", _workingDirectory)).ConfigureAwait(false);
                if (gitShallowLogOutput is null)
                {
                    Log.Warning("ITR: 'git log --format=oneline -n 2' command is null");
                    return 0;
                }

                // After asking for 2 logs lines, if the git log command returns just one commit sha, we reconfigure the repo
                // to ask for git commits and trees of the last month (no blobs)
                var shallowLogArray = gitShallowLogOutput.Output.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (shallowLogArray.Length == 1)
                {
                    // Just one commit SHA. Reconfiguring repo
                    Log.Information("ITR: The current repo is a shallow clone, reconfiguring git repo and refetching data...");
                    await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "config remote.origin.partialclonefilter \"blob:none\"", _workingDirectory)).ConfigureAwait(false);
                    await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "fetch --shallow-since=\"1 month ago\" --update-shallow --refetch", _workingDirectory)).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error detecting and reconfiguring git repository for shallow clone.");
        }

        var gitLogOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "log --format=%H -n 1000 --since=\"1 month ago\"", _workingDirectory)).ConfigureAwait(false);
        if (gitLogOutput is null)
        {
            Log.Warning("ITR: 'git log...' command is null");
            return 0;
        }

        var localCommits = gitLogOutput.Output.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (localCommits.Length == 0)
        {
            Log.Debug("ITR: Local commits not found. (since 1 month ago)");
            return 0;
        }

        Log.Debug<int>("ITR: Local commits = {Count}", localCommits.Length);
        var remoteCommitsData = await SearchCommitAsync(localCommits).ConfigureAwait(false);
        return await SendObjectsPackFileAsync(localCommits[0], remoteCommitsData).ConfigureAwait(false);
    }

    public async Task<SettingsResponse> GetSettingsAsync(bool skipFrameworkInfo = false)
    {
        Log.Debug("ITR: Getting settings...");
        if (!_useEvpProxy && string.IsNullOrEmpty(_settings.ApplicationKey))
        {
            Log.Error("ITR: Error getting settings: Application key is missing.");
        }

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
            var request = _apiRequestFactory.Create(_settingsUrl);
            SetRequestHeader(request, useApplicationHeader: true);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("ITR: Getting settings from: {Url}", _settingsUrl.ToString());
            }

            using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            CheckResponseStatusCode(response, responseContent, finalTry);

            Log.Debug("ITR: JSON RS = {Json}", responseContent);
            if (string.IsNullOrEmpty(responseContent))
            {
                return default;
            }

            var deserializedResult = JsonConvert.DeserializeObject<DataEnvelope<Data<SettingsResponse>?>>(responseContent);
            return deserializedResult.Data?.Attributes ?? default;
        }
    }

    public async Task<SkippableTest[]> GetSkippableTestsAsync()
    {
        Log.Debug("ITR: Getting skippable tests...");
        if (!_useEvpProxy && string.IsNullOrEmpty(_settings.ApplicationKey))
        {
            Log.Error("ITR: Error getting skippable tests: Application key is missing.");
        }

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
            var request = _apiRequestFactory.Create(_skippableTestsUrl);
            SetRequestHeader(request, useApplicationHeader: true);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("ITR: Searching skippable tests from: {Url}", _skippableTestsUrl.ToString());
            }

            using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            CheckResponseStatusCode(response, responseContent, finalTry);

            Log.Debug("ITR: JSON RS = {Json}", responseContent);
            if (string.IsNullOrEmpty(responseContent))
            {
                return Array.Empty<SkippableTest>();
            }

            var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<Data<SkippableTest>>>(responseContent);
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

            return testAttributes.ToArray();
        }
    }

    private async Task<string[]> SearchCommitAsync(string[]? localCommits)
    {
        if (localCommits is null)
        {
            return Array.Empty<string>();
        }

        Log.Debug("ITR: Searching commits...");

        Data<object>[] commitRequests;
        if (localCommits.Length == 0)
        {
            commitRequests = Array.Empty<Data<object>>();
        }
        else
        {
            commitRequests = new Data<object>[localCommits.Length];
            for (var i = 0; i < localCommits.Length; i++)
            {
                commitRequests[i] = new Data<object>(localCommits[i], CommitType, default);
            }
        }

        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var jsonPushedSha = JsonConvert.SerializeObject(new DataArrayEnvelope<Data<object>>(commitRequests, repository), SerializerSettings);
        Log.Debug("ITR: JSON RQ = {Json}", jsonPushedSha);
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

        return await WithRetries(InternalSearchCommitAsync, jsonPushedShaBytes, MaxRetries).ConfigureAwait(false);

        async Task<string[]> InternalSearchCommitAsync(byte[] state, bool finalTry)
        {
            var request = _apiRequestFactory.Create(_searchCommitsUrl);
            SetRequestHeader(request, useApplicationHeader: false);

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                Log.Debug("ITR: Searching commits from: {Url}", _searchCommitsUrl.ToString());
            }

            using var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            CheckResponseStatusCode(response, responseContent, finalTry);

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
    }

    public async Task<long> SendObjectsPackFileAsync(string commitSha, string[]? commitsToExclude)
    {
        Log.Debug("ITR: Packing and sending delta of commits and tree objects...");

        var packFilesObject = await GetObjectsPackFileFromWorkingDirectoryAsync(commitsToExclude).ConfigureAwait(false);
        if (packFilesObject is null || packFilesObject.Files.Length == 0)
        {
            return 0;
        }

        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var jsonPushedSha = JsonConvert.SerializeObject(new DataEnvelope<Data<object>>(new Data<object>(commitSha, CommitType, default), repository), SerializerSettings);
        Log.Debug("ITR: JSON RQ = {Json}", jsonPushedSha);
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

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
            var request = _apiRequestFactory.Create(_packFileUrl);
            SetRequestHeader(request, useApplicationHeader: false);

            var multipartRequest = (IMultipartApiRequest)request;
            using var fileStream = File.Open(packFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var response = await multipartRequest.PostAsync(
                new MultipartFormItem("pushedSha", MimeTypes.Json, null, new ArraySegment<byte>(jsonPushedShaBytes)),
                new MultipartFormItem("packfile", "application/octet-stream", null, fileStream))
            .ConfigureAwait(false);
            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            CheckResponseStatusCode(response, responseContent, finalTry);

            return new FileInfo(packFile).Length;
        }
    }

    private async Task<ObjectPackFilesResult> GetObjectsPackFileFromWorkingDirectoryAsync(string[]? commitsToExclude)
    {
        Log.Debug("ITR: Getting objects...");
        commitsToExclude ??= Array.Empty<string>();
        var temporaryFolder = string.Empty;
        var temporaryPath = Path.GetTempFileName();

        var getObjectsArguments = "rev-list --objects --no-object-names --filter=blob:none --since=\"1 month ago\" HEAD " + string.Join(" ", commitsToExclude.Select(c => "^" + c));
        var getObjectsCommand = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", getObjectsArguments, _workingDirectory)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(getObjectsCommand?.Output))
        {
            // If not objects has been returned we skip the pack + upload.
            Log.Debug("ITR: No objects were returned from the git rev-list command.");
            return new ObjectPackFilesResult(Array.Empty<string>(), temporaryFolder);
        }

        Log.Debug("ITR: Packing objects...");
        var getPacksArguments = $"pack-objects --compression=9 --max-pack-size={MaxPackFileSizeInMb}m \"{temporaryPath}\"";
        var packObjectsResultCommand = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", getPacksArguments, _workingDirectory), getObjectsCommand!.Output).ConfigureAwait(false);
        if (packObjectsResultCommand is null)
        {
            Log.Warning("ITR: 'git pack-objects...' command is null");
            return new ObjectPackFilesResult(Array.Empty<string>(), temporaryFolder);
        }

        if (packObjectsResultCommand.ExitCode != 0 && packObjectsResultCommand.Error.IndexOf("Cross-device", StringComparison.OrdinalIgnoreCase) != -1)
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
            packObjectsResultCommand = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", getPacksArguments, _workingDirectory), getObjectsCommand!.Output).ConfigureAwait(false);
            if (packObjectsResultCommand is null)
            {
                Log.Warning("ITR: 'git pack-objects...' command is null");
                return new ObjectPackFilesResult(Array.Empty<string>(), temporaryFolder);
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

    private void SetRequestHeader(IApiRequest request, bool useApplicationHeader)
    {
        request.AddHeader(HttpHeaderNames.TraceId, _id);
        request.AddHeader(HttpHeaderNames.ParentId, _id);
        if (_useEvpProxy)
        {
            request.AddHeader(EvpSubdomainHeader, "api");
            if (useApplicationHeader)
            {
                request.AddHeader(EvpNeedsApplicationKeyHeader, "true");
            }
        }
        else
        {
            request.AddHeader(ApiKeyHeader, _settings.ApiKey);
            if (useApplicationHeader)
            {
                request.AddHeader(ApplicationKeyHeader, _settings.ApplicationKey);
            }
        }
    }

    private void CheckResponseStatusCode(IApiResponse response, string responseContent, bool finalTry)
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
                Log.Error<int, string>("ITR: Request failed with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
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
        var gitOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "config --get remote.origin.url", _workingDirectory)).ConfigureAwait(false);
        if (gitOutput is null)
        {
            Log.Warning("ITR: 'git config --get remote.origin.url' command is null");
            return string.Empty;
        }

        return gitOutput.Output.Replace("\n", string.Empty);
    }

    private async Task<string> GetBranchNameAsync()
    {
        var gitOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "branch --show-current", _workingDirectory)).ConfigureAwait(false);
        if (gitOutput is null)
        {
            Log.Warning("ITR: 'git branch --show-current' command is null");
            return string.Empty;
        }

        return gitOutput.Output.Replace("\n", string.Empty);
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

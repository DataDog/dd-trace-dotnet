// <copyright file="ITRClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
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
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci;

/// <summary>
/// Intelligent Test Runner Client
/// </summary>
internal class ITRClient
{
    private const string ApiKeyHeader = "dd-api-key";
    private const int MaxRetries = 3;
    private const int MaxPackFileSizeInMb = 3;

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ITRClient));
    private static readonly Regex ShaRegex = new Regex("[0-9a-f]+", RegexOptions.Compiled);

    private readonly GlobalSettings _globalSettings;
    private readonly CIVisibilitySettings _settings;
    private readonly string _workingDirectory;
    private readonly IApiRequestFactory _apiRequestFactory;
    private readonly Uri _searchCommitsUrl;
    private readonly Uri _packFileUrl;
    private readonly Task<string> _getRepositoryUrlTask;

    public ITRClient(string workingDirectory, CIVisibilitySettings settings = null)
    {
        _globalSettings = GlobalSettings.FromDefaultSources();
        _settings = settings ?? CIVisibility.Settings;

        _workingDirectory = workingDirectory;
        _getRepositoryUrlTask = GetRepositoryUrlAsync();
        _apiRequestFactory = CIVisibility.GetRequestFactory(_settings.TracerSettings.Build());

        var agentlessUrl = _settings.AgentlessUrl;
        if (!string.IsNullOrWhiteSpace(agentlessUrl))
        {
            _searchCommitsUrl = new UriBuilder(agentlessUrl)
            {
                Path = "api/v2/git/repository/search_commits"
            }.Uri;

            _packFileUrl = new UriBuilder(agentlessUrl)
            {
                Path = "api/v2/git/repository/packfile"
            }.Uri;
        }
        else
        {
            _searchCommitsUrl = new UriBuilder(
                scheme: "https",
                host: "api." + _settings.Site,
                port: 443,
                pathValue: "api/v2/git/repository/search_commits").Uri;

            _packFileUrl = new UriBuilder(
                scheme: "https",
                host: "api." + _settings.Site,
                port: 443,
                pathValue: "api/v2/git/repository/packfile").Uri;
        }
    }

    public async Task<long> UploadRepositoryChangesAsync()
    {
        Log.Debug("ITR: Uploading Repository Changes...");
        var gitOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "log --format=%H -n 1000 --since=\"1 month ago\"", _workingDirectory)).ConfigureAwait(false);
        var localCommits = gitOutput.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (localCommits.Length == 0)
        {
            return 0;
        }

        var remoteCommitsData = await SearchCommitAsync(localCommits).ConfigureAwait(false);
        return await SendObjectsPackFileAsync(localCommits[0], remoteCommitsData).ConfigureAwait(false);
    }

    private async Task<string[]> SearchCommitAsync(string[] localCommits)
    {
        if (localCommits is null)
        {
            return null;
        }

        Log.Debug("ITR: Searching commits...");

        var commitRequests = new CommitRequest[localCommits.Length];
        for (var i = 0; i < localCommits.Length; i++)
        {
            commitRequests[i] = new CommitRequest(localCommits[i]);
        }

        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var jsonPushedSha = JsonConvert.SerializeObject(new DataArrayEnvelopeWithMeta<CommitRequest>(commitRequests, repository));
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

        return await WithRetries(InternalSearchCommitAsync, jsonPushedShaBytes, MaxRetries).ConfigureAwait(false);

        async Task<string[]> InternalSearchCommitAsync(byte[] state, bool finalTry)
        {
            var request = _apiRequestFactory.Create(_searchCommitsUrl);
            request.AddHeader(ApiKeyHeader, _settings.ApiKey);
            Log.Debug("ITR: Searching commits from: {url}", _searchCommitsUrl.ToString());
            var response = await request.PostAsync(new ArraySegment<byte>(state), MimeTypes.Json).ConfigureAwait(false);
            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 300)
            {
                if (finalTry)
                {
                    try
                    {
                        Log.Error<int, string>("Failed to submit events with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                    }
                    catch (Exception ex)
                    {
                        Log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                    }
                }

                throw new WebException($"Status: {response.StatusCode}, Content: {responseContent}");
            }

            var deserializedResult = JsonConvert.DeserializeObject<DataArrayEnvelope<CommitResponse>>(responseContent);
            if (deserializedResult.Data is null)
            {
                return null;
            }

            var stringArray = new string[deserializedResult.Data.Length];
            for (var i = 0; i < deserializedResult.Data.Length; i++)
            {
                var value = deserializedResult.Data[i].Id;
                if (ShaRegex.Matches(value).Count != 1)
                {
                    ThrowHelper.ThrowException($"The value '{value}' is not a valid Sha.");
                }

                stringArray[i] = deserializedResult.Data[i].Id;
            }

            return stringArray;
        }
    }

    public async Task<long> SendObjectsPackFileAsync(string commitSha, string[] commitsExceptions)
    {
        Log.Debug("ITR: Packing and sending delta of commits and tree objects...");

        var packFiles = await GetObjectsPackFileFromWorkingDirectoryAsync(commitsExceptions).ConfigureAwait(false);
        if (packFiles is null)
        {
            return 0;
        }

        var repository = await _getRepositoryUrlTask.ConfigureAwait(false);
        var jsonPushedSha = JsonConvert.SerializeObject(new DataEnvelopeWithMeta<CommitRequest>(new CommitRequest(commitSha), repository));
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

        long totalUploadSize = 0;
        foreach (var packFile in packFiles)
        {
            // Send PackFile content
            Log.Information("ITR: Sending {packFile}", packFile);
            totalUploadSize += await WithRetries(InternalSendObjectsPackFileAsync, packFile, MaxRetries).ConfigureAwait(false);

            // Delete temporal pack file
            try
            {
                File.Delete(packFile);
            }
            catch
            {
                // .
            }
        }

        Log.Information("ITR: Total pack file upload: {totalUploadSize} bytes", totalUploadSize);
        return totalUploadSize;

        async Task<long> InternalSendObjectsPackFileAsync(string packFile, bool finalTry)
        {
            var request = _apiRequestFactory.Create(_packFileUrl);
            request.AddHeader(ApiKeyHeader, _settings.ApiKey);
            var multipartRequest = (IMultipartApiRequest)request;

            using var fileStream = File.Open(packFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var response = await multipartRequest.PostAsync(
                new MultipartFormItem("pushedSha", MimeTypes.Json, null, new ArraySegment<byte>(jsonPushedShaBytes)),
                new MultipartFormItem("packfile", "application/octet-stream", null, fileStream))
            .ConfigureAwait(false);
            var responseContent = await response.ReadAsStringAsync().ConfigureAwait(false);
            if (response.StatusCode is < 200 or >= 300)
            {
                if (finalTry)
                {
                    try
                    {
                        Log.Error<int, string>("Failed to submit events with status code {StatusCode} and message: {ResponseContent}", response.StatusCode, responseContent);
                    }
                    catch (Exception ex)
                    {
                        Log.Error<int>(ex, "Unable to read response for failed request with status code {StatusCode}", response.StatusCode);
                    }
                }

                throw new WebException($"Status: {response.StatusCode}, Content: {responseContent}");
            }

            return new FileInfo(packFile).Length;
        }
    }

    private async Task<string[]> GetObjectsPackFileFromWorkingDirectoryAsync(string[] commitsExceptions)
    {
        Log.Debug("ITR: Getting objects...");
        commitsExceptions ??= Array.Empty<string>();
        var temporalPath = Path.GetTempFileName();

        var getObjectsArguments = "rev-list --objects --no-object-names --filter=blob:none --since=\"1 month ago\" HEAD " + string.Join(" ", commitsExceptions.Select(c => "^" + c));
        var getObjects = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", getObjectsArguments, _workingDirectory)).ConfigureAwait(false);
        if (string.IsNullOrEmpty(getObjects))
        {
            // If not objects has been returned we skip the pack + upload.
            Log.Debug("ITR: No objects were returned from the git rev-list command.");
            return null;
        }

        Log.Debug("ITR: Packing objects...");
        var getPacksArguments = $"pack-objects --compression=9 --max-pack-size={MaxPackFileSizeInMb}m  {temporalPath}";
        var packObjectsResult = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", getPacksArguments, _workingDirectory), getObjects).ConfigureAwait(false);
        var packObjectsSha = packObjectsResult.Split(new[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // We try to return an array with the path in the same order as has been returned by the git command.
        var tempFolder = Path.GetDirectoryName(temporalPath) ?? string.Empty;
        var tempFile = Path.GetFileName(temporalPath);
        var lstFiles = new List<string>(packObjectsSha.Length);
        foreach (var pObjSha in packObjectsSha)
        {
            var file = Path.Combine(tempFolder, tempFile + "-" + pObjSha + ".pack");
            if (File.Exists(file))
            {
                lstFiles.Add(file);
            }
        }

        return lstFiles.ToArray();
    }

    private async Task<T> WithRetries<T, TState>(Func<TState, bool, Task<T>> sendDelegate, TState state, int numOfRetries)
    {
        var retryCount = 1;
        var sleepDuration = 100; // in milliseconds

        while (true)
        {
            T response = default;
            bool success = false;
            ExceptionDispatchInfo exceptionDispatchInfo = null;
            bool isFinalTry = retryCount >= numOfRetries;

            try
            {
                response = await sendDelegate(state, isFinalTry).ConfigureAwait(false);
                success = true;
            }
            catch (Exception ex)
            {
                exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);

                if (_globalSettings.DebugEnabled)
                {
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        Log.Error(ex, "An error occurred while sending data to the Intelligent Test Runner");
                        return default;
                    }
                }
            }

            // Error handling block
            if (!success)
            {
                if (isFinalTry)
                {
                    // stop retrying
                    Log.Error<int>(exceptionDispatchInfo.SourceException, "An error occurred while sending intelligent test runner data after {Retries} retries.", retryCount);
                    exceptionDispatchInfo?.Throw();
                    return default;
                }

                // Before retry delay
                bool isSocketException = false;
                Exception innerException = exceptionDispatchInfo.SourceException;

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
                    Log.Debug(exceptionDispatchInfo.SourceException, "Unable to communicate with the server");
                }

                // Execute retry delay
                await Task.Delay(sleepDuration).ConfigureAwait(false);
                retryCount++;
                sleepDuration *= 2;

                continue;
            }

            Log.Debug("Successfully sent intelligent test runner data");
            return response;
        }
    }

    private async Task<string> GetRepositoryUrlAsync()
    {
        var gitOutput = await ProcessHelpers.RunCommandAsync(new ProcessHelpers.Command("git", "config --get remote.origin.url", _workingDirectory)).ConfigureAwait(false);
        return gitOutput.Replace("\n", string.Empty);
    }

    private readonly struct DataArrayEnvelope<T>
    {
        [JsonProperty("data")]
        public readonly T[] Data;

        public DataArrayEnvelope(T[] data)
        {
            Data = data;
        }
    }

    private readonly struct DataEnvelopeWithMeta<T>
    {
        [JsonProperty("data")]
        public readonly T Data;

        [JsonProperty("meta")]
        public readonly Metadata Meta;

        public DataEnvelopeWithMeta(T data, string repositoryUrl)
        {
            Data = data;
            Meta = new Metadata(repositoryUrl);
        }
    }

    private readonly struct DataArrayEnvelopeWithMeta<T>
    {
        [JsonProperty("data")]
        public readonly T[] Data;

        [JsonProperty("meta")]
        public readonly Metadata Meta;

        public DataArrayEnvelopeWithMeta(T[] data, string repositoryUrl)
        {
            Data = data;
            Meta = new Metadata(repositoryUrl);
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

    private class CommitRequest
    {
        public CommitRequest(string id)
        {
            Id = id;
            Type = "commit";
        }

        [JsonProperty("id")]
        public string Id { get; }

        [JsonProperty("type")]
        public string Type { get; }
    }

    private class CommitResponse
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("attributes")]
        public object Attributes { get; set; }
    }
}

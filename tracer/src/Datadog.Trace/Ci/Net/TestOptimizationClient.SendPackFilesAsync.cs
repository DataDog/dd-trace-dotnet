// <copyright file="TestOptimizationClient.SendPackFilesAsync.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
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
    private const string PackFileUrlPath = "api/v2/git/repository/packfile";
    private Uri? _packFileUrl;

    public async Task<long> SendPackFilesAsync(string commitSha, string[]? commitsToInclude, string[]? commitsToExclude)
    {
        Log.Debug("TestOptimizationClient: Packing and sending delta of commits and tree objects...");

        var packFilesObject = GetObjectsPackFileFromWorkingDirectory(commitsToInclude, commitsToExclude);
        if (packFilesObject.Files.Length == 0)
        {
            return 0;
        }

        if (!EnsureRepositoryUrl())
        {
            return 0;
        }

        _packFileUrl ??= GetUriFromPath(PackFileUrlPath);

        var jsonPushedSha = JsonConvert.SerializeObject(new DataEnvelope<Data<object>>(new Data<object>(commitSha, CommitType, null), _repositoryUrl), SerializerSettings);
        Log.Debug("TestOptimizationClient: ObjPack.JSON RQ = {Json}", jsonPushedSha);
        var jsonPushedShaBytes = Encoding.UTF8.GetBytes(jsonPushedSha);

        TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsObjectsPackFiles(packFilesObject.Files.Length);
        long totalUploadSize = 0;
        foreach (var packFile in packFilesObject.Files)
        {
            if (!Directory.Exists(Path.GetDirectoryName(packFile)) || !File.Exists(packFile))
            {
                // Pack files must be sent in order, if a pack file is missing, we stop the upload of the rest of the pack files
                // Previous pack files will enrich the backend with some of the data.
                Log.Error("TestOptimizationClient: Pack file '{PackFile}' is missing, cancelling upload.", packFile);
                break;
            }

            // Send PackFile content
            Log.Information("TestOptimizationClient: Sending {PackFile}", packFile);
            try
            {
                var packFileContent = File.ReadAllBytes(packFile);
                var queryResponse = await SendRequestAsync<ObjectPackFilesCallbacks>(
                                            _packFileUrl,
                                            [
                                                new MultipartFormItem("pushedSha", MimeTypes.Json, null, new ArraySegment<byte>(jsonPushedShaBytes)),
                                                new MultipartFormItem("packfile", "application/octet-stream", null, new ArraySegment<byte>(packFileContent))
                                            ])
                                       .ConfigureAwait(false);
                if (queryResponse is not null)
                {
                    totalUploadSize += packFileContent.Length;
                }
            }
            catch (Exception ex)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPackErrors(MetricTags.CIVisibilityErrorType.Network);
                Log.Error(ex, "TestOptimizationClient: Send object pack file request failed.");
                throw;
            }

            // Delete temporal pack file
            try
            {
                File.Delete(packFile);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TestOptimizationClient: Error deleting pack file: '{PackFile}'", packFile);
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
                Log.Warning(ex, "TestOptimizationClient: Error deleting temporary folder: '{TemporaryFolder}'", packFilesObject.TemporaryFolder);
            }
        }

        Log.Information("TestOptimizationClient: Total pack file upload: {TotalUploadSize} bytes", totalUploadSize);
        return totalUploadSize;
    }

    private ObjectPackFilesResult GetObjectsPackFileFromWorkingDirectory(string[]? commitsToInclude, string[]? commitsToExclude)
    {
        Log.Debug("TestOptimizationClient: Getting objects...");
        commitsToInclude ??= [];
        commitsToExclude ??= [];
        var temporaryFolder = string.Empty;
        var temporaryPath = Path.GetTempFileName();

        var getObjectsArguments = "rev-list --objects --no-object-names --filter=blob:none --since=\"1 month ago\" HEAD " + string.Join(" ", commitsToExclude.Select(c => "^" + c)) + " " + string.Join(" ", commitsToInclude);
        var getObjectsCommand = GitCommandHelper.RunGitCommand(_workingDirectory, getObjectsArguments, MetricTags.CIVisibilityCommands.GetObjects);
        if (string.IsNullOrEmpty(getObjectsCommand?.Output))
        {
            // If not objects has been returned we skip the pack + upload.
            Log.Debug("TestOptimizationClient: No objects were returned from the git rev-list command.");
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
            Log.Debug("TestOptimizationClient: No valid objects were returned from the git rev-list command.");
            return new ObjectPackFilesResult([], temporaryFolder);
        }

        objectsOutput = string.Join("\n", lstObjectsSha) + "\n";

        Log.Debug<int>("TestOptimizationClient: Packing {NumObjects} objects...", lstObjectsSha.Count);
        var getPacksArguments = $"pack-objects --compression=9 --max-pack-size={MaxPackFileSizeInMb}m \"{temporaryPath}\"";
        var packObjectsResultCommand = GitCommandHelper.RunGitCommand(_workingDirectory, getPacksArguments, MetricTags.CIVisibilityCommands.PackObjects, objectsOutput);
        if (packObjectsResultCommand is null)
        {
            Log.Warning("TestOptimizationClient: 'git pack-objects...' command is null");
            return new ObjectPackFilesResult([], temporaryFolder);
        }

        if (packObjectsResultCommand.ExitCode != 0)
        {
            if (packObjectsResultCommand.Error.IndexOf("Cross-device", StringComparison.OrdinalIgnoreCase) != -1)
            {
                // Git can throw a cross device error if the temporal folder is in a different drive than the .git folder (eg. symbolic link)
                // to handle this edge case, we create a temporal folder inside the current folder.

                Log.Warning("TestOptimizationClient: 'git pack-objects...' returned a cross-device error, retrying using a local temporal folder.");
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
                    Log.Warning("TestOptimizationClient: 'git pack-objects...' command is null");
                    return new ObjectPackFilesResult([], temporaryFolder);
                }
            }

            if (packObjectsResultCommand.ExitCode != 0)
            {
                Log.Warning("TestOptimizationClient: 'git pack-objects...' command error: {Stderr}", packObjectsResultCommand.Error);
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
                Log.Warning("TestOptimizationClient: The file '{PackFile}' doesn't exist.", file);
            }
        }

        return new ObjectPackFilesResult(lstFiles.ToArray(), temporaryFolder);
    }

    private readonly struct ObjectPackFilesCallbacks : ICallbacks
    {
        public void OnBeforeSend()
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPack(MetricTags.CIVisibilityRequestCompressed.Uncompressed);
        }

        public void OnStatusCodeReceived(int statusCode, int responseLength)
        {
            if (TelemetryHelper.GetErrorTypeFromStatusCode(statusCode) is { } errorType)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPackErrors(errorType);
            }
        }

        public void OnError(Exception ex)
        {
            TelemetryFactory.Metrics.RecordCountCIVisibilityGitRequestsObjectsPackErrors(MetricTags.CIVisibilityErrorType.Network);
        }

        public void OnAfterSend(double totalMs)
        {
            TelemetryFactory.Metrics.RecordDistributionCIVisibilityGitRequestsObjectsPackMs(totalMs);
        }
    }

    private sealed class ObjectPackFilesResult
    {
        public ObjectPackFilesResult(string[] files, string temporaryFolder)
        {
            Files = files;
            TemporaryFolder = temporaryFolder;
        }

        public string[] Files { get; }

        public string TemporaryFolder { get; }
    }
}

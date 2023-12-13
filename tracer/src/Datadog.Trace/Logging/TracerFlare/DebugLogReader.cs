// <copyright file="DebugLogReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Datadog.Trace.Logging.TracerFlare;

internal static class DebugLogReader
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DebugLogReader));

    public static bool TryToCreateSentinelFile(string logDirectory, string flareRequestId)
    {
        // Assuming the log directory is accessible and writeable
        // If it isn't this will return false anyway, but we do an extra check, just to be sure
        try
        {
            // Using the dotnet-tracer-sentinel-*.log pattern means it gets automatically cleaned up by the log cleaning task
            var filePath = Path.Combine(logDirectory, $"dotnet-tracer-sentinel-{flareRequestId}.log");
            using var file = File.Open(filePath, FileMode.CreateNew, FileAccess.Write);

            // if we got this far, the file was created
            return true;
        }
        catch (IOException)
        {
            // expected, the file already exists
            Log.Information("Sentinel file for config {FlareRequestID} found in {LogDirectory}", flareRequestId, logDirectory);
            return false;
        }
        catch (Exception ex)
        {
            // check if the log directory exists, to see if this is an "expected" consequence
            if (Directory.Exists(logDirectory))
            {
                // strange error
                Log.Warning(ex, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}", flareRequestId, logDirectory);
            }
            else
            {
                Log.Warning(ex, "Error creating sentinel file for config {FlareRequestID} in {LogDirectory}: Directory does not exist", flareRequestId, logDirectory);
            }

            return false;
        }
    }

    public static async Task WriteDebugLogArchiveToStream(Stream writeTo, string logDirectory)
    {
        try
        {
            using var archive = new ZipArchive(writeTo, ZipArchiveMode.Create, true);

            // Only sending .log files to avoid the risk of sending files we don't want.
            // Also not recurrsing, in-case they have a weird log setup
            // Grabbing all the filenames once, as we could be creating more in the background, and don't
            // want to run into any modified enumerable issues etc
            var filesToUpload = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly);
            if (filesToUpload.Length == 0)
            {
                // no files to upload
                return;
            }

            foreach (var filePath in filesToUpload)
            {
                try
                {
                    var filename = Path.GetFileName(filePath);
                    var entry = archive.CreateEntry(filename);
                    using var entryStream = entry.Open();
                    // Have to allow FileShare.ReadWrite as the logger will already have it open for writing
                    using var file = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    await file.CopyToAsync(entryStream).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error recording file {Filename} in tracer flare zip", filePath);
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Error creating sanitized debug log stream for tracer flare");
        }
    }
}

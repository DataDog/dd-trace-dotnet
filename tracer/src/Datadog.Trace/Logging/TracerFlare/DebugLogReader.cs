// <copyright file="DebugLogReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;

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
}

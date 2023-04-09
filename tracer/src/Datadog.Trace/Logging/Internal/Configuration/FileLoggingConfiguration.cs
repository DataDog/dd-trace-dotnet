// <copyright file="FileLoggingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Logging.Internal.Configuration;

internal readonly struct FileLoggingConfiguration
{
    public readonly long MaxLogFileSizeBytes;
    public readonly string LogDirectory;
    public readonly int LogFileRetentionDays;

    public FileLoggingConfiguration(long maxLogFileSizeBytes, string logDirectory, int logFileRetentionDays)
    {
        MaxLogFileSizeBytes = maxLogFileSizeBytes;
        LogDirectory = logDirectory;
        LogFileRetentionDays = logFileRetentionDays;
    }
}

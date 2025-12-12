// <copyright file="FileLoggingConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Logging.Internal.Configuration;

internal sealed class FileLoggingConfiguration(long maxLogFileSizeBytes, string logDirectory, int logFileRetentionDays)
{
    public long MaxLogFileSizeBytes { get; } = maxLogFileSizeBytes;

    public string LogDirectory { get; } = logDirectory;

    public int LogFileRetentionDays { get; } = logFileRetentionDays;
}

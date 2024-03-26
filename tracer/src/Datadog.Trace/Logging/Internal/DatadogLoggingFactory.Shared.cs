// <copyright file="DatadogLoggingFactory.Shared.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.Internal.Configuration;

namespace Datadog.Trace.Logging;

internal static partial class DatadogLoggingFactory
{
    private const int DefaultMaxLogFileSize = 10 * 1024 * 1024;

    internal static FileLoggingConfiguration? GetFileLoggingConfiguration(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        string? logDirectory = null;
        try
        {
            logDirectory = GetLogDirectory(source, telemetry);
        }
        catch
        {
            // Do nothing when an exception is thrown for attempting to access the filesystem
        }

        if (logDirectory is null)
        {
            return null;
        }

        // get file details
        var maxLogFileSize = new ConfigurationBuilder(source, telemetry)
                            .WithKeys(ConfigurationKeys.MaxLogFileSize)
                            .GetAs(
                                 () => DefaultMaxLogFileSize,
                                 converter: x => long.TryParse(x, out var maxLogSize)
                                                     ? maxLogSize
                                                     : ParsingResult<long>.Failure(),
                                 validator: x => x >= 0);

        var logFileRetentionDays = new ConfigurationBuilder(source, telemetry)
                                  .WithKeys(ConfigurationKeys.LogFileRetentionDays)
                                  .AsInt32(32, x => x >= 0)
                                  .Value;

        return new FileLoggingConfiguration(maxLogFileSize, logDirectory, logFileRetentionDays);
    }

    // Internal for testing
    internal static string GetLogDirectory(IConfigurationTelemetry telemetry)
        => GetLogDirectory(GlobalConfigurationSource.CreateDefaultConfigurationSource(), telemetry);

    private static string GetLogDirectory(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var logDirectory = new ConfigurationBuilder(source, telemetry).WithKeys(ConfigurationKeys.LogDirectory).AsString();
        if (string.IsNullOrEmpty(logDirectory))
        {
#pragma warning disable 618 // ProfilerLogPath is deprecated but still supported
            var nativeLogFile = new ConfigurationBuilder(source, telemetry).WithKeys(ConfigurationKeys.ProfilerLogPath).AsString();
#pragma warning restore 618

            if (!string.IsNullOrEmpty(nativeLogFile))
            {
                logDirectory = Path.GetDirectoryName(nativeLogFile);
            }
        }

        return GetDefaultLogDirectory(source, telemetry, logDirectory);
    }

    private static string GetDefaultLogDirectory(IConfigurationSource source, IConfigurationTelemetry telemetry, string? logDirectory)
    {
        // This entire block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
        // because of the following API calls
        //   - Directory.Exists
        //   - Environment.GetFolderPath
        //   - Path.GetTempPath
        if (string.IsNullOrEmpty(logDirectory))
        {
#if NETFRAMEWORK
            logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
#else
            var isWindows = FrameworkDescription.Instance.IsWindows();

            if (ImmutableAzureAppServiceSettings.GetIsAzureAppService(source, telemetry))
            {
                return isWindows ? @"C:\home\LogFiles\datadog" : "/home/LogFiles/datadog";
            }
            else if (isWindows)
            {
                logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
            }
            else
            {
                // Linux or GCP Functions
                logDirectory = "/var/log/datadog/dotnet";
            }
#endif
        }

        if (!Directory.Exists(logDirectory))
        {
            try
            {
                Directory.CreateDirectory(logDirectory);
            }
            catch
            {
                // Unable to create the directory meaning that the user
                // will have to create it on their own.
                // Last effort at writing logs
                logDirectory = Path.GetTempPath();
            }
        }

        return logDirectory!;
    }
}

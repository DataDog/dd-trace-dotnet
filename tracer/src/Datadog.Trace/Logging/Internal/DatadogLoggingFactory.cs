// <copyright file="DatadogLoggingFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Logging;

internal static class DatadogLoggingFactory
{
    // By default, we don't rate limit log messages;
    private const int DefaultRateLimit = 0;
    private const int DefaultMaxLogFileSize = 10 * 1024 * 1024;

    public static DatadogLoggingConfiguration GetConfiguration(IConfigurationSource? source)
    {
        var logSinkOptions = source?.GetString(ConfigurationKeys.LogSinks)
                                   ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        FileLoggingConfiguration? fileConfig = null;
        if (logSinkOptions is null || Contains(logSinkOptions, LogSinkOptions.File))
        {
            fileConfig = GetFileLoggingConfiguration(source);
        }

        var rateLimit = source?.GetInt32(ConfigurationKeys.LogRateLimit) switch
        {
            >= 0 and { } r => r,
            _ => DefaultRateLimit,
        };

        return new DatadogLoggingConfiguration(rateLimit, fileConfig);

        static bool Contains(string?[]? array, string toMatch)
        {
            if (array is null)
            {
                return false;
            }

            foreach (var value in array)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (string.Equals(value!.Trim(), toMatch))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static IDatadogLogger? CreateFromConfiguration(
        in DatadogLoggingConfiguration config,
        DomainMetadata domainMetadata)
    {
        if (!config.File.HasValue)
        {
            // no enabled sinks
            return null;
        }

        var loggerConfiguration =
            new LoggerConfiguration()
               .Enrich.FromLogContext()
               .MinimumLevel.ControlledBy(DatadogLogging.LoggingLevelSwitch);

        if (config.File is { } fileConfig)
        {
            // Ends in a dash because of the date postfix
            var managedLogPath = Path.Combine(fileConfig.LogDirectory, $"dotnet-tracer-managed-{domainMetadata.ProcessName}-.log");

            loggerConfiguration
               .WriteTo.File(
                    managedLogPath,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Exception} {Properties}{NewLine}",
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: fileConfig.MaxLogFileSizeBytes,
                    shared: true);
        }

        try
        {
            loggerConfiguration.Enrich.WithProperty("MachineName", domainMetadata.MachineName);
            loggerConfiguration.Enrich.WithProperty("Process", $"[{domainMetadata.ProcessId} {domainMetadata.ProcessName}]");
            loggerConfiguration.Enrich.WithProperty("AppDomain", $"[{domainMetadata.AppDomainId} {domainMetadata.AppDomainName}]");
#if NETCOREAPP
            loggerConfiguration.Enrich.WithProperty("AssemblyLoadContext", System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(typeof(DatadogLogging).Assembly)?.ToString());
#endif
            loggerConfiguration.Enrich.WithProperty("TracerVersion", TracerConstants.AssemblyVersion);
        }
        catch
        {
            // At all costs, make sure the logger works when possible.
        }

        var internalLogger = loggerConfiguration.CreateLogger();

        ILogRateLimiter rateLimiter;

        try
        {
            rateLimiter = config.RateLimit == 0
                              ? new NullLogRateLimiter()
                              : new LogRateLimiter(config.RateLimit);
        }
        catch
        {
            rateLimiter = new NullLogRateLimiter();
        }

        return new DatadogSerilogLogger(internalLogger, rateLimiter);
    }

    // Internal for testing
    internal static string GetLogDirectory()
        => GetLogDirectory(GlobalConfigurationSource.CreateDefaultConfigurationSource());

    private static string GetLogDirectory(IConfigurationSource? source)
    {
        var logDirectory = source?.GetString(ConfigurationKeys.LogDirectory);
        if (string.IsNullOrEmpty(logDirectory))
        {
#pragma warning disable 618 // ProfilerLogPath is deprecated but still supported
            var nativeLogFile = source?.GetString(ConfigurationKeys.ProfilerLogPath);
#pragma warning restore 618

            if (!string.IsNullOrEmpty(nativeLogFile))
            {
                logDirectory = Path.GetDirectoryName(nativeLogFile);
            }
        }

        return GetDefaultLogDirectory(logDirectory);
    }

    private static string GetDefaultLogDirectory(string? logDirectory)
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
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
            }
            else
            {
                // Linux
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

    private static FileLoggingConfiguration? GetFileLoggingConfiguration(IConfigurationSource? source)
    {
        string? logDirectory = null;
        try
        {
            logDirectory = GetLogDirectory(source);
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
        var maxLogSizeVar = source?.GetString(ConfigurationKeys.MaxLogFileSize);
        var maxLogFileSize = long.TryParse(maxLogSizeVar, out var maxLogSize) ? maxLogSize : DefaultMaxLogFileSize;

        var logFileRetentionDays = source?.GetInt32(ConfigurationKeys.LogFileRetentionDays) switch
        {
            >= 0 and var d => d,
            _ => 32,
        };

        return new FileLoggingConfiguration(maxLogFileSize, logDirectory, logFileRetentionDays);
    }
}

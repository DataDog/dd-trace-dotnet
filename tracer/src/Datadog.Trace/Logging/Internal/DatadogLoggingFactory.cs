// <copyright file="DatadogLoggingFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.Internal;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.RuntimeMetrics;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;

namespace Datadog.Trace.Logging;

internal static class DatadogLoggingFactory
{
    // By default, we don't rate limit log messages;
    private const int DefaultRateLimit = 0;
    private const int DefaultMaxLogFileSize = 10 * 1024 * 1024;

    public static DatadogLoggingConfiguration GetConfiguration(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var logSinkOptions = new ConfigurationBuilder(source, telemetry)
                            .WithKeys(ConfigurationKeys.LogSinks)
                            .AsString()
                           ?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        FileLoggingConfiguration? fileConfig = null;
        if (logSinkOptions is null || Contains(logSinkOptions, LogSinkOptions.File))
        {
            fileConfig = GetFileLoggingConfiguration(source, telemetry);
        }

        var redactedErrorLogsConfig = GetRedactedErrorTelemetryConfiguration(source, telemetry);

        var rateLimit = new ConfigurationBuilder(source, telemetry)
                       .WithKeys(ConfigurationKeys.LogRateLimit)
                       .AsInt32(DefaultRateLimit, x => x >= 0)
                       .Value;

        return new DatadogLoggingConfiguration(rateLimit, fileConfig, redactedErrorLogsConfig);

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
        if (config is { File: null, ErrorLogging: null })
        {
            // no enabled sinks
            return null;
        }

        var loggerConfiguration =
            new LoggerConfiguration()
               .Enrich.FromLogContext()
               .MinimumLevel.ControlledBy(DatadogLogging.LoggingLevelSwitch);

        if (config.ErrorLogging is { } telemetry)
        {
            // Write error logs to the redacted log sink
            loggerConfiguration
               .WriteTo.Logger(
                    lc => lc
                         .MinimumLevel.Error()
                         .Filter.ByExcluding(log => IsExcludedMessage(log.MessageTemplate.Text))
                         .WriteTo.Sink(new RedactedErrorLogSink(telemetry.Collector)));
        }

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

        return new DatadogSerilogLogger(internalLogger, rateLimiter, config.File?.LogDirectory);
    }

    private static bool IsExcludedMessage(string messageTemplateText)
        => ReferenceEquals(messageTemplateText, Api.FailedToSendMessageTemplate)
#if NETFRAMEWORK
        || ReferenceEquals(messageTemplateText, PerformanceCountersListener.InsufficientPermissionsMessageTemplate)
#endif
    ;

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

    private static FileLoggingConfiguration? GetFileLoggingConfiguration(IConfigurationSource source, IConfigurationTelemetry telemetry)
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

    private static RedactedErrorLoggingConfiguration? GetRedactedErrorTelemetryConfiguration(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var config = new ConfigurationBuilder(source, telemetry);

        // We only check for the top-level key here, telemetry may be _indirectly_ disabled (because other keys are etc)
        // in which case the collector will be disabled later, but this is a preferable option.
        var telemetryEnabled = config.WithKeys(ConfigurationKeys.Telemetry.Enabled).AsBool(true);
        if (telemetryEnabled)
        {
            return config.WithKeys(ConfigurationKeys.Telemetry.TelemetryLogsEnabled).AsBool(true)
                       ? new RedactedErrorLoggingConfiguration(TelemetryFactory.RedactedErrorLogs) // use the global collector
                       : null;
        }

        // If telemetry is disabled
        return null;
    }
}

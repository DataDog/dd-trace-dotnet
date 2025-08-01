// <copyright file="DatadogLoggingFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Globalization;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging.Internal;
using Datadog.Trace.Logging.Internal.Configuration;
using Datadog.Trace.Logging.Internal.Sinks;
using Datadog.Trace.Logging.Internal.TextFormatters;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Filters;

namespace Datadog.Trace.Logging;

internal static class DatadogLoggingFactory
{
    // By default, we don't rate limit log messages;
    private const int DefaultRateLimit = 0;
    private const int DefaultMaxLogFileSize = 10 * 1024 * 1024;

    internal const int DefaultConsoleQueueLimit = 1024;

    public static DatadogLoggingConfiguration GetConfiguration(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var logSinkOptions = new ConfigurationBuilder(source, telemetry)
                             .WithKeys(ConfigurationKeys.LogSinks)
                             .AsString("file")
                             .Split([','], StringSplitOptions.RemoveEmptyEntries);

        var fileConfig = Contains(logSinkOptions, LogSinkOptions.File) ?
            GetFileLoggingConfiguration(source, telemetry) :
            null;

        var consoleConfig = Contains(logSinkOptions, LogSinkOptions.Console) ?
            GetConsoleLoggingConfiguration(source) :
            (ConsoleLoggingConfiguration?)null;

        var redactedErrorLogsConfig = GetRedactedErrorTelemetryConfiguration(source, telemetry);

        var rateLimit = new ConfigurationBuilder(source, telemetry)
                       .WithKeys(ConfigurationKeys.LogRateLimit)
                       .AsInt32(DefaultRateLimit, x => x >= 0)
                       .Value;

        return new DatadogLoggingConfiguration(rateLimit, redactedErrorLogsConfig, fileConfig, consoleConfig);

        static bool Contains(string[] items, string value)
        {
            foreach (var item in items)
            {
                if (item.AsSpan().Trim().Equals(value.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static ConsoleLoggingConfiguration GetConsoleLoggingConfiguration(IConfigurationSource? source)
    {
        // Since we are writing from a background thread, we can use the synchronized Console.Out
        // instead of Console.OpenStandardOutput() without blocking the main thread. By using Console.Out we honor
        // any previous call to Console.SetOut() and we don't have to worry about writing the UTF-8 BOM to the output stream
        // or mangling the output by writing to the console from multiple threads.
        return new ConsoleLoggingConfiguration(DefaultConsoleQueueLimit, Console.Out);
    }

    public static IDatadogLogger? CreateFromConfiguration(
        in DatadogLoggingConfiguration config,
        DomainMetadata domainMetadata)
    {
        if (config is { File: null, ErrorLogging: null, Console: null })
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
                         .Filter.ByExcluding(Matching.WithProperty(DatadogSerilogLogger.SkipTelemetryProperty))
                         .WriteTo.Sink(new RedactedErrorLogSink(telemetry.Collector)));
        }

        if (config.File is { } fileConfig)
        {
            var managedLogPath = Path.Combine(fileConfig.LogDirectory, $"dotnet-tracer-managed-{domainMetadata.ProcessName}-{domainMetadata.ProcessId.ToString(CultureInfo.InvariantCulture)}.log");

            loggerConfiguration
               .WriteTo.Logger(
                    lc => lc
                          .Enrich.With(new RemovePropertyEnricher(LogEventLevel.Error, DatadogSerilogLogger.SkipTelemetryProperty))
                          .WriteTo.File(
                               managedLogPath,
                               outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Exception} {Properties}{NewLine}",
                               rollingInterval: RollingInterval.Infinite, // don't do daily rolling, rely on the file size limit for rolling instead
                               rollOnFileSizeLimit: true,
                               fileSizeLimitBytes: fileConfig.MaxLogFileSizeBytes,
                               shared: true));
        }

        if (config.Console is { } consoleConfig)
        {
            loggerConfiguration
               .WriteTo.Logger(
                   lc => lc
                         .Enrich.With(new RemovePropertyEnricher(LogEventLevel.Error, DatadogSerilogLogger.SkipTelemetryProperty))
                         .WriteTo.Sink(new AsyncTextWriterSink(new SingleLineTextFormatter(), consoleConfig.TextWriter, consoleConfig.BufferSize)));
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

        return new DatadogSerilogLogger(internalLogger, rateLimiter, config.File);
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
            var isWindows = FrameworkDescription.Instance.IsWindows();

            if (ImmutableAzureAppServiceSettings.IsRunningInAzureAppServices(source, telemetry) ||
                ImmutableAzureAppServiceSettings.IsRunningInAzureFunctions(source, telemetry))
            {
                return isWindows ? @"C:\home\LogFiles\datadog" : "/home/LogFiles/datadog";
            }

            if (isWindows)
            {
                logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
            }
            else
            {
                logDirectory = "/var/log/datadog/dotnet";
            }
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

    private class RemovePropertyEnricher(LogEventLevel minLevel, string propertyName) : ILogEventEnricher
    {
        private readonly LogEventLevel _minLevel = minLevel;
        private readonly string _propertyName = propertyName;

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent.Level >= _minLevel)
            {
                logEvent.RemovePropertyIfPresent(_propertyName);
            }
        }
    }
}

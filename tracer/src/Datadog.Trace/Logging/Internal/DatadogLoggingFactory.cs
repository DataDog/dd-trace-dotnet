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
using Datadog.Trace.SourceGenerators;
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

    [TestingOnly]
    internal static string GetLogDirectory(IConfigurationTelemetry telemetry)
        => GetLogDirectory(GlobalConfigurationSource.Instance, telemetry);

    [TestingAndPrivateOnly]
    internal static string GetLogDirectory(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        // This entire block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
        // because of the following API calls
        // - Directory.Exists
        // - Directory.CreateDirectory
        // - Environment.GetFolderPath
        // - Path.GetTempPath

        // try reading from DD_TRACE_LOG_DIRECTORY
        var configurationBuilder = new ConfigurationBuilder(source, telemetry);
        var logDirectory = configurationBuilder.WithKeys(ConfigurationKeys.LogDirectory).AsString();

        if (StringUtil.IsNullOrEmpty(logDirectory))
        {
            // fallback #1: try getting the directory from DD_TRACE_LOG_PATH
            // todo, handle in phase 2 with deprecations
            // TraceLogPath is deprecated but still supported. For now, we bypass the WithKeys analyzer, but later (config registry v2) we want to pull deprecations differently as part of centralized file
#pragma warning disable DD0008, 618
            var nativeLogFile = configurationBuilder.WithKeys(ConfigurationKeys.TraceLogPath).AsString();
#pragma warning restore DD0008, 618

            if (!StringUtil.IsNullOrEmpty(nativeLogFile))
            {
                logDirectory = Path.GetDirectoryName(nativeLogFile);
            }
        }

        if (StringUtil.IsNullOrEmpty(logDirectory))
        {
            // fallback #2: use the default log directory
            logDirectory = GetDefaultLogDirectory(source, telemetry);
        }

        // try creating the directory if it doesn't exist
        if (logDirectory != null && (Directory.Exists(logDirectory) || TryCreateLogDirectory(logDirectory)))
        {
            return logDirectory;
        }

        // fallback #3: use the temp path
        return Path.GetTempPath();
    }

    [TestingAndPrivateOnly]
    internal static string GetDefaultLogDirectory(IConfigurationSource source, IConfigurationTelemetry telemetry)
    {
        var isWindows = FrameworkDescription.Instance.IsWindows();

        if (ImmutableAzureAppServiceSettings.IsRunningInAzureAppServices(source, telemetry) ||
            ImmutableAzureAppServiceSettings.IsRunningInAzureFunctions(source, telemetry))
        {
            return isWindows ? @"C:\home\LogFiles\datadog" : "/home/LogFiles/datadog";
        }

        string logDirectory;

        if (isWindows)
        {
            var programData = GetProgramDataDirectory();

            logDirectory = Path.Combine(programData, "Datadog .NET Tracer", "logs");
        }
        else
        {
            logDirectory = "/var/log/datadog/dotnet";
        }

        return logDirectory;
    }

    [TestingAndPrivateOnly]
    internal static string GetProgramDataDirectory()
    {
        // On Nano Server, this returns "", so we fall back to reading from the env var set in the base image instead
        // - https://github.com/dotnet/runtime/issues/22690
        // - https://github.com/dotnet/runtime/issues/21430
        // - https://github.com/dotnet/runtime/pull/109673
        // If _that_ fails, we just hard code it to "C:\ProgramData", which is what the native components do anyway
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        if (StringUtil.IsNullOrEmpty(programData))
        {
            // fallback #1: try reading from the env var
            programData = EnvironmentHelpersNoLogging.ProgramData();

            if (StringUtil.IsNullOrEmpty(programData))
            {
                // fallback #2: hard-coded
                programData = @"C:\ProgramData";
            }
        }

        return programData;
    }

    [TestingAndPrivateOnly]
    internal static bool TryCreateLogDirectory(string logDirectory)
    {
        try
        {
            Directory.CreateDirectory(logDirectory);
            return true;
        }
        catch
        {
            // Unable to create the directory meaning that the user
            // will have to create it on their own.
            return false;
        }
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
                                 defaultValue: new(DefaultMaxLogFileSize, DefaultMaxLogFileSize.ToString(CultureInfo.InvariantCulture)),
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

    private sealed class RemovePropertyEnricher(LogEventLevel minLevel, string propertyName) : ILogEventEnricher
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

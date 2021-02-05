using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging
{
    internal static class DatadogLogging
    {
        internal static readonly LoggingLevelSwitch LoggingLevelSwitch = new LoggingLevelSwitch(DefaultLogLevel);
        private const int DefaultLogMessageRateLimit = 60;
        private const LogEventLevel DefaultLogLevel = LogEventLevel.Information;
        private static readonly long? MaxLogFileSize = 10 * 1024 * 1024;
        private static readonly IDatadogLogger SharedLogger = null;
        private static readonly ILogger InternalLogger = null;

        static DatadogLogging()
        {
            // No-op for if we fail to construct the file logger
            var defaultRateLimiter = new LogRateLimiter(DefaultLogMessageRateLimit);
            InternalLogger =
                new LoggerConfiguration()
                   .WriteTo.Sink<NullSink>()
                   .CreateLogger();

            SharedLogger = new DatadogSerilogLogger(InternalLogger, defaultRateLimiter);

            try
            {
                if (GlobalSettings.Source.DebugEnabled)
                {
                    LoggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                }

                var maxLogSizeVar = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.MaxLogFileSize);
                if (long.TryParse(maxLogSizeVar, out var maxLogSize))
                {
                    // No verbose or debug logs
                    MaxLogFileSize = maxLogSize;
                }

                string logDirectory = null;
                try
                {
                    logDirectory = GetLogDirectory();
                }
                catch
                {
                    // Do nothing when an exception is thrown for attempting to access the filesystem
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (logDirectory == null)
                {
                    return;
                }

                // Ends in a dash because of the date postfix
                var managedLogPath = Path.Combine(logDirectory, $"dotnet-tracer-managed-{DomainMetadata.ProcessName}-.log");

                var loggerConfiguration =
                    new LoggerConfiguration()
                       .Enrich.FromLogContext()
                       .MinimumLevel.ControlledBy(LoggingLevelSwitch)
                       .WriteTo.File(
                            managedLogPath,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}",
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: MaxLogFileSize);

                try
                {
                    loggerConfiguration.Enrich.WithProperty("MachineName", DomainMetadata.MachineName);
                    loggerConfiguration.Enrich.WithProperty("Process", $"[{DomainMetadata.ProcessId} {DomainMetadata.ProcessName}]");
                    loggerConfiguration.Enrich.WithProperty("AppDomain", $"[{DomainMetadata.AppDomainId} {DomainMetadata.AppDomainName}]");
                    loggerConfiguration.Enrich.WithProperty("TracerVersion", TracerConstants.AssemblyVersion);
                }
                catch
                {
                    // At all costs, make sure the logger works when possible.
                }

                InternalLogger = loggerConfiguration.CreateLogger();
                SharedLogger = new DatadogSerilogLogger(InternalLogger, defaultRateLimiter);

                var rate = GetRateLimit();
                ILogRateLimiter rateLimiter = rate == 0
                    ? new NullLogRateLimiter()
                    : new LogRateLimiter(rate);

                SharedLogger = new DatadogSerilogLogger(InternalLogger, rateLimiter);
            }
            catch
            {
                // Don't let this exception bubble up as this logger is for debugging and is non-critical
            }
        }

        public static IDatadogLogger GetLoggerFor(Type classType)
        {
            // Tells us which types are loaded, when, and how often.
            SharedLogger.Debug($"Logger retrieved for: {classType.AssemblyQualifiedName}");
            return SharedLogger;
        }

        public static IDatadogLogger GetLoggerFor<T>()
        {
            return GetLoggerFor(typeof(T));
        }

        [Obsolete("This method is deprecated and will be removed. Use GetLoggerFor() instead. " +
            "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public static ILogger GetLogger(Type classType)
        {
            // Tells us which types are loaded, when, and how often.
            SharedLogger.Debug($"Obsolete logger retrieved for: {classType.AssemblyQualifiedName}");
            return InternalLogger;
        }

        [Obsolete("This method is deprecated and will be removed. Use GetLoggerFor() instead. " +
            "Kept for backwards compatability where there is a version mismatch between manual and automatic instrumentation")]
        public static ILogger For<T>()
        {
            return GetLogger(typeof(T));
        }

        internal static void Reset()
        {
            LoggingLevelSwitch.MinimumLevel = DefaultLogLevel;
        }

        internal static void SetLogLevel(LogEventLevel logLevel)
        {
            LoggingLevelSwitch.MinimumLevel = logLevel;
        }

        internal static void UseDefaultLevel()
        {
            SetLogLevel(DefaultLogLevel);
        }

        private static int GetRateLimit()
        {
            string rawRateLimit = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.LogRateLimit);
            if (!string.IsNullOrEmpty(rawRateLimit)
                && int.TryParse(rawRateLimit, out var rate)
                && (rate >= 0))
            {
                return rate;
            }

            return DefaultLogMessageRateLimit;
        }

        private static string GetLogDirectory()
        {
            string logDirectory = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.LogDirectory);
            if (logDirectory == null)
            {
                var nativeLogFile = EnvironmentHelpers.GetEnvironmentVariable(ConfigurationKeys.ProfilerLogPath);

                if (!string.IsNullOrEmpty(nativeLogFile))
                {
                    logDirectory = Path.GetDirectoryName(nativeLogFile);
                }
            }

            // This entire block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
            // because of the following API calls
            //   - Directory.Exists
            //   - Environment.GetFolderPath
            //   - Path.GetTempPath
            if (logDirectory == null)
            {
#if NETFRAMEWORK
                logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

            return logDirectory;
        }
    }
}

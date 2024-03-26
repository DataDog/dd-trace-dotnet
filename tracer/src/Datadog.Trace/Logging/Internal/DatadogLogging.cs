// <copyright file="DatadogLogging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.IO;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Core;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal static class DatadogLogging
    {
        internal static readonly LoggingLevelSwitch LoggingLevelSwitch = new(DefaultLogLevel);
        private const LogEventLevel DefaultLogLevel = LogEventLevel.Information;
        private static readonly IDatadogLogger SharedLogger;

        static DatadogLogging()
        {
            // Initialize the fallback logger right away
            // because some part of the code might produce logs while we initialize the actual logger
            SharedLogger = DatadogSerilogLogger.NullLogger;

            try
            {
                if (GlobalSettings.Instance.DebugEnabledInternal)
                {
                    LoggingLevelSwitch.MinimumLevel = LogEventLevel.Debug;
                }

                var config = DatadogLoggingFactory.GetConfiguration(GlobalConfigurationSource.Instance, TelemetryFactory.Config);

                if (config.File is { LogFileRetentionDays: > 0 } fileConfig)
                {
                    Task.Run(() => CleanLogFiles(fileConfig.LogFileRetentionDays, fileConfig.LogDirectory));
                }

                var domainMetadata = DomainMetadata.Instance;
                SharedLogger = DatadogLoggingFactory.CreateFromConfiguration(in config, domainMetadata) ?? SharedLogger;
            }
            catch
            {
                // Don't let this exception bubble up as this logger is for debugging and is non-critical
            }
        }

        public static IDatadogLogger GetLoggerFor(Type classType)
        {
            // Tells us which types are loaded, when, and how often.
            SharedLogger.Debug("Logger retrieved for: {AssemblyQualifiedName}", classType.AssemblyQualifiedName);
            return SharedLogger;
        }

        public static IDatadogLogger GetLoggerFor<T>()
        {
            return GetLoggerFor(typeof(T));
        }

        internal static void CloseAndFlush()
        {
            SharedLogger.CloseAndFlush();
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

        internal static void CleanLogFiles(int deleteAfter, string logsDirectory)
        {
            var date = DateTime.Now.AddDays(-deleteAfter);
            var logFormats = new[]
              {
                "dotnet-tracer-*.log",
                "dotnet-native-loader-*.log",
                "DD-DotNet-Profiler-Native-*.log"
              };

            try
            {
                foreach (var logFormat in logFormats)
                {
                    foreach (var logFile in Directory.EnumerateFiles(logsDirectory, logFormat))
                    {
                        if (File.GetLastWriteTime(logFile) < date)
                        {
                            File.Delete(logFile);
                        }
                    }
                }

                CleanInstrumentationVerificationLogFiles(logsDirectory, date);
            }
            catch
            {
                // Abort on first catch when doing IO operation for performance reasons.
            }
        }

        private static void CleanInstrumentationVerificationLogFiles(string logsDirectory, DateTime date)
        {
            var instrumentationVerificationLogs = Path.Combine(logsDirectory, "InstrumentationVerification");
            if (!Directory.Exists(instrumentationVerificationLogs))
            {
                return;
            }

            foreach (var dir in Directory.EnumerateDirectories(instrumentationVerificationLogs))
            {
                if (Directory.GetLastWriteTime(dir) < date)
                {
                    Directory.Delete(dir);
                }
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging
{
    internal static class DatadogLogging
    {
        private const string NixDefaultDirectory = "/var/log/datadog/dotnet";

        private static readonly long? MaxLogFileSize = 10 * 1024 * 1024;
        private static readonly LogEventLevel MinimumLogEventLevel = LogEventLevel.Information;

        private static readonly ConcurrentQueue<Action<ILogger>> ActionsToRunWhenLoggerReady = new ConcurrentQueue<Action<ILogger>>();

        private static readonly ILogger SharedLogger = null;
        private static readonly bool Initialized = false;

        static DatadogLogging()
        {
            // No-op for if we fail to construct the file logger
            SharedLogger =
                new LoggerConfiguration()
                   .WriteTo.Sink<NullSink>()
                   .CreateLogger();
            try
            {
                // We use environment variables and not the tracer settings to avoid a startup race condition between the logger and the tracer.
                var ddTraceDebugValue = Environment.GetEnvironmentVariable(ConfigurationKeys.DebugEnabled);
                if (ddTraceDebugValue?.ToBoolean() == true)
                {
                    MinimumLogEventLevel = LogEventLevel.Verbose;
                }

                var maxLogSizeVar = Environment.GetEnvironmentVariable(ConfigurationKeys.MaxLogFileSize);
                if (long.TryParse(maxLogSizeVar, out var maxLogSize))
                {
                    // No verbose or debug logs
                    MaxLogFileSize = maxLogSize;
                }

                var logDirectory = GetLogDirectory();

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (logDirectory == null)
                {
                    return;
                }

                var currentProcess = Process.GetCurrentProcess();
                // Ends in a dash because of the date postfix
                var managedLogPath = Path.Combine(logDirectory, $"dotnet-tracer-{currentProcess.ProcessName}-.log");

                var loggerConfiguration =
                    new LoggerConfiguration()
                       .Enrich.FromLogContext()
                       .MinimumLevel.Is(MinimumLogEventLevel)
                       .WriteTo.File(
                            managedLogPath,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}",
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: MaxLogFileSize);

                try
                {
                    var currentAppDomain = AppDomain.CurrentDomain;
                    loggerConfiguration.Enrich.WithProperty("MachineName", currentProcess.MachineName);
                    loggerConfiguration.Enrich.WithProperty("ProcessName", currentProcess.ProcessName);
                    loggerConfiguration.Enrich.WithProperty("PID", currentProcess.Id);
                    loggerConfiguration.Enrich.WithProperty("AppDomainName", currentAppDomain.FriendlyName);
                }
                catch
                {
                    // At all costs, make sure the logger works when possible.
                }

                SharedLogger = loggerConfiguration.CreateLogger();

                // Use to immediately execute any startup logs
                Initialized = true;
            }
            catch
            {
                // If for some reason the logger initialization fails, don't let the queue fill
                Initialized = true;
                // nothing else to do here
            }
            finally
            {
                // Log some information to correspond with the app domain
                SharedLogger.Information(FrameworkDescription.Create().ToString());

                // Clear the queue out regardless of exception
                while (ActionsToRunWhenLoggerReady.TryDequeue(out var logAction))
                {
                    try
                    {
                        logAction(SharedLogger);
                    }
                    catch (Exception ex)
                    {
                        SharedLogger.Error(ex, "Failure on logger startup subscriber");
                    }
                }
            }
        }

        public static void RegisterStartupLog(Action<ILogger> logAction)
        {
            try
            {
                if (Initialized)
                {
                    logAction(SharedLogger);
                }
                else
                {
                    ActionsToRunWhenLoggerReady.Enqueue(logAction);
                }
            }
            catch
            {
                // ignored
            }
        }

        public static ILogger GetLogger(Type classType)
        {
            // Tells us which types are loaded, when, and how often.
            SharedLogger.Debug($"Logger retrieved for: {classType.AssemblyQualifiedName}");
            return SharedLogger;
        }

        public static ILogger For<T>()
        {
            return GetLogger(typeof(T));
        }

        private static string GetLogDirectory()
        {
            var nativeLogFile = Environment.GetEnvironmentVariable(ConfigurationKeys.ProfilerLogPath);
            string logDirectory = null;

            if (!string.IsNullOrEmpty(nativeLogFile))
            {
                logDirectory = Path.GetDirectoryName(nativeLogFile);
            }

            // This entire block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
            // because of the following API calls
            //   - Directory.Exists
            //   - Environment.GetFolderPath
            //   - Path.GetTempPath
            if (logDirectory == null)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var windowsDefaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
                    if (Directory.Exists(windowsDefaultDirectory))
                    {
                        logDirectory = windowsDefaultDirectory;
                    }
                }
                else
                {
                    // either Linux or OS X
                    if (Directory.Exists(NixDefaultDirectory))
                    {
                        logDirectory = NixDefaultDirectory;
                    }
                }
            }

            if (logDirectory == null)
            {
                // Last effort at writing logs
                logDirectory = Path.GetTempPath();
            }

            return logDirectory;
        }
    }
}

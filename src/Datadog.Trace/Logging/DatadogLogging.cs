using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Serilog;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.Serilog.Sinks.File;

namespace Datadog.Trace.Logging
{
    internal static class DatadogLogging
    {
        private const string NixDefaultDirectory = "/var/log/datadog/";
        private static readonly string WindowsDefaultDirectory;

        private static readonly long? MaxLogFileSize = 10 * 1024 * 1024;
        private static readonly LogEventLevel MinimumLogEventLevel = LogEventLevel.Warning;

        private static readonly ILogger SharedLogger = null;

        static DatadogLogging()
        {
            // No-op for if we fail to construct the file logger
            SharedLogger =
                new LoggerConfiguration()
                   .WriteTo.Sink<NullSink>()
                   .CreateLogger();
            try
            {
                var currentAppDomain = AppDomain.CurrentDomain;
                var currentProcess = Process.GetCurrentProcess();

                if (Tracer.Instance?.Settings.DebugEnabled == true)
                {
                    MinimumLogEventLevel = LogEventLevel.Verbose;
                }

                var maxLogSizeVar = Environment.GetEnvironmentVariable(ConfigurationKeys.MaxLogFileSize);
                if (long.TryParse(maxLogSizeVar, out var maxLogSize))
                {
                    // No verbose or debug logs
                    MaxLogFileSize = maxLogSize;
                }

                var nativeLogFile = Environment.GetEnvironmentVariable(ConfigurationKeys.ProfilerLogPath);
                string logDirectory = null;

                if (!string.IsNullOrEmpty(nativeLogFile))
                {
                    logDirectory = Path.GetDirectoryName(nativeLogFile);
                }

                // This entire block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission because of the following API calls
                //   - Directory.Exists
                //   - Environment.GetFolderPath
                //   - Path.GetTempPath
                if (logDirectory == null)
                {
                    WindowsDefaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
                    if (Directory.Exists(WindowsDefaultDirectory))
                    {
                        logDirectory = WindowsDefaultDirectory;
                    }
                    else if (Directory.Exists(NixDefaultDirectory))
                    {
                        logDirectory = NixDefaultDirectory;
                    }
                    else
                    {
                        // Last effort at writing logs
                        logDirectory = Path.GetTempPath();
                    }
                }

                if (logDirectory == null)
                {
                    return;
                }

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

                // Log some information to correspond with the app domain
                SharedLogger.Debug(
                    "OSArchitecture: {0}, OSDescription: {1}, ProcessArchitecture: {2}, FrameworkDescription: {3}",
                    RuntimeInformation.OSArchitecture,
                    RuntimeInformation.OSDescription,
                    RuntimeInformation.ProcessArchitecture,
                    RuntimeInformation.FrameworkDescription);
            }
            catch
            {
                // nothing to do here
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
    }
}

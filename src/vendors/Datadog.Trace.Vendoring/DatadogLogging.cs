using System;
using System.IO;
using Datadog.Trace.Vendoring.Serilog;
using Datadog.Trace.Vendoring.Serilog.Events;
using Datadog.Trace.Vendoring.Serilog.Sinks.File;

namespace Datadog.Trace.Vendoring
{
    /// <summary>
    /// Class for Datadog managed file logging.
    /// </summary>
    public static class DatadogLogging
    {
        private const string WindowsDefaultDirectory = "C:\\ProgramData\\Datadog .NET Tracer\\logs\\";
        private const string NixDefaultDirectory = "/var/log/datadog/";

        private static readonly System.Diagnostics.Process CurrentProcess;
        private static readonly AppDomain CurrentAppDomain;
        private static readonly LogEventLevel MinimumLogEventLevel = LogEventLevel.Verbose; // Lowest level
        private static readonly string ManagedLogPath;
        private static readonly ILogger NoOpLogger;

        static DatadogLogging()
        {
            CurrentAppDomain = AppDomain.CurrentDomain;
            CurrentProcess = System.Diagnostics.Process.GetCurrentProcess();

            var debugEnabledVariable = Environment.GetEnvironmentVariable("DD_TRACE_DEBUG")?.ToLower();
            if (debugEnabledVariable != "1" && debugEnabledVariable != "true")
            {
                // No verbose or debug logs
                MinimumLogEventLevel = LogEventLevel.Information;
            }

            var nativeLogFile = Environment.GetEnvironmentVariable("DD_TRACE_LOG_PATH");
            string logDirectory = null;

            if (!string.IsNullOrEmpty(nativeLogFile))
            {
                logDirectory = Path.GetDirectoryName(nativeLogFile);
            }
            
            if (logDirectory == null)
            {
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
                    logDirectory = Environment.CurrentDirectory;
                }
            }

            // Ends in a dash because of the date postfix
            ManagedLogPath = Path.Combine(logDirectory, $"dotnet-tracer-{CurrentProcess.ProcessName}-.log");

            // No-op for if we fail to construct the file logger
            NoOpLogger =
                new LoggerConfiguration()
                   .WriteTo.Sink<NullSink>()
                   .CreateLogger();
        }

        /// <summary>
        /// Get Datadog logger to write to files from managed code.
        /// </summary>
        /// <param name="classType"> The class which owns this instance of the logger. </param>
        public static ILogger GetLogger(Type classType)
        {
            try
            {
                var loggerConfiguration =
                    new LoggerConfiguration()
                       .Enrich.FromLogContext()
                       .MinimumLevel.Is(MinimumLogEventLevel)
                       .WriteTo.File(
                            ManagedLogPath,
                            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}{Properties}{NewLine}",
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,  
                            fileSizeLimitBytes: 10 * 1024 * 1024); // TODO: Figure out good size and make this configurable


                var enrichedMetadata = false;
                try
                {
                    loggerConfiguration.Enrich.WithProperty("OwningType", classType.AssemblyQualifiedName);
                    loggerConfiguration.Enrich.WithProperty("MachineName", CurrentProcess.MachineName);
                    loggerConfiguration.Enrich.WithProperty("ProcessName", CurrentProcess.ProcessName);
                    loggerConfiguration.Enrich.WithProperty("PID", CurrentProcess.Id);
                    loggerConfiguration.Enrich.WithProperty("AppDomainName", CurrentAppDomain.FriendlyName);
                    enrichedMetadata = true;
                }
                catch
                {
                    // At all costs, make sure the logger works when possible.
                }

                var logger = loggerConfiguration.CreateLogger();

                // Tells us which types are loaded, when, and how often.
                logger.Information(
                    enrichedMetadata
                        ? $"Logger retrieved for {classType.AssemblyQualifiedName} with application metadata."
                        : $"Logger retrieved for {classType.AssemblyQualifiedName}, but failed to populate application metadata.");

                return logger;
            }
            catch
            {
                // Not much we can do here
                return NoOpLogger;
            }
        }

        /// <summary>
        /// Get Datadog logger to write to files from managed code.
        /// </summary>
        public static ILogger For<T>()
        {
            return GetLogger(typeof(T));
        }
    }
}

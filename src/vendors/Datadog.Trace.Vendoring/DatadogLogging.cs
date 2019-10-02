using System;
using System.IO;
using System.Runtime.InteropServices;
using Datadog.Trace.Vendoring.Serilog;
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

        /// <summary>
        /// Ends in a dash because of the date postfix
        /// </summary>
        private const string FileName = "dotnet-tracer-.log";

        private static readonly string ManagedLogPath;
        private static readonly ILogger NoOpLogger;

        static DatadogLogging()
        {
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

            ManagedLogPath = Path.Combine(logDirectory, FileName);

            // No-op for if we fail to construct the file logger
            NoOpLogger =
                new LoggerConfiguration()
                   .WriteTo.Sink<NullSink>()
                   .CreateLogger();
        }

        /// <summary>
        /// Get Datadog logger to write to files from managed code.
        /// </summary>
        /// <param name="classType"> Todo. </param>
        public static ILogger GetLogger(Type classType)
        {
            try
            {
                var loggerConfiguration =
                    new LoggerConfiguration()
                       .Enrich.FromLogContext()
                       .WriteTo.File(
                            ManagedLogPath,
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: 10 * 1024 * 1024); // TODO: Figure out good size and make this configurable

                return loggerConfiguration.CreateLogger();

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

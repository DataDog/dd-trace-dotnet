using System;
using System.IO;
using Datadog.Trace.Vendoring.Serilog;
using Datadog.Trace.Vendoring.Serilog.Sinks.File;

namespace Datadog.Trace.Vendoring
{
    /// <summary>
    /// Class for Datadog managed file logging.
    /// </summary>
    public static class DatadogLogging
    {
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
                logDirectory = Environment.CurrentDirectory;
            }

            ManagedLogPath = Path.Combine(logDirectory, "dotnet-tracer.log");

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

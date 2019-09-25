using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Sinks.File;

namespace Datadog.Trace.Vendoring
{
    /// <summary>
    /// Helper class for Datadog managed file logging.
    /// </summary>
    public static class SerilogFileLogger
    {
        /// <summary>
        /// Dedicated Datadog logger to write to files from managed code.
        /// </summary>
        public static readonly Logger Instance;

        /// <summary>
        /// Flag indicating whether the Instance has been initialized.
        /// </summary>
        public static readonly bool Initialized;

        static SerilogFileLogger()
        {
            try
            {
                var nativeLogFile = Environment.GetEnvironmentVariable("DD_TRACE_LOG_PATH");

                if (string.IsNullOrEmpty(nativeLogFile))
                {
                    return;
                }

                var logsDirectory = Path.GetDirectoryName(nativeLogFile) ?? Environment.CurrentDirectory;

                var managedLogFile = Path.Combine(logsDirectory, "dotnet-tracer.log");

                Instance =
                    new LoggerConfiguration()
                       .WriteTo.File(
                            managedLogFile,
                            rollingInterval: RollingInterval.Day,
                            rollOnFileSizeLimit: true,
                            fileSizeLimitBytes: 10 * 1024 * 1024) // TODO: Figure out good size and make this configurable
                       .CreateLogger();

                Initialized = true;
            }
            finally
            {
                if (!Initialized)
                {
                    // No-op if we fail to construct the file logger
                    Instance =
                        new LoggerConfiguration()
                           .WriteTo.Sink<NullSink>()
                           .CreateLogger();
                }
            }
        }
    }
}

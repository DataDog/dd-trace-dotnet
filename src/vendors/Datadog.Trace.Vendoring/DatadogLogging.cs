using System;
using System.IO;
using Datadog.Trace.Vendoring.Serilog;
using Datadog.Trace.Vendoring.Serilog.Core;
using Datadog.Trace.Vendoring.Serilog.Sinks.File;

namespace Datadog.Trace.Vendoring
{
    /// <summary>
    /// Class for Datadog managed file logging.
    /// </summary>
    public static class DatadogLogging
    {
        /// <summary>
        /// Flag indicating whether the Instance has been initialized.
        /// </summary>
        public static readonly bool Initialized;

        private static readonly LoggerConfiguration LoggerConfiguration;
        private static readonly object InitLock = new object();

        static DatadogLogging()
        {
            lock (InitLock)
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

                    LoggerConfiguration =
                        new LoggerConfiguration()
                           .Enrich.FromLogContext()
                           .WriteTo.File(
                                managedLogFile,
                                rollingInterval: RollingInterval.Day,
                                rollOnFileSizeLimit: true,
                                fileSizeLimitBytes: 10 * 1024 * 1024); // TODO: Figure out good size and make this configurable

                    Initialized = true;
                }
                finally
                {
                    if (!Initialized)
                    {
                        // No-op if we fail to construct the file logger
                        LoggerConfiguration =
                            new LoggerConfiguration()
                               .WriteTo.Sink<NullSink>();
                    }
                }
            }
        }

        /// <summary>
        /// Get Datadog logger to write to files from managed code.
        /// </summary>
        /// <param name="classType"> Todo. </param>
        public static Logger GetLogger(Type classType)
        {
            return LoggerConfiguration.CreateLogger();
        }

        /// <summary>
        /// Get Datadog logger to write to files from managed code.
        /// </summary>
        public static Logger For<T>()
        {
            return LoggerConfiguration.CreateLogger();
        }
    }
}

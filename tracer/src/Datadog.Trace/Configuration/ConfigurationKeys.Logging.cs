// <copyright file="ConfigurationKeys.Logging.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.Configuration;

internal static partial class ConfigurationKeys
{
        /// <summary>
        /// Configuration key for enabling or disabling the diagnostic log at startup
        /// </summary>
        /// <seealso cref="TracerSettings.StartupDiagnosticLogEnabled"/>
        public const string StartupDiagnosticLogEnabled = "DD_TRACE_STARTUP_LOGS";

        /// <summary>
        /// Configuration key for setting the approximate maximum size,
        /// in bytes, for Tracer log files.
        /// Default value is 10 MB.
        /// </summary>
        public const string MaxLogFileSize = "DD_MAX_LOGFILE_SIZE";

        /// <summary>
        /// Configuration key for setting the number of seconds between,
        /// identical log messages, for Tracer log files.
        /// Default value is 0 and setting to 0 disables rate limiting.
        /// </summary>
        public const string LogRateLimit = "DD_TRACE_LOGGING_RATE";

        /// <summary>
        /// Configuration key for setting the path to the .NET Tracer native log file.
        /// This also determines the output folder of the .NET Tracer managed log files.
        /// Overridden by <see cref="LogDirectory"/> if present.
        /// </summary>
        [Obsolete(DeprecationMessages.LogPath)]
        public const string ProfilerLogPath = "DD_TRACE_LOG_PATH";

        /// <summary>
        /// Configuration key for setting the directory of the .NET Tracer logs.
        /// Overrides the value in <see cref="ProfilerLogPath"/> if present.
        /// Default value is "%ProgramData%"\Datadog .NET Tracer\logs\" on Windows
        /// or "/var/log/datadog/dotnet/" on Linux.
        /// </summary>
        public const string LogDirectory = "DD_TRACE_LOG_DIRECTORY";

        /// <summary>
        /// Configuration key for setting in number of days when to delete log files based on their last writetime date.
        /// </summary>
        public const string LogFileRetentionDays = "DD_TRACE_LOGFILE_RETENTION_DAYS";

        /// <summary>
        /// Configuration key for locations to write internal diagnostic logs.
        /// Currently only <c>file</c> is supported
        /// Defaults to <c>file</c>
        /// </summary>
        public const string LogSinks = "DD_TRACE_LOG_SINKS";
}

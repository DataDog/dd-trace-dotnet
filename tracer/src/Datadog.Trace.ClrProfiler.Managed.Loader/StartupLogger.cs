// <copyright file="StartupLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    internal static class StartupLogger
    {
        private const string NixDefaultDirectory = "/var/log/datadog/dotnet";

        private static readonly bool DebugEnabled = IsDebugEnabled();
        private static readonly string LogDirectory = GetLogDirectory();
        private static readonly string StartupLogFilePath = SetStartupLogFilePath();

        public static void Log(string message, params object[] args)
        {
            try
            {
                if (StartupLogFilePath != null)
                {
                    try
                    {
                        using (var fileSink = new FileSink(StartupLogFilePath))
                        {
                            fileSink.Info($"[{DateTime.UtcNow}] {message}{Environment.NewLine}", args);
                        }

                        return;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                Console.Error.WriteLine(message, args);
            }
            catch
            {
                // ignore
            }
        }

        public static void Log(Exception ex, string message, params object[] args)
        {
            message = $"{message}{Environment.NewLine}{ex}";
            Log(message, args);
        }

        public static void Debug(string message, params object[] args)
        {
            if (DebugEnabled)
            {
                Log(message, args);
            }
        }

        private static string GetLogDirectory()
        {
            string logDirectory = null;

            try
            {
                logDirectory = Environment.GetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY");

                if (logDirectory == null)
                {
                    var nativeLogFile = Environment.GetEnvironmentVariable("DD_TRACE_LOG_PATH");

                    if (!string.IsNullOrEmpty(nativeLogFile))
                    {
                        logDirectory = Path.GetDirectoryName(nativeLogFile);
                    }
                }

                if (logDirectory == null)
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        var windowsDefaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
                        logDirectory = windowsDefaultDirectory;
                    }
                    else
                    {
                        // Linux
                        logDirectory = NixDefaultDirectory;
                    }
                }

                logDirectory = CreateDirectoryIfMissing(logDirectory) ?? Path.GetTempPath();
            }
            catch
            {
                // The try block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
                // because of the following API calls
                //   - Directory.Exists
                //   - Environment.GetFolderPath
                //   - Path.GetTempPath

                // Unsafe to log
                logDirectory = null;
            }

            return logDirectory;
        }

        private static string CreateDirectoryIfMissing(string pathToCreate)
        {
            try
            {
                Directory.CreateDirectory(pathToCreate);
                return pathToCreate;
            }
            catch
            {
                // Unable to create the directory meaning that the user will have to create it on their own.
                // It is unsafe to log here, so return null to defer deciding what the path is
                return null;
            }
        }

        private static string SetStartupLogFilePath()
        {
            if (LogDirectory == null)
            {
                return null;
            }

            try
            {
                var process = Process.GetCurrentProcess();
                // Do our best to not block other processes on write
                return Path.Combine(LogDirectory, $"dotnet-tracer-loader-{process.ProcessName}-{process.Id}.log");
            }
            catch
            {
                // We can't get the process info
                return Path.Combine(LogDirectory, "dotnet-tracer-loader.log");
            }
        }

        private static bool IsDebugEnabled()
        {
            try
            {
                var ddTraceDebugValue = Environment.GetEnvironmentVariable("DD_TRACE_DEBUG");

                if (ddTraceDebugValue == null)
                {
                    return false;
                }

                switch (ddTraceDebugValue.ToUpperInvariant())
                {
                    case "TRUE":
                    case "YES":
                    case "Y":
                    case "T":
                    case "1":
                        return true;
                    default:
                        return false;
                }
            }
            catch
            {
                // Default to not enabled
                return false;
            }
        }
    }
}

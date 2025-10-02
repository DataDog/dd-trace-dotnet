// <copyright file="StartupLogger.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics;
using System.IO;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    internal static class StartupLogger
    {
        private static readonly bool DebugEnabled;
        private static readonly string? StartupLogFilePath;
        private static readonly object PadLock = new();

        private static bool _loggingEnabled;

        static StartupLogger()
        {
            var envVars = new EnvironmentVariableProvider(logErrors: false);
            DebugEnabled = envVars.GetBooleanEnvironmentVariable("DD_TRACE_DEBUG") ?? false;

            try
            {
                // check DD_TRACE_LOG_DIRECTORY first, then DD_TRACE_LOG_PATH,
                // otherwise fallback to the default log directory for the current platform
                var logDirectory = GetLogDirectoryFromEnvVars(envVars) ?? GetDefaultLogDirectory(envVars);
                Directory.CreateDirectory(logDirectory);
                StartupLogFilePath = ComputeStartupLogFilePath(logDirectory);
                _loggingEnabled = true;
            }
            catch
            {
                // The try block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
                // because of the following API calls
                //   - Directory.Exists
                //   - Environment.GetFolderPath
                //   - Path.GetTempPath
                StartupLogFilePath = null;
                _loggingEnabled = false;
            }
        }

        public static void Log(string message)
        {
            if (_loggingEnabled)
            {
                Log(message, args: []);
            }
        }

        public static void Log(string message, object? arg0)
        {
            if (_loggingEnabled)
            {
                Log(message, [arg0]);
            }
        }

        public static void Log(string message, object? arg0, object? arg1)
        {
            if (_loggingEnabled)
            {
                Log(message, [arg0, arg1]);
            }
        }

        public static void Log(string message, object? arg0, object? arg1, object? arg2)
        {
            if (_loggingEnabled)
            {
                Log(message, [arg0, arg1, arg2]);
            }
        }

        private static void Log(string message, object?[] args)
        {
            if (StartupLogFilePath == null)
            {
                return;
            }

            try
            {
                lock (PadLock)
                {
                    using var fileSink = new FileSink(StartupLogFilePath);

                    if (DebugEnabled)
                    {
                        var currentDomain = AppDomain.CurrentDomain;
                        var isDefaultAppDomain = currentDomain.IsDefaultAppDomain();
                        fileSink.Info($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}|{currentDomain.Id}|{currentDomain.FriendlyName}|{isDefaultAppDomain}] {message}{Environment.NewLine}", args);
                    }
                    else
                    {
                        fileSink.Info($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] {message}{Environment.NewLine}", args);
                    }
                }
            }
            catch
            {
                // ignore exceptions (nowhere to log) and disable logging
                _loggingEnabled = false;
            }
        }

        public static void Log(Exception ex, string message)
        {
            if (_loggingEnabled)
            {
                Log("{0}{1}{2}", [message, Environment.NewLine, ex]);
            }
        }

        public static void Log(Exception ex, string message, object? arg0)
        {
            if (_loggingEnabled)
            {
                var formattedMessage = string.Format(message, arg0);
                Log("{0}{1}{2}", [formattedMessage, Environment.NewLine, ex]);
            }
        }

        public static void Log(Exception ex, string message, object? arg0, object? arg1)
        {
            if (_loggingEnabled)
            {
                var formattedMessage = string.Format(message, arg0, arg1);
                Log("{0}{1}{2}", [formattedMessage, Environment.NewLine, ex]);
            }
        }

        public static void Debug(string message)
        {
            if (_loggingEnabled && DebugEnabled)
            {
                Log(message, args: []);
            }
        }

        public static void Debug(string message, object? arg0)
        {
            if (_loggingEnabled && DebugEnabled)
            {
                Log(message, [arg0]);
            }
        }

        public static void Debug(string message, object? arg0, object? arg1)
        {
            if (_loggingEnabled && DebugEnabled)
            {
                Log(message, [arg0, arg1]);
            }
        }

        public static void Debug(string message, object? arg0, object? arg1, object? arg2)
        {
            if (_loggingEnabled && DebugEnabled)
            {
                Log(message, [arg0, arg1, arg2]);
            }
        }

        internal static string? GetLogDirectoryFromEnvVars<TEnvVars>(TEnvVars envVars)
            where TEnvVars : IEnvironmentVariableProvider
        {
            if (envVars.GetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY") is { } logDirectory)
            {
                return logDirectory;
            }

            if (envVars.GetEnvironmentVariable("DD_TRACE_LOG_PATH") is { } logFilename)
            {
                return Path.GetDirectoryName(logFilename);
            }

            return null;
        }

        internal static string GetDefaultLogDirectory<TEnvVars>(TEnvVars envVars)
            where TEnvVars : IEnvironmentVariableProvider
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var isAas = !string.IsNullOrEmpty(envVars.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

            if (isAas)
            {
                // Azure App Services and Azure Functions
                return isWindows ? @"C:\home\LogFiles\datadog" : "/home/LogFiles/datadog";
            }

            if (isWindows)
            {
                // On Nano Server, this returns "", so we fallback to reading from the env var set in the base image instead
                // - https://github.com/dotnet/runtime/issues/22690
                // - https://github.com/dotnet/runtime/issues/21430
                // - https://github.com/dotnet/runtime/pull/109673
                // If _that_ fails, we just hard code it to "C:\ProgramData", which is what the native components do anyway
                var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (string.IsNullOrEmpty(programData))
                {
                    programData = envVars.GetEnvironmentVariable("ProgramData");
                    if (string.IsNullOrEmpty(programData))
                    {
                        programData = @"C:\ProgramData";
                    }
                }

                return Path.Combine(programData, "Datadog .NET Tracer", "logs");
            }

            // not Windows
            return "/var/log/datadog/dotnet";
        }

        private static string ComputeStartupLogFilePath(string logDirectory)
        {
            try
            {
                // Do our best to not block other processes on write by using the process name and id
                using var process = Process.GetCurrentProcess();
                var fileName = string.Concat("dotnet-tracer-loader-", process.ProcessName, "-", process.Id.ToString(), ".log");
                return Path.Combine(logDirectory, fileName);
            }
            catch
            {
                // We can't get the process info, use a random guid instead
                var fileName = string.Concat("dotnet-tracer-loader-", Guid.NewGuid().ToString(), ".log");
                return Path.Combine(logDirectory, fileName);
            }
        }
    }
}

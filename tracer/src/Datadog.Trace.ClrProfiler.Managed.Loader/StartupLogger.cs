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
        private const string NixDefaultDirectory = "/var/log/datadog/dotnet";

        private static readonly bool DebugEnabled;
        private static readonly string? StartupLogFilePath;
        private static readonly object PadLock = new();

        static StartupLogger()
        {
            var envVars = new EnvironmentVariableProvider(false);

            DebugEnabled = envVars.GetBooleanEnvironmentVariable("DD_TRACE_DEBUG", false);

            var logDirectory = GetLogDirectory(envVars);
            StartupLogFilePath = SetStartupLogFilePath(logDirectory);
        }

        public static void Log(string message, params object?[] args)
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
                // ignore
            }
        }

        public static void Log(Exception ex, string message, params object?[] args)
        {
            Log($"{message}{Environment.NewLine}{ex}", args);
        }

        public static void Debug(string message, params object?[] args)
        {
            if (DebugEnabled)
            {
                Log(message, args);
            }
        }

        internal static string? GetLogDirectory(IEnvironmentVariableProvider envVars)
        {
            string? logDirectory;

            try
            {
                logDirectory = envVars.GetEnvironmentVariable("DD_TRACE_LOG_DIRECTORY");

                if (logDirectory == null)
                {
                    var nativeLogFile = envVars.GetEnvironmentVariable("DD_TRACE_LOG_PATH");

                    if (!string.IsNullOrEmpty(nativeLogFile))
                    {
                        logDirectory = Path.GetDirectoryName(nativeLogFile);
                    }
                }

                if (logDirectory == null)
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        // On Nano Server, this returns "", so we fallback to reading from the env var set in the base image instead
                        // - https://github.com/dotnet/runtime/issues/22690
                        // - https://github.com/dotnet/runtime/issues/21430
                        // - https://github.com/dotnet/runtime/pull/109673
                        // If _that_ fails, we just hard code it to "C:\ProgramData", which is what the native components do anyway
                        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        if (string.IsNullOrEmpty(programData))
                        {
                            programData = Environment.GetEnvironmentVariable("ProgramData");
                            if (string.IsNullOrEmpty(programData))
                            {
                                programData = @"C:\ProgramData";
                            }
                        }

                        var windowsDefaultDirectory = Path.Combine(programData, "Datadog .NET Tracer", "logs");
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

        private static string? CreateDirectoryIfMissing(string pathToCreate)
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

        private static string? SetStartupLogFilePath(string? logDirectory)
        {
            if (logDirectory == null)
            {
                return null;
            }

            try
            {
                using var process = Process.GetCurrentProcess();
                // Do our best to not block other processes on write
                return Path.Combine(logDirectory, $"dotnet-tracer-loader-{process.ProcessName}-{process.Id}.log");
            }
            catch
            {
                // We can't get the process info
                return Path.Combine(logDirectory, "dotnet-tracer-loader.log");
            }
        }
    }
}

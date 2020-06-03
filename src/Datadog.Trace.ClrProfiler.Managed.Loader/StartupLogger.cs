using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.Managed.Loader
{
    internal static class StartupLogger
    {
        private const string NixDefaultDirectory = "/var/log/datadog/dotnet";

        private static readonly string LogDirectory = GetLogDirectory();
        private static readonly string StartupLogFilePath = Path.Combine(LogDirectory, "dotnet-tracer-loader.log");

        public static void Log(string message, params object[] args)
        {
            try
            {
                using (var fileSink = new FileSink(StartupLogFilePath))
                {
                    fileSink.Info($"[{DateTime.UtcNow}] {message}{Environment.NewLine}", args);
                }
            }
            catch
            {
                try
                {
                    Console.WriteLine(message, args);
                }
                catch
                {
                    // ignore
                }
            }
        }

        public static void Log(Exception ex, string message, params object[] args)
        {
            message = $"{message}{Environment.NewLine}{ex}";
            Log(message, args);
        }

        private static string GetLogDirectory()
        {
            string logDirectory = null;

            try
            {
                var nativeLogFile = Environment.GetEnvironmentVariable("DD_TRACE_LOG_PATH");

                if (!string.IsNullOrEmpty(nativeLogFile))
                {
                    logDirectory = Path.GetDirectoryName(nativeLogFile);
                }

                if (logDirectory == null)
                {
                    var windowsDefaultDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Datadog .NET Tracer", "logs");
                    if (Directory.Exists(windowsDefaultDirectory))
                    {
                        logDirectory = windowsDefaultDirectory;
                    }
                    else
                    {
                        // either Linux or OS X
                        if (Directory.Exists(NixDefaultDirectory))
                        {
                            logDirectory = NixDefaultDirectory;
                        }
                        else
                        {
                            try
                            {
                                Directory.CreateDirectory(NixDefaultDirectory);
                                logDirectory = NixDefaultDirectory;
                            }
                            catch
                            {
                                // Unable to create the directory meaning that the user
                                // will have to create it on their own.
                            }
                        }
                    }
                }

                if (logDirectory == null)
                {
                    // Last effort at writing logs
                    logDirectory = Path.GetTempPath();
                }
            }
            catch
            {
                // The try block may throw a SecurityException if not granted the System.Security.Permissions.FileIOPermission
                // because of the following API calls
                //   - Directory.Exists
                //   - Environment.GetFolderPath
                //   - Path.GetTempPath
            }

            return logDirectory;
        }
    }
}

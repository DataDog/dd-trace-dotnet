// <copyright file="LocalFilesProfilesExporter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using Datadog.Configuration;
using Datadog.Logging.Emission;
using Datadog.PProf.Export;
using Datadog.Util;

namespace Datadog.Profiler
{
    internal class LocalFilesProfilesExporter
    {
        private const string DirectoryFallbackRoot = "Datadog-APM";
        private const string DirectoryFallbackSub = "PProf-Files";

        private const string TimestampTag = "{TS}";
        private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss_fff";

        private const bool ApplyGZip = true;
        private const CompressionLevel GZipCompressionLevel = CompressionLevel.Optimal;

        private readonly bool _isEnabled;
        private readonly string _appName;
        private readonly string _outputDirectory;

        private Random _random = null;

        public LocalFilesProfilesExporter(IProductConfiguration config)
        {
            _appName = GetSanitizedAppName(config);

            _outputDirectory = GetOutputDirectory(config, _appName);

            _isEnabled = _outputDirectory != null;

            Log.Info(LogSource.Info, "Initialized", "IsEnabled", _isEnabled, "AppName", _appName, "OutputDirectory", _outputDirectory);
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
        }

        public bool ExportProfiles(PProfBuildSession pprofBuildSession)
        {
            if (_isEnabled)
            {
                return ExportProfiles(pprofBuildSession, _outputDirectory);
            }
            else
            {
                return false;
            }
        }

        private static string GetSanitizedAppName(IProductConfiguration config)
        {
            string unsanitizedAppName = config.DDDataTags_Service;

            if (string.IsNullOrWhiteSpace(unsanitizedAppName))
            {
                try
                {
                    unsanitizedAppName = CurrentProcess.GetName();
                }
                catch
                {
                    unsanitizedAppName = "UnknownApp";
                }
            }

            // Sanitize app name:

            var appName = new StringBuilder(unsanitizedAppName);

            // Note that the 'appName' is likely to be based on the service name tag setting, which, in turn,
            // is likely to be based on the ASP.NET site name.
            // Common "unwished" characters that come up in site names, such as '/', '\\', ':' and others are
            // contained in the invalid chars arrays below, especially in 'invalidFilenameChars'.
            char[] invalidPathChars = Path.GetInvalidPathChars();
            char[] invalidFilenameChars = Path.GetInvalidFileNameChars();

            for (int p = 0; p < appName.Length; p++)
            {
                char c = appName[p];
                bool isInvalid = char.IsWhiteSpace(c) || c == '.';

                for (int i = 0; i < invalidPathChars.Length && !isInvalid; i++)
                {
                    isInvalid = (c == invalidPathChars[i]);
                }

                for (int i = 0; i < invalidFilenameChars.Length && !isInvalid; i++)
                {
                    isInvalid = (c == invalidFilenameChars[i]);
                }

                if (isInvalid)
                {
                    appName[p] = '_';
                }
            }

            return appName.ToString();
        }

        private string GetOutputDirectory(IProductConfiguration config, string appName)
        {
            if (string.IsNullOrWhiteSpace(config.ProfilesExport_LocalFiles_Directory))
            {
                return null;
            }

            string outDir;

            // First, try with the defined directory
            try
            {
                if (BuildAndValidateOutputDirectory(config.ProfilesExport_LocalFiles_Directory, appName, isLastResort: false, out outDir))
                {
                    return outDir;
                }
            }
            catch (Exception ex)
            {
                // Probably did not have permissions to GetCurrentDirectory.
                Log.Error(LogSource.Info.WithCallInfo(), ex);
            }

            // Otherwise, save them in a temp folder
            try
            {
                string tempDir = Path.GetTempPath();
                string outBaseDir = Path.Combine(tempDir, DirectoryFallbackRoot, DirectoryFallbackSub);
                if (BuildAndValidateOutputDirectory(outBaseDir, appName, isLastResort: true, out outDir))
                {
                    return outDir;
                }
            }
            catch (Exception ex)
            {
                // Probably did not have permissions to GetTempPath.
                Log.Error(LogSource.Info.WithCallInfo(), ex);
            }

            return null;
        }

        private bool BuildAndValidateOutputDirectory(string outBaseDir, string appName, bool isLastResort, out string outDir)
        {
            outDir = Path.Combine(outBaseDir, appName);

            try
            {
                DirectoryInfo outDirInfo = Directory.CreateDirectory(outDir);
                if (!outDirInfo.Exists)
                {
                    if (isLastResort)
                    {
                        Log.Error(
                            LogSource.Info,
                            $"Cannot ensure profiles export directory '{outDir}' exists. This may be due to lacking permissions.");
                    }
                    else
                    {
                        Log.Info(
                            LogSource.Info,
                            $"Cannot ensure profiles export directory '{outDir}' exists. This may be due to lacking permissions.");
                    }

                    return false;
                }

                ExportProfiles(pprofBuildSession: null, outputDirectory: outDir);
                return true;
            }
            catch (Exception ex)
            {
                if (isLastResort)
                {
                    Log.Error(
                        LogSource.Info,
                        $"The profiles export directory '{outDir}' does not exist or it cannot be written to. This may be due to lacking permissions.",
                        "Exception",
                        ex);
                }
                else
                {
                    Log.Info(
                        LogSource.Info,
                        $"The local profiles export directory '{outDir}' does not exist or it cannot be written to. This may be due to lacking permissions.",
                        "Exception",
                        ex.ToString());
                }

                return false;
            }
        }

        private bool ExportProfiles(PProfBuildSession pprofBuildSession, string outputDirectory)
        {
            var filename = new StringBuilder("StackSamples.");
            filename.Append(_appName);
            filename.Append('.');
            filename.Append(TimestampTag);
            filename.Append(".pprof");

            Stream outFileStream = OpenTimestampedFile(outputDirectory, filename.ToString());

            if (pprofBuildSession == null)
            {
                using (outFileStream)
                {
                    using (var outw = new StreamWriter(outFileStream, Encoding.UTF8))
                    {
                        outw.WriteLine("No profiles data available at this time.");
                    }
                }
            }
            else
            {
                Stream outs = ApplyGZip
                                ? new GZipStream(outFileStream, GZipCompressionLevel)
                                : outFileStream;
                using (outs)
                {
                    pprofBuildSession.WriteProfileToStream(outs);
                }
            }

            return true;
        }

        private Stream OpenTimestampedFile(string outputDirectory, string filenameTemplate)
        {
            const int MaxRetries = 10;
            const int RetrySleepMaxMillisecs = 5;
            const int OutStreamBufferSize = 4096;

            Validate.NotNull(outputDirectory, nameof(outputDirectory));
            Validate.NotNullOrWhitespace(filenameTemplate, nameof(filenameTemplate));

            int currentTry = 0;
            while (true)
            {
                currentTry++;

                DateTimeOffset now = DateTimeOffset.Now;
                string nowStr = now.ToString(TimestampFormat);

                string path = filenameTemplate.Replace(TimestampTag, nowStr);
                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    path = Path.Combine(outputDirectory, path);
                }

                try
                {
                    var fileStream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, OutStreamBufferSize, useAsync: false);
                    return fileStream;
                }
                catch (IOException ioEx)
                {
                    if (ioEx.Message.Contains("already exists") && ioEx.Message.Contains(path))
                    {
                        if (_random == null)
                        {
                            _random = new Random();
                        }

                        int msecs = _random.Next(RetrySleepMaxMillisecs - 1) + 1;
                        Thread.Sleep(msecs);
                    }
                    else
                    {
                        if (currentTry <= MaxRetries)
                        {
                            Thread.Yield();
                        }
                        else
                        {
                            throw ioEx.Rethrow();
                        }
                    }
                }
            }
        }

        private static class LogSource
        {
            public static readonly LogSourceInfo Info = new LogSourceInfo(nameof(LocalFilesProfilesExporter));
        }
    }
}

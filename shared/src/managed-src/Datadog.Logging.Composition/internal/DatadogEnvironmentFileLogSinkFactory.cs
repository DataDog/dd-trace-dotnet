using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1308:Variable names must not be prefixed", Justification = "Should not apply to statics")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields must begin with upper-case letter", Justification = "Should only apply to vars that are logically const.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names must not contain underscore", Justification = "Underscore aid visibility in long names")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:Element return value must be documented", Justification = "That would be great.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters must be documented", Justification = "That would be great.")]
    internal static class DatadogEnvironmentFileLogSinkFactory
    {
        /// <summary>
        /// Encapsulates the information required to construct the filename base:
        ///   <c>DD-{ProductFamily}-{Product}-{ComponentGroup}-{ProcessName}</c>
        /// </summary>
        public struct FilenameBaseInfo
        {
            public static void EnsureValidAsParam(FilenameBaseInfo filenameBaseInfo)
            {
                if (!filenameBaseInfo.IsValid)
                {
                    throw new ArgumentException($"Specified {nameof(filenameBaseInfo)} is not valid."
                                              + $" Use the non-default (aka paramaterized) ctor to create valid {nameof(FilenameBaseInfo)} instances.",
                                                nameof(filenameBaseInfo));
                }
            }

            private string _logFilenameBase;

            public FilenameBaseInfo(string productFamily, string product, string componentGroup)
                : this(productFamily, product, componentGroup, GetProcessName())
            { }

            public FilenameBaseInfo(string productFamily, string product, string componentGroup, string processName)
            {
                ProductFamily = productFamily;
                Product = product;
                ComponentGroup = componentGroup;
                ProcessName = processName;
                IsValid = true;
                _logFilenameBase = null;
            }

            public bool IsValid { get; }

            public string ProductFamily { get; }
            public string Product { get; }
            public string ComponentGroup { get; }
            public string ProcessName { get; }
            public string LogFilenameBase
            {
                get
                {
                    string logFilenameBase = _logFilenameBase;
                    if (logFilenameBase == null && IsValid)
                    {
                        logFilenameBase = ConstructFilenameBase(ProductFamily, Product, ComponentGroup, ProcessName);
                        _logFilenameBase = logFilenameBase;
                    }

                    return logFilenameBase;
                }
            }
        }

        private const string FilenamePrefix = "DD-";
        private const string FilenameMissingComponentFallback = "_";
        private const char FilenameInvalidCharFallback = '_';
        private const string FilenameSeparator = "-";

        private const string WindowsDefaultLogDirectory = @"Datadog-APM\logs\";    // relative to Environment.SpecialFolder.CommonApplicationData
        private const string NixDefaultLogDirectory = @"/var/log/datadog/";        // global path

        private const string DdTraceLogDirectoryEnvVarName = "DD_TRACE_LOG_DIRECTORY";

        private static string s_processNameString = null;

        /// <summary>
        /// The name that will be constructed is:
        /// <c>DD-{ProductFamily}-{Product}-{ComponentGroup}-{ProcessName}-{Date}[_Index].log</c>
        /// </summary>
        /// <remarks>Specify an <c>index</c> smaller than zero to avoid including the index component.</remarks>
        public static string ConstructFilename(FilenameBaseInfo filenameBaseInfo, DateTimeOffset timestamp, int index)
        {
            FilenameBaseInfo.EnsureValidAsParam(filenameBaseInfo);
            return FileLogSink.ConstructFilename(filenameBaseInfo.LogFilenameBase, timestamp, index);
        }

        public static bool TryCreateNewFileLogSink(FilenameBaseInfo filenameBaseInfo, Guid logGroupId, out FileLogSink newLogSink)
        {
            return TryCreateNewFileLogSink(filenameBaseInfo,
                                           logGroupId,
                                           preferredLogFileDirectory: null,
                                           FileLogSink.RotateLogFileWhenLargerBytesDefault,
                                           FileLogSink.DefaultFormatOptions,
                                           out newLogSink);
        }

        public static bool TryCreateNewFileLogSink(FilenameBaseInfo filenameBaseInfo,
                                                   Guid logGroupId,
                                                   string preferredLogFileDirectory,
                                                   DefaultFormat.Options formatOptions,
                                                   out FileLogSink newLogSink)
        {
            return TryCreateNewFileLogSink(filenameBaseInfo,
                                           logGroupId,
                                           preferredLogFileDirectory,
                                           FileLogSink.RotateLogFileWhenLargerBytesDefault,
                                           formatOptions,
                                           out newLogSink);
        }

        /// <summary>
        /// Attention: All loggers from all processes that write to the same
        ///   (<c>productFamily</c> - <c>product</c> - <c>componentGroup</c> - <c>processName</c>)
        /// MUST use the same value for <c>rotateLogFileWhenLargerBytes</c>!
        /// </summary>
        /// <param name="filenameBaseInfo">Info required to construct the base file name.
        ///     Use the non-default (aka paramaterized) ctor to create valid <c>FilenameBaseInfo</c> instances.</param>
        /// <param name="logGroupId">A unique ID for all loggers across all processes that write to the same file (or a set of rotating files).
        ///     Used for inter-process synchronization.</param>
        /// <param name="preferredLogFileDirectory">Target folder for the log file.
        ///     Specify <c>null</c> to use default.</param>
        /// <param name="rotateLogFileWhenLargerBytes">Log files will be rotated (new index used) when the file uses the specified size.
        ///     Specify a nagative value to disable size-based file rotation.
        ///     All loggers that write to the same file must use the same value for this parameter.
        ///     Use <c>FileLogSink.RotateLogFileWhenLargerBytesDefault</c> as a default.</param>
        /// <param name="formatOptions">Formatting options for the log file.
        ///     Specify <c>null</c> to use default.</param>
        /// <param name="newLogSink">OUT parameter containing the newly created <c>FileLogSink</c>.</param>
        /// <returns><c>True</c> is a new <c>FileLogSink</c> was created and initialized, <c>False</c> otherwise.</returns>        
        public static bool TryCreateNewFileLogSink(FilenameBaseInfo filenameBaseInfo,
                                                   Guid logGroupId,
                                                   string preferredLogFileDirectory,
                                                   int rotateLogFileWhenLargerBytes,
                                                   DefaultFormat.Options formatOptions,
                                                   out FileLogSink newLogSink)
        {
            FilenameBaseInfo.EnsureValidAsParam(filenameBaseInfo);

            // If user speficied a log file directory, we try to use it. If it fails we fry to fall back to defaults.
            if (!String.IsNullOrWhiteSpace(preferredLogFileDirectory))
            {
                if (FileLogSink.TryCreateNew(preferredLogFileDirectory, filenameBaseInfo.LogFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, formatOptions, out newLogSink))
                {
                    return true;
                }
            }

            // If specified and accessible, use the Env Var for the log folder:
            {
                string userSetDdTraceLogDir = ReadEnvironmentVariable(DdTraceLogDirectoryEnvVarName);
                if (FileLogSink.TryCreateNew(userSetDdTraceLogDir, filenameBaseInfo.LogFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, formatOptions, out newLogSink))
                {
                    return true;
                }
            }

            // If accessible, use the default for the log folder:
            {
                string defaultProductFamilyLogDir;
                try
                {
                    if (FileLogSink.IsWindowsFileSystem)
                    {
                        string commonAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        defaultProductFamilyLogDir = Path.Combine(commonAppDataDir,
                                                                  WindowsDefaultLogDirectory,
                                                                  filenameBaseInfo.ProductFamily ?? FilenameMissingComponentFallback);
                    }
                    else
                    {
                        defaultProductFamilyLogDir = Path.Combine(NixDefaultLogDirectory, (filenameBaseInfo.ProductFamily ?? FilenameMissingComponentFallback).ToLower());
                    }
                }
                catch
                {
                    defaultProductFamilyLogDir = null;
                }

                if (FileLogSink.TryCreateNew(defaultProductFamilyLogDir, filenameBaseInfo.LogFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, formatOptions, out newLogSink))
                {
                    return true;
                }
            }

            // If we could not use the above folders, try using the application folder, is accessible:
            {
                string appDir;
                try
                {
                    appDir = Environment.CurrentDirectory;
                }
                catch
                {
                    appDir = null;
                }

                if (FileLogSink.TryCreateNew(appDir, filenameBaseInfo.LogFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, formatOptions, out newLogSink))
                {
                    return true;
                }
            }

            // As last resort, we will try using the temp directory:
            {
                string tempDir;
                try
                {
                    tempDir = Path.GetTempPath();
                }
                catch
                {
                    tempDir = null;
                }

                if (FileLogSink.TryCreateNew(tempDir, filenameBaseInfo.LogFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, formatOptions, out newLogSink))
                {
                    return true;
                }
            }

            // We could not write logs into any of the abofe folders. Give up.

            newLogSink = null;
            return false;
        }

        private static string ConstructFilenameBase(string productFamily, string product, string componentGroup, string processName)
        {
            var filenameBase = new StringBuilder();
            filenameBase.Append(FilenamePrefix);
            AppendToFilenameBase(filenameBase, productFamily);
            filenameBase.Append(FilenameSeparator);
            AppendToFilenameBase(filenameBase, product);
            filenameBase.Append(FilenameSeparator);
            AppendToFilenameBase(filenameBase, componentGroup);
            filenameBase.Append(FilenameSeparator);
            AppendToFilenameBase(filenameBase, processName);

            return filenameBase.ToString();
        }

        private static void AppendToFilenameBase(StringBuilder filenameBase, string filenameComponent)
        {
            if (filenameComponent == null)
            {
                filenameBase.Append(FilenameMissingComponentFallback);
                return;
            }

            for (int p = 0; p < filenameComponent.Length; p++)
            {
                char c = filenameComponent[p];
                if (char.IsWhiteSpace(c) || c == '-')
                {
                    filenameBase.Append(FilenameInvalidCharFallback);
                }
                else
                {
                    filenameBase.Append(c);
                }
            }
        }

        private static string GetProcessName()
        {
            string procName = s_processNameString;
            if (procName == null)
            {
                try
                {
                    procName = Process.GetCurrentProcess().ProcessName;
                }
                catch
                {
                    procName = FilenameMissingComponentFallback;
                }

                s_processNameString = procName;
            }

            return procName;
        }

        private static string ReadEnvironmentVariable(string envVarName)
        {
            try
            {
                return Environment.GetEnvironmentVariable(envVarName);
            }
            catch
            {
                return null;
            }
        }
    }
}

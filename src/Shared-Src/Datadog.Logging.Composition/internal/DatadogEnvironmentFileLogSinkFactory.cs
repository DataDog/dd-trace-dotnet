using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Datadog.Logging.Composition
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1308:Variable names must not be prefixed", Justification = "Should not apply to statics")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields must begin with upper-case letter", Justification = "Should only apply to vars that are logically const.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names must not contain underscore", Justification = "Underscore aid visibility in long names")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:Element return value must be documented", Justification = "That would be great.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters must be documented", Justification = "That would be great.")]
    internal class DatadogEnvironmentFileLogSinkFactory
    {
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
        public static string ConstructFilename(string productFamily, string product, string componentGroup, DateTimeOffset timestamp)
        {
            return ConstructFilename(productFamily, product, componentGroup, timestamp, index: -1);
        }

        /// <summary>
        /// The name that will be constructed is:
        /// <c>DD-{ProductFamily}-{Product}-{ComponentGroup}-{ProcessName}-{Date}[_Index].log</c>
        /// </summary>
        public static string ConstructFilename(string productFamily, string product, string componentGroup, DateTimeOffset timestamp, int index)
        {
            return ConstructFilename(productFamily, product, componentGroup, GetProcessName(), timestamp, index);
        }

        /// <summary>
        /// The name that will be constructed is:
        /// <c>DD-{ProductFamily}-{Product}-{ComponentGroup}-{ProcessName}-{Date}[_Index].log</c>
        /// </summary>
        public static string ConstructFilename(string productFamily, string product, string componentGroup, string processName, DateTimeOffset timestamp, int index)
        {
            StringBuilder filenameBuffer = ConstructFilenameBaseBuffer(productFamily, product, componentGroup, processName);
            FileLogSink.ConstructAndAppendFilename(filenameBuffer, timestamp, index);
            return filenameBuffer.ToString();
        }

        /// <summary>
        /// The name base that will be constructed is:
        /// <c>DD-{ProductFamily}-{Product}-{ComponentGroup}-{ProcessName}</c>
        /// </summary>
        public static string ConstructFilenameBase(string productFamily, string product, string componentGroup)
        {
            return ConstructFilenameBase(productFamily, product, componentGroup, GetProcessName());
        }

        /// <summary>
        /// The name base that will be constructed is:
        /// <c>DD-{ProductFamily}-{Product}-{ComponentGroup}-{ProcessName}</c>
        /// </summary>
        public static string ConstructFilenameBase(string productFamily, string product, string componentGroup, string processName)
        {
            return ConstructFilenameBaseBuffer(productFamily, product, componentGroup, processName).ToString();
        }

        public static bool TryCreateNewFileLogSink(string productFamily, string product, string componentGroup, Guid logGroupId, out FileLogSink newLogSink)
        {
            return TryCreateNewFileLogSink(productFamily, product, componentGroup, GetProcessName(), logGroupId, FileLogSink.RotateLogFileWhenLargerBytesDefault, out newLogSink);
        }

        /// <summary>
        /// Attention: All loggers from all processes that write to the same
        ///   (<c>productFamily</c> - <c>product</c> - <c>componentGroup</c> - <c>processName</c>)
        /// MUST use the same value for <c>rotateLogFileWhenLargerBytes</c>!
        /// </summary>
        public static bool TryCreateNewFileLogSink(string productFamily,
                                                   string product,
                                                   string componentGroup,
                                                   Guid logGroupId,
                                                   int rotateLogFileWhenLargerBytes,
                                                   out FileLogSink newLogSink)
        {
            return TryCreateNewFileLogSink(productFamily, product, componentGroup, GetProcessName(), logGroupId, rotateLogFileWhenLargerBytes, out newLogSink);
        }

        public static bool TryCreateNewFileLogSink(string productFamily, string product, string componentGroup, string processName, Guid logGroupId, out FileLogSink newLogSink)
        {
            return TryCreateNewFileLogSink(productFamily, product, componentGroup, processName, logGroupId, FileLogSink.RotateLogFileWhenLargerBytesDefault, out newLogSink);
        }

        /// <summary>
        /// Attention: All loggers from all processes that write to the same
        ///   (<c>productFamily</c> - <c>product</c> - <c>componentGroup</c> - <c>processName</c>)
        /// MUST use the same value for <c>rotateLogFileWhenLargerBytes</c>!
        /// </summary>
        public static bool TryCreateNewFileLogSink(string productFamily,
                                                   string product,
                                                   string componentGroup,
                                                   string processName,
                                                   Guid logGroupId,
                                                   int rotateLogFileWhenLargerBytes,
                                                   out FileLogSink newLogSink)
        {
            // Construct the basis for the file name within the log dir:
            string logFilenameBase = ConstructFilenameBase(productFamily, product, componentGroup, processName);

            // If specified and accessible, use the Env Var for the log folder:
            {
                string userSetDdTraceLogDir = Environment.GetEnvironmentVariable(DdTraceLogDirectoryEnvVarName);
                if (FileLogSink.TryCreateNew(userSetDdTraceLogDir, logFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, out newLogSink))
                {
                    return true;
                }
            }

            // If accessible, use the default for the log folder:
            {
                string defaultProductFamilyLogDir;
                try
                {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    {
                        string commonAppDataDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                        defaultProductFamilyLogDir = Path.Combine(commonAppDataDir, WindowsDefaultLogDirectory, (productFamily ?? FilenameMissingComponentFallback).ToLower());
                    }
                    else
                    {
                        defaultProductFamilyLogDir = Path.Combine(NixDefaultLogDirectory, (productFamily ?? FilenameMissingComponentFallback).ToLower());
                    }
                }
                catch
                {
                    defaultProductFamilyLogDir = null;
                }

                if (FileLogSink.TryCreateNew(defaultProductFamilyLogDir, logFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, out newLogSink))
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

                if (FileLogSink.TryCreateNew(appDir, logFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, out newLogSink))
                {
                    return true;
                }
            }

            // As last resort, we will try using the log directory:
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

                if (FileLogSink.TryCreateNew(tempDir, logFilenameBase, logGroupId, rotateLogFileWhenLargerBytes, out newLogSink))
                {
                    return true;
                }
            }

            // We could not write logs into any of the abofe folders. Give up.

            newLogSink = null;
            return false;
        }

        private static StringBuilder ConstructFilenameBaseBuffer(string productFamily, string product, string componentGroup, string processName)
        {
            var filenameBase = new StringBuilder();
            filenameBase.Append(FilenamePrefix);
            filenameBase.Append(productFamily ?? FilenameMissingComponentFallback);
            filenameBase.Append(FilenameSeparator);
            filenameBase.Append(product ?? FilenameMissingComponentFallback);
            filenameBase.Append(FilenameSeparator);
            filenameBase.Append(componentGroup ?? FilenameMissingComponentFallback);
            filenameBase.Append(FilenameSeparator);
            filenameBase.Append(processName ?? FilenameMissingComponentFallback);
            filenameBase.Append(FilenameSeparator);

            for (int p = 0; p < filenameBase.Length; p++)
            {
                char c = filenameBase[p];
                if (char.IsWhiteSpace(c) || c == '-')
                {
                    filenameBase[p] = FilenameInvalidCharFallback;
                }
            }

            return filenameBase;
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

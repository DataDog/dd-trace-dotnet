using System;
using System.IO;
using System.Text;
using System.Threading;
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
    internal sealed class FileLogSink : ILogSink, IDisposable
    {
        public const int RotateLogFileWhenLargerBytesDefault = 1024 * 1024 * 128;  // 128 MB

        private static readonly LoggingComponentName LogComponentMoniker = LoggingComponentName.Create(typeof(FileLogSink).FullName, null);

        private const string FilenameSeparatorForTimestamp = "-";
        private const string FilenameTimestampFormat = "yyyyMMdd";
        private const string FilenameSeparatorForIndex = "_";
        private const string FilenameIndexFormat = "000";
        private const string FilenameExtension = "log";

        private const bool UseNewLinesInErrorMessages = false;

        private const int FilenameTimestampAndIndexPartsLengthEstimate = 20;

        private static readonly Encoding LogTextEncoding = Encoding.UTF8;

        private static readonly bool s_isWindowsFileSystem = GetIsWindowsFileSystem();

        private readonly Guid _logSessionId;
        private readonly LogGroupMutex _logGroupMutex;
        private readonly string _logFileDir;
        private readonly string _logFileNameBase;
        private readonly int _rotateLogFileWhenLargerBytes;

        private FileStream _logStream;
        private StreamWriter _logWriter;
        private int _rotationIndex;

        private FileLogSink(LogGroupMutex logGroupMutex,
                            string logFileDir,
                            string logFileNameBase,
                            int rotateLogFileWhenLargerBytes,
                            FileStream logStream,
                            int initialRotationIndex)
        {
            _logSessionId = Guid.NewGuid();
            _logGroupMutex = logGroupMutex;
            _logFileDir = logFileDir;
            _logFileNameBase = logFileNameBase;
            _rotateLogFileWhenLargerBytes = (rotateLogFileWhenLargerBytes <= 0) ? -1 : rotateLogFileWhenLargerBytes;

            _logStream = logStream;
            _logWriter = new StreamWriter(logStream, LogTextEncoding);

            _rotationIndex = initialRotationIndex;
        }

        public Guid LogSessionId
        {
            get { return _logSessionId; }
        }

        public int RotateLogFileWhenLargerBytes
        {
            get { return _rotateLogFileWhenLargerBytes; }
        }

        public static bool IsWindowsFileSystem { get { return s_isWindowsFileSystem; } }

        public static bool TryCreateNew(string logFileDir, string logFileNameBase, Guid logGroupId, out FileLogSink newSink)
        {
            return TryCreateNew(logFileDir, logFileNameBase, logGroupId, FileLogSink.RotateLogFileWhenLargerBytesDefault, out newSink);
        }

        /// <summary>
        /// Attention: All loggers from all processes that write to the same <c>logFileNameBase</c> MUST use the same value for <c>rotateLogFileWhenLargerBytes</c>!
        /// </summary>
        public static bool TryCreateNew(string logFileDir, string logFileNameBase, Guid logGroupId, int rotateLogFileWhenLargerBytes, out FileLogSink newSink)
        {
            // Bad usage - throw.

            if (logFileNameBase == null)
            {
                throw new ArgumentNullException(nameof(logFileNameBase));
            }

            if (string.IsNullOrWhiteSpace(logFileNameBase))
            {
                throw new ArgumentException($"{nameof(logFileNameBase)} may not be white-space only.", nameof(logFileNameBase));
            }

            // Ok usage, but bad state - do not throw and return false.

            newSink = null;

            if (string.IsNullOrWhiteSpace(logFileDir))
            {
                return false;
            }

            // Normalize in respect to final dir separator:
            logFileDir = Path.GetDirectoryName(Path.Combine(logFileDir, "."));

            // Ensure the directory exists:
            if (!EnsureDirectoryExists(logFileDir, out DirectoryInfo logFileDirInfo))
            {
                return false;
            }

            try
            {
                var logGroupMutex = new LogGroupMutex(logGroupId);
                if (logGroupMutex.TryAcquire(out LogGroupMutex.Handle logGroupMutexHandle))
                {
                    using (logGroupMutexHandle)
                    {
                        DateTimeOffset now = DateTimeOffset.Now;
                        //int rotationIndex = FindLatestRotationIndex(logFileDirInfo, logFileNameBase, now);
                        int rotationIndex = 0;

                        string logFileName = ConstructFilename(logFileNameBase, now, rotationIndex);
                        string logFilePath = Path.Combine(logFileDir, logFileName);
                        FileStream logStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                        newSink = new FileLogSink(logGroupMutex, logFileDir, logFileNameBase, rotateLogFileWhenLargerBytes, logStream, rotationIndex);
                    }
                }
                else
                {
                    return false;
                }

                if (newSink.TryLogInfo(LogComponentMoniker,
                                       "Logging session started",
                                       "LogGroupId",
                                       logGroupId,
                                       "LogSessionId",
                                       newSink.LogSessionId,
                                       "RotateLogFileWhenLargerBytes",
                                       newSink.RotateLogFileWhenLargerBytes))
                {
                    return true;
                }
            }
            catch
            { }

            // If we did not succeed, the sink may be still constructed (e.g. TryLogInfo(..) returned false).
            // We need to disposed the sink before giving up, but be brepaed for it tobe null.
            try
            {
                newSink?.Dispose();
            }
            catch
            { }

            newSink = null;
            return false;
        }

        public static void ConstructAndAppendFilename(StringBuilder bufferWithFileameBase, DateTimeOffset timestamp)
        {
            ConstructAndAppendFilename(bufferWithFileameBase, timestamp, indexStr: null);
        }

        public static void ConstructAndAppendFilename(StringBuilder bufferWithFileameBase, DateTimeOffset timestamp, int index)
        {
            ConstructAndAppendFilename(bufferWithFileameBase, timestamp, (index < 0) ? null : index.ToString(FilenameIndexFormat));
        }

        public static void ConstructAndAppendFilename(StringBuilder bufferWithFileameBase, DateTimeOffset timestamp, string indexStr)
        {
            if (bufferWithFileameBase == null)
            {
                throw new ArgumentNullException(nameof(bufferWithFileameBase));
            }

            bufferWithFileameBase.Append(FilenameSeparatorForTimestamp);
            bufferWithFileameBase.Append(timestamp.ToString(FilenameTimestampFormat));

            if (indexStr != null)
            {
                bufferWithFileameBase.Append(FilenameSeparatorForIndex);
                bufferWithFileameBase.Append(indexStr);
            }

            bufferWithFileameBase.Append(".");
            bufferWithFileameBase.Append(FilenameExtension);
        }

        public void Dispose()
        {
            if (_logStream != null && _logWriter != null)
            {
                this.TryLogInfo(LogComponentMoniker, "Finishing logging session", "LogSessionId", LogSessionId);

                // If we can acquire the file mutex, we will dispose while holding it, so that no concurrent log writes are affected.
                // But eventually we will disposed regardless.
                bool hasMutex = _logGroupMutex.TryAcquire(out LogGroupMutex.Handle logGroupMutexHandle);
                try
                {
                    StreamWriter logWriter = _logWriter;
                    if (logWriter != null)
                    {
                        _logWriter = null;
                        logWriter.Dispose();
                    }

                    FileStream logStream = _logStream;
                    if (logStream != null)
                    {
                        _logStream = null;
                        logStream.Dispose();
                    }
                }
                finally
                {
                    if (hasMutex)
                    {
                        logGroupMutexHandle.Dispose();
                    }
                }

                _logGroupMutex.Dispose();
            }
        }

        public bool TryLogError(LoggingComponentName componentName, string message, Exception exception, params object[] dataNamesAndValues)
        {
            try
            {
                string errorMessage = DefaultFormat.ConstructErrorMessage(message, exception, UseNewLinesInErrorMessages);
                StringBuilder logLine = DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Error,
                                                                 componentName.Part1,
                                                                 componentName.Part2,
                                                                 useUtcTimestamp: false,
                                                                 errorMessage,
                                                                 dataNamesAndValues);
                return TryWriteToFile(logLine.ToString());
            }
            catch
            {
                return false;
            }
        }

        public bool TryLogInfo(LoggingComponentName componentName, string message, params object[] dataNamesAndValues)
        {
            try
            {
                StringBuilder logLine = DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Info,
                                                            componentName.Part1,
                                                            componentName.Part2,
                                                            useUtcTimestamp: false,
                                                            message,
                                                            dataNamesAndValues);
                return TryWriteToFile(logLine.ToString());
            }
            catch
            {
                return false;
            }
        }

        public bool TryLogDebug(LoggingComponentName componentName, string message, params object[] dataNamesAndValues)
        {
            try
            {
                StringBuilder logLine = DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Debug,
                                                        componentName.Part1,
                                                        componentName.Part2,
                                                        useUtcTimestamp: false,
                                                        message,
                                                        dataNamesAndValues);
                return TryWriteToFile(logLine.ToString());
            }
            catch
            {
                return false;
            }
        }

        private static bool EnsureDirectoryExists(string dirName, out DirectoryInfo dirInfo)
        {
            try
            {
                dirInfo = Directory.CreateDirectory(dirName);
                if (dirInfo.Exists)
                {
                    return true;
                }
            }
            catch
            { }

            dirInfo = null;
            return false;
        }

        private static int FindLatestRotationIndex(DirectoryInfo logFileDirInfo, string logFileNameBase, DateTimeOffset timestamp)
        {
            // The largest existing inde can be obtained by sorting the files that fit the pattern.
            // However, we need to validate that the file not only matches the pattern, but is an exact filename structure match.
            // E.g. "xyz-123.log" and "xyz-aa123.log" both fit the pattern, but only the former is an exact match.

            // Search for files that fit the pattern:
            string filenamePattern = ConstructFilename(logFileNameBase, timestamp, "*");

            // In none found, then 0 is the last index:
            FileInfo[] logFileInfos = logFileDirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
            if (logFileInfos == null || logFileInfos.Length == 0)
            {
                return 0;
            }

            // Look at every filename found startng with the last in the alphabetical order:
            Array.Sort(logFileInfos, (fi1, fi2) => fi1.Name.CompareTo(fi2.Name));
            for (int f = logFileInfos.Length - 1; f >= 0; f--)
            {
                // COnsider the next file:
                string existingFileName = logFileInfos[f].Name;
                string existingFileNameNoExt = Path.GetFileNameWithoutExtension(existingFileName);

                // Is it long-enough to even have the index in it (we matched the pattern, but be defensive)?
                if (existingFileNameNoExt.Length >= FilenameIndexFormat.Length)
                {
                    // Find the "_" that separates the index.
                    // (Remember that in some rare cases the actual index has more digits than given by the min length.)

                    int rotationIndexSeparatorPos = existingFileNameNoExt.LastIndexOf(FilenameSeparatorForIndex);
                    if (rotationIndexSeparatorPos >= 0
                            && rotationIndexSeparatorPos < existingFileNameNoExt.Length
                            && rotationIndexSeparatorPos <= existingFileNameNoExt.Length - FilenameIndexFormat.Length)
                    {
                        // If the "_" separator was found, get the index string that follows it and parse it.

                        string rotationIndexStr = existingFileNameNoExt.Substring(rotationIndexSeparatorPos + 1);
                        if (Int32.TryParse(rotationIndexStr, out int rotationIndex))
                        {
                            // If we can parse the index, compare the actual file name with the correct file name for that index.
                            // If the match in not exact, then we ignore this file and keep searching.
                            // Otherwise we fiund the index.

                            string filenameForIndex = ConstructFilename(logFileNameBase, timestamp, rotationIndex);
                            if (IsSameFilename(existingFileName, filenameForIndex))
                            {
                                return rotationIndex;
                            }
                        }
                    }
                }
            }

            return 0;
        }

        private static bool IsSameFilename(string fileName1, string fileName2)
        {
            if (fileName1 == fileName2)
            {
                return true;
            }

            if (fileName1 == null || fileName2 == null)
            {
                return false;
            }

            if (IsWindowsFileSystem)
            {
                return fileName1.Equals(fileName2, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return fileName1.Equals(fileName2, StringComparison.Ordinal);
            }
        }

        private static bool GetIsWindowsFileSystem()
        {
            try
            {
                PlatformID platformID = Environment.OSVersion.Platform;
                switch (platformID)
                {
                    case PlatformID.Win32S:
                    case PlatformID.Win32Windows:
                    case PlatformID.Win32NT:
                    case PlatformID.WinCE:
                        return true;

                    case PlatformID.Unix:
                    case PlatformID.MacOSX:
                        return false;

                    default:
                        throw new InvalidOperationException($"Unexpected OS PlatformID: \"{platformID}\" ({((int) platformID)})");
                }
            }
            catch
            {
                // defaut to Windows.
                return true;
            }
        }

        private static string ConstructFilename(string nameBase, DateTimeOffset timestamp, int index)
        {
            var filename = new StringBuilder(nameBase.Length + FilenameTimestampAndIndexPartsLengthEstimate);
            filename.Append(nameBase);
            ConstructAndAppendFilename(filename, timestamp, index);
            return filename.ToString();
        }

        private static string ConstructFilename(string nameBase, DateTimeOffset timestamp, string indexStr)
        {
            var filename = new StringBuilder(nameBase.Length + FilenameTimestampAndIndexPartsLengthEstimate);
            filename.Append(nameBase);
            ConstructAndAppendFilename(filename, timestamp, indexStr);
            return filename.ToString();
        }

        private bool TryWriteToFile(string logLine)
        {
            if (TryWriteToFile(logLine, rotateFileIfNecessary: false))
            {
                return true;
            }

            return TryWriteToFile(logLine, rotateFileIfNecessary: true);
        }

        private bool TryWriteToFile(string logLine, bool rotateFileIfNecessary)
        {
            if (_logGroupMutex.TryAcquire(out LogGroupMutex.Handle logGroupMutexHandle))
            {
                using (logGroupMutexHandle)
                {
                    long pos = _logStream.Seek(0, SeekOrigin.End);
                    while (_rotateLogFileWhenLargerBytes > 0 && pos > _rotateLogFileWhenLargerBytes)
                    {
                        // If rotating in not enabled OR if we try and fail rotating => give up.
                        if (!rotateFileIfNecessary || !RotateLogFile(logGroupMutexHandle))
                        {
                            return false;
                        }

                        pos = _logStream.Seek(0, SeekOrigin.End);
                    }

                    _logWriter.WriteLine(logLine);

                    _logWriter.Flush();
                    _logStream.Flush(flushToDisk: true);

                    return true;
                }
            }

            return false;
        }

        private bool RotateLogFile(LogGroupMutex.Handle logGroupMutexHandle)
        {
            // Be defensive: Wiw we remember to take the locks:
            if (logGroupMutexHandle.IsValid != true)
            {
                return false;
            }

            if (!EnsureDirectoryExists(_logFileDir, out DirectoryInfo logFileDirInfo))
            {
                return false;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            int lastRotationIndexOnDisk = FindLatestRotationIndex(logFileDirInfo, _logFileNameBase, now);

            int nextRotationIndex = (lastRotationIndexOnDisk > _rotationIndex)
                                        ? lastRotationIndexOnDisk
                                        : _rotationIndex + 1;

            string logFileName = ConstructFilename(_logFileNameBase, now, nextRotationIndex);
            string logFilePath = Path.Combine(_logFileDir, logFileName);
            FileStream logStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            StreamWriter logWriter = new StreamWriter(logStream, LogTextEncoding);

            _rotationIndex = nextRotationIndex;
            _logStream = logStream;
            _logWriter = logWriter;
            return true;
        }
    }
}

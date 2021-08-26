using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1308:Variable names must not be prefixed", Justification = "Should not apply to statics")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1311:Static readonly fields must begin with upper-case letter", Justification = "Should only apply to vars that are logically const.")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1116:Split parameters must start on line after declaration", Justification = "Bad rule")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names must not contain underscore", Justification = "Underscore aid visibility in long names")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0007:Use implicit type", Justification = "Worst piece of advise Style tools ever gave.")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1615:Element return value must be documented", Justification = "That would be great.")]
    //[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1611:Element parameters must be documented", Justification = "That would be great.")]
    internal sealed class FileLogSink : ILogSink, IDisposable
    {
        public const int RotateLogFileWhenLargerBytesDefault = 1024 * 1024 * 128;  // 128 MB

#pragma warning disable IDE1006  // Runtime-initialized Constants {
        public static readonly DefaultFormat.Options DefaultFormatOptions = new DefaultFormat.Options(useUtcTimestamps: false,
                                                                                                      useNewLinesInErrorMessages: false,
                                                                                                      useNewLinesInDataNamesAndValues: false);

        private static readonly LogSourceInfo SelfLogSourceInfo = new LogSourceInfo(typeof(FileLogSink).FullName);

        private static readonly Encoding LogTextEncoding = Encoding.UTF8;
#pragma warning restore IDE1006  // } Runtime-initialized Constants

        private const string FilenameSeparatorForTimestamp = "-";
        private const string FilenameTimestampFormat = "yyyyMMdd";
        private const string FilenameSeparatorForIndex = "_";
        private const string FilenameIndexFormat = "000";
        private const string FilenameExtension = "log";

        private const int FilenameTimestampAndIndexPartsLengthEstimate = 20;

        private static readonly bool s_isWindowsFileSystem = GetIsWindowsFileSystem();

        private readonly DefaultFormat.Options _formatOptions;
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
                            int initialRotationIndex,
                            DefaultFormat.Options formatOptions)
        {
            _logSessionId = Guid.NewGuid();
            _logGroupMutex = logGroupMutex;
            _logFileDir = logFileDir;
            _logFileNameBase = logFileNameBase;
            _rotateLogFileWhenLargerBytes = (rotateLogFileWhenLargerBytes <= 0) ? -1 : rotateLogFileWhenLargerBytes;

            _logStream = logStream;
            _logWriter = new StreamWriter(logStream, LogTextEncoding);

            _rotationIndex = initialRotationIndex;

            _formatOptions = formatOptions ?? DefaultFormatOptions;
        }

        public Guid LogSessionId
        {
            get { return _logSessionId; }
        }

        public Guid LogGroupId
        {
            get { return _logGroupMutex.LogGroupId; }
        }

        public int RotateLogFileWhenLargerBytes
        {
            get { return _rotateLogFileWhenLargerBytes; }
        }

        public bool IsRotateLogFileBasedOnSizeEnabled
        {
            get { return (_rotateLogFileWhenLargerBytes > 0); }
        }

        public static bool IsWindowsFileSystem { get { return s_isWindowsFileSystem; } }

        public static bool TryCreateNew(string logFileDir, string logFileNameBase, Guid logGroupId, DefaultFormat.Options formatOptions, out FileLogSink newSink)
        {
            return TryCreateNew(logFileDir, logFileNameBase, logGroupId, FileLogSink.RotateLogFileWhenLargerBytesDefault, formatOptions, out newSink);
        }

        /// <summary>
        /// Attention: All loggers from all processes that write to the same <c>logFileNameBase</c> MUST use the same value for <c>rotateLogFileWhenLargerBytes</c>!
        /// </summary>
        public static bool TryCreateNew(string logFileDir,
                                        string logFileNameBase,
                                        Guid logGroupId,
                                        int rotateLogFileWhenLargerBytes,
                                        DefaultFormat.Options formatOptions,
                                        out FileLogSink newSink)
        {
            // Bad usage - throw.

            if (logFileNameBase == null)
            {
                throw new ArgumentNullException(nameof(logFileNameBase));
            }

            if (String.IsNullOrWhiteSpace(logFileNameBase))
            {
                throw new ArgumentException($"{nameof(logFileNameBase)} may not be white-space only.", nameof(logFileNameBase));
            }

            // Ok usage, but bad state - do not throw and return false.

            newSink = null;

            if (String.IsNullOrWhiteSpace(logFileDir))
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

            LogGroupMutex logGroupMutex = null;
            try
            {
                logGroupMutex = new LogGroupMutex(logGroupId);
                if (!logGroupMutex.TryAcquire(out LogGroupMutex.Handle logGroupMutexHandle))
                {
                    logGroupMutex.Dispose();
                    return false;
                }

                using (logGroupMutexHandle)
                {
                    DateTimeOffset now = DateTimeOffset.Now;
                    int rotationIndex = FindLatestRotationIndex(logFileDirInfo, logFileNameBase, now);

                    string logFileName = ConstructFilename(logFileNameBase, now, rotationIndex);
                    string logFilePath = Path.Combine(logFileDir, logFileName);
                    FileStream logStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                    newSink = new FileLogSink(logGroupMutex, logFileDir, logFileNameBase, rotateLogFileWhenLargerBytes, logStream, rotationIndex, formatOptions);
                }

                if (newSink.TryLogInfo(SelfLogSourceInfo.WithCallInfo().WithAssemblyName(),
                                       "Logging session started",
                                       new object[]
                                           {
                                               "LogGroupId",
                                               newSink.LogGroupId,
                                               "LogSessionId",
                                               newSink.LogSessionId,
                                               "RotateLogFileWhenLargerBytes",
                                               newSink.RotateLogFileWhenLargerBytes
                                           }))
                {
                    return true;
                }
            }
            catch
            { }

            // If we did not succeed, the sink may be still constructed (e.g. TryLogInfo(..) returned false).
            // We need to dispose the sink before giving up, but be brepaed for it to sbe null.
            try
            {
                if (newSink != null)
                {
                    newSink.Dispose();  // This will also dispose the logGroupMutex owned by the newSink.
                }
                else
                {
                    logGroupMutex.Dispose();
                }
            }
            catch
            { }

            newSink = null;
            return false;
        }

        public static string ConstructFilename(string nameBase, DateTimeOffset timestamp)
        {
            return ConstructFilename(nameBase, timestamp, indexStr: null);
        }

        public static string ConstructFilename(string nameBase, DateTimeOffset timestamp, int index)
        {
            return ConstructFilename(nameBase, timestamp, indexStr: (index < 0) ? null : index.ToString(FilenameIndexFormat));
        }

        public static string ConstructFilename(string nameBase, DateTimeOffset timestamp, string indexStr)
        {
            if (nameBase == null)
            {
                throw new ArgumentNullException(nameof(nameBase));
            }

            var filename = new StringBuilder(nameBase.Length + FilenameTimestampAndIndexPartsLengthEstimate);
            filename.Append(nameBase);

            filename.Append(FilenameSeparatorForTimestamp);
            filename.Append(timestamp.ToString(FilenameTimestampFormat));

            if (indexStr != null)
            {
                filename.Append(FilenameSeparatorForIndex);
                filename.Append(indexStr);
            }

            filename.Append(".");
            filename.Append(FilenameExtension);

            return filename.ToString();
        }

        public void Dispose()
        {
            if (_logStream == null && _logWriter == null)
            {
                // Already disposed.
                return;
            }

            // If this sink is already disposed, then TryLogInfo(..) will fail to aquire the _logGroupMutex and will gracefully return false.
            this.TryLogInfo(SelfLogSourceInfo.WithCallInfo().WithAssemblyName(), "Finishing logging session", new object[] { "LogSessionId", LogSessionId });

            // If we can acquire the file mutex, we will dispose while holding it, so that concurrent log writes are not affected.
            // But eventually we will dispose regardless.
            bool hasMutex = _logGroupMutex.TryAcquire(out LogGroupMutex.Handle logGroupMutexHandle);
            try
            {
                SafeDisposeAndSetToNull(ref _logWriter);
                SafeDisposeAndSetToNull(ref _logStream);
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

        public bool TryLogError(LogSourceInfo logSourceInfo, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            try
            {
                string errorMessage = DefaultFormat.ErrorMessage.Construct(message, exception, _formatOptions.UseNewLinesInErrorMessages);
                StringBuilder logLine = DefaultFormat.LogLine.Construct(DefaultFormat.LogLevelMoniker_Error,
                                                                        logSourceInfo.LogSourceNamePart1,
                                                                        logSourceInfo.LogSourceNamePart2,
                                                                        logSourceInfo.CallLineNumber,
                                                                        logSourceInfo.CallMemberName,
                                                                        logSourceInfo.CallFileName,
                                                                        logSourceInfo.AssemblyName,
                                                                        errorMessage,
                                                                        dataNamesAndValues,
                                                                        _formatOptions.UseUtcTimestamps,
                                                                        _formatOptions.UseNewLinesInDataNamesAndValues);
                return TryWriteToFile(logLine.ToString());
            }
            catch
            {
                return false;
            }
        }

        public bool TryLogInfo(LogSourceInfo logSourceInfo, string message, IEnumerable<object> dataNamesAndValues)
        {
            try
            {
                StringBuilder logLine = DefaultFormat.LogLine.Construct(DefaultFormat.LogLevelMoniker_Info,
                                                                        logSourceInfo.LogSourceNamePart1,
                                                                        logSourceInfo.LogSourceNamePart2,
                                                                        logSourceInfo.CallLineNumber,
                                                                        logSourceInfo.CallMemberName,
                                                                        logSourceInfo.CallFileName,
                                                                        logSourceInfo.AssemblyName,
                                                                        message,
                                                                        dataNamesAndValues,
                                                                        _formatOptions.UseUtcTimestamps,
                                                                        _formatOptions.UseNewLinesInDataNamesAndValues);
                return TryWriteToFile(logLine.ToString());
            }
            catch
            {
                return false;
            }
        }

        public bool TryLogDebug(LogSourceInfo logSourceInfo, string message, IEnumerable<object> dataNamesAndValues)
        {
            try
            {
                StringBuilder logLine = DefaultFormat.LogLine.Construct(DefaultFormat.LogLevelMoniker_Debug,
                                                                        logSourceInfo.LogSourceNamePart1,
                                                                        logSourceInfo.LogSourceNamePart2,
                                                                        logSourceInfo.CallLineNumber,
                                                                        logSourceInfo.CallMemberName,
                                                                        logSourceInfo.CallFileName,
                                                                        logSourceInfo.AssemblyName,
                                                                        message,
                                                                        dataNamesAndValues,
                                                                        _formatOptions.UseUtcTimestamps,
                                                                        _formatOptions.UseNewLinesInDataNamesAndValues);
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

                    case PlatformID.Xbox:
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

        private static bool SafeDisposeAndSetToNull<T>(ref T reference) where T : class, IDisposable
        {
            T referencedItem = Interlocked.Exchange(ref reference, null);
            if (referencedItem != null)
            {
                try
                {
                    referencedItem.Dispose();
                    return true;
                }
                catch
                { }
            }

            return false;
        }

        private bool TryWriteToFile(string logLine)
        {
            if (_logGroupMutex.TryAcquire(out LogGroupMutex.Handle logGroupMutexHandle))
            {
                using (logGroupMutexHandle)
                {
                    long pos = _logStream.Seek(0, SeekOrigin.End);
                    while (IsRotateLogFileBasedOnSizeEnabled && pos > _rotateLogFileWhenLargerBytes)
                    {
                        // Ff we try and fail rotating => give up.
                        if (!RotateLogFile(logGroupMutexHandle))
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
            // Be defensive: Did we remember to take the locks?
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

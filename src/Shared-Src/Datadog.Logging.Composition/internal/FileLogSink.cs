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

        private const string FilenameSeparatorForTimestamp = "-";
        private const string FilenameTimestampFormat = "yyyyMMdd";
        private const string FilenameSeparatorForIndex = "_";
        private const string FilenameIndexFormat = "000";
        private const string FilenameExtension = "log";

        private const int FilenameTimestampAndIndexPartsLengthEstimate = 20;

        private static readonly Encoding LogTextEncoding = Encoding.UTF8;

        private readonly object _rotationLock = new object();
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
            _rotateLogFileWhenLargerBytes = rotateLogFileWhenLargerBytes;

            _logStream = logStream;
            _logWriter = new StreamWriter(logStream, LogTextEncoding);

            _rotationIndex = initialRotationIndex;
        }

        public Guid LogSessionId
        {
            get { return _logSessionId; }
        }

        public static bool TryCreateNew(string logFileDir, string logFileNameBase, Guid logGroupId, out FileLogSink newSink)
        {
            return TryCreateNew(logFileDir, logFileNameBase, logGroupId, FileLogSink.RotateLogFileWhenLargerBytesDefault, out newSink);
        }

        /// <summary>
        /// Attention: All loggers from all processes that write to the same <c>logFileNameBase</c> MUST use the same value for <c>rotateLogFileWhenLargerBytes</c>!
        /// </summary>
        public static bool TryCreateNew(string logFileDir, string logFileNameBase, Guid logGroupId, int rotateLogFileWhenLargerBytes, out FileLogSink newSink)
        {
            if (logFileNameBase == null)
            {
                throw new ArgumentNullException(nameof(logFileNameBase));
            }

            if (string.IsNullOrWhiteSpace(logFileNameBase))
            {
                throw new ArgumentException($"{nameof(logFileNameBase)} may not be white-space only.", nameof(logFileNameBase));
            }

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

            var logGroupMutex = new LogGroupMutex(logGroupId);
            if (!logGroupMutex.TryAcquire(out Mutex mutex))
            {
                return false;
            }

            if (rotateLogFileWhenLargerBytes <= 0)
            {
                rotateLogFileWhenLargerBytes = -1;
            }

            try
            {
                try
                {
                    DateTimeOffset now = DateTimeOffset.Now;
                    int rotationIndex = FindLatestRotationIndex(logFileDirInfo, logFileNameBase, now);

                    string logFileName = ConstructFilename(logFileNameBase, now, rotationIndex);
                    string logFilePath = Path.Combine(logFileDir, logFileName);
                    FileStream logStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                    newSink = new FileLogSink(logGroupMutex, logFileDir, logFileNameBase, rotateLogFileWhenLargerBytes, logStream, rotationIndex);
                }
                finally
                {
                    mutex.ReleaseMutex();
                }

                newSink.Info(StringPair.Create(typeof(FileLogSink).FullName, null),
                             "Logging session started",
                             "LogGroupId",
                             logGroupId,
                             "LogSessionId",
                             newSink.LogSessionId,
                             "RotateLogFileWhenLargerBytes",
                             rotateLogFileWhenLargerBytes);
            }
            catch
            {
                try
                {
                    newSink.Dispose();
                }
                catch
                { }

                newSink = null;
                return false;
            }

            return true;
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
                this.Info(StringPair.Create(typeof(FileLogSink).FullName, null), "Finishing logging session", "LogSessionId", LogSessionId);

                lock (_rotationLock)
                {
                    bool hasMutex = _logGroupMutex.TryAcquire(out Mutex mutex);
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
                            mutex.ReleaseMutex();
                        }
                    }

                    _logGroupMutex.Dispose();
                }
            }
        }

        public void Error(StringPair componentName, string message, Exception exception, params object[] dataNamesAndValues)
        {
            string errorMessage = DefaultFormat.ConstructErrorMessage(message, exception);
            string logLine = DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Error,
                                                             componentName.Item1,
                                                             componentName.Item2,
                                                             useUtcTimestamp: false,
                                                             errorMessage,
                                                             dataNamesAndValues)
                                          .ToString();
            WriteToFile(logLine);
        }

        public void Info(StringPair componentName, string message, params object[] dataNamesAndValues)
        {
            string logLine = DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Info,
                                                            componentName.Item1,
                                                            componentName.Item2,
                                                            useUtcTimestamp: false,
                                                            message,
                                                            dataNamesAndValues)
                                          .ToString();
            WriteToFile(logLine);
        }

        public void Debug(StringPair componentName, string message, params object[] dataNamesAndValues)
        {
            string logLine = DefaultFormat.ConstructLogLine(DefaultFormat.LogLevelMoniker_Debug,
                                                            componentName.Item1,
                                                            componentName.Item2,
                                                            useUtcTimestamp: false,
                                                            message,
                                                            dataNamesAndValues)
                                          .ToString();
            WriteToFile(logLine);
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
            string filenamePattern = ConstructFilename(logFileNameBase, timestamp, "*");

            FileInfo[] logFileInfos = logFileDirInfo.GetFiles("*", SearchOption.TopDirectoryOnly);
            if (logFileInfos == null || logFileInfos.Length == 0)
            {
                return 0;
            }

            Array.Sort(logFileInfos, (fi1, fi2) => -fi1.Name.CompareTo(fi2));
            string lastFile = logFileInfos[0].Name;
            lastFile = Path.GetFileNameWithoutExtension(lastFile);

            string rotationIndexStr = lastFile.Substring(lastFile.Length - 3);
            int rotationIndex = int.Parse(rotationIndexStr);
            return rotationIndex;
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

        private bool WriteToFile(string logLine)
        {
            bool logLineWritten = false;
            while (!logLineWritten)
            {
                if (!_logGroupMutex.TryAcquire(out Mutex mutex))
                {
                    return false;  // Disposed or error => give up.
                }

                try
                {
                    long pos = _logStream.Seek(0, SeekOrigin.End);
                    if (_rotateLogFileWhenLargerBytes > 0 && pos <= _rotateLogFileWhenLargerBytes)
                    {
                        _logWriter.WriteLine(logLine);

                        _logWriter.Flush();
                        _logStream.Flush(flushToDisk: true);

                        logLineWritten = true;
                    }
                }
                finally
                {
                    mutex.ReleaseMutex();
                }

                if (!logLineWritten)
                {
                    if (!RotateLogFile())
                    {
                        return false;  // Cannot rotate => give up.
                    }
                }
            }

            return true;
        }

        private bool RotateLogFile()
        {
            try
            {
                if (!EnsureDirectoryExists(_logFileDir, out DirectoryInfo logFileDirInfo))
                {
                    return false;
                }

                lock (_rotationLock)
                {
                    int nextRotationIndex;
                    FileStream logStream;
                    StreamWriter logWriter;

                    if (!_logGroupMutex.TryAcquire(out Mutex mutex))
                    {
                        return false;  // Disposed or error => give up.
                    }

                    try
                    {
                        DateTimeOffset now = DateTimeOffset.Now;
                        int lastRotationIndexOnDisk = FindLatestRotationIndex(logFileDirInfo, _logFileNameBase, now);

                        nextRotationIndex = (lastRotationIndexOnDisk > _rotationIndex)
                                                    ? lastRotationIndexOnDisk
                                                    : _rotationIndex + 1;

                        string logFileName = ConstructFilename(_logFileNameBase, now, nextRotationIndex);
                        string logFilePath = Path.Combine(_logFileDir, logFileName);
                        logStream = new FileStream(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                        logWriter = new StreamWriter(_logStream, LogTextEncoding);
                    }
                    finally
                    {
                        mutex.ReleaseMutex();
                    }

                    _rotationIndex = nextRotationIndex;
                    _logStream = logStream;
                    _logWriter = logWriter;
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}

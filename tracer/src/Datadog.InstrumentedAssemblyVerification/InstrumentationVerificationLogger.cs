using System;
using System.IO;
using System.Text;

namespace Datadog.InstrumentedAssemblyVerification
{
    internal enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    internal sealed class InstrumentationVerificationLogger : IDisposable
    {
        private readonly LogLevel _logLevel;
        private readonly string _path;
        private bool _disposed;
        private readonly StringBuilder _log;
        private readonly bool _isInitialized;

#if DEBUG
        public InstrumentationVerificationLogger(string instrumentedAssembliesOutputFolder) : this(instrumentedAssembliesOutputFolder, LogLevel.Debug)
        {
        }
#else
        public InstrumentationVerificationLogger(string instrumentedAssembliesOutputFolder) : this(instrumentedAssembliesOutputFolder, LogLevel.Info)
        {
        }
#endif

        public InstrumentationVerificationLogger(string instrumentedModulePath, LogLevel logLevel)
        {
            try
            {
                _logLevel = logLevel;
                var instrumentedAssembliesOutputFolder = Directory.GetParent(instrumentedModulePath);
                string logFolder = Path.Combine(instrumentedAssembliesOutputFolder.FullName, "Logs");
                Directory.CreateDirectory(logFolder);
                _path = Path.Combine(logFolder, 
                                     $"{nameof(InstrumentationVerificationLogger)}_" +
                                     $"{Path.GetFileNameWithoutExtension(instrumentedModulePath)}_" +
                                     $"{DateTime.Now.ToUniversalTime():dd-MM-yyyy_HH-mm-ss}.log");
                _log = new StringBuilder();
                _isInitialized = true;
            }
            catch (Exception e)
            {
                _isInitialized = false;
                Console.WriteLine("Failed to create logger: " + e);
            }
        }

        public void Error(string error)
        {
            if (ShouldLog(LogLevel.Error))
            {
                _log.AppendLine("Error: " + error);
            }
        }

        public void Error(Exception ex)
        {
            if (ShouldLog(LogLevel.Error))
            {
                _log.AppendLine("Error: " + ex);
            }
        }

        public void Warn(string warn)
        {
            if (ShouldLog(LogLevel.Warn))
            {
                _log.AppendLine("Warning: " + warn);
            }
        }

        public void Warn(Exception ex)
        {
            if (ShouldLog(LogLevel.Error))
            {
                _log.AppendLine("Warning: " + ex);
            }
        }

        public void Info(string info)
        {
            if (ShouldLog(LogLevel.Info))
            {
                _log.AppendLine("Info:  " + info);
            }
        }

        public void Debug(string message)
        {
            if (ShouldLog(LogLevel.Debug))
            {
                _log.AppendLine("Debug: " + message);
            }
        }

        private bool ShouldLog(LogLevel logLevel)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InstrumentationVerificationLogger));
            }
            return _isInitialized && logLevel >= _logLevel;
        }

        public void Dispose()
        {
            var preColor = Console.ForegroundColor;
            try
            {
                _disposed = true;
                File.WriteAllText(_path, _log.ToString());
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"{GetType().Name} saved to: '{_path}'");
            }
            catch (Exception)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($"Fail to save {GetType().Name}");
                throw;
            }
            Console.ForegroundColor = preColor;
        }
    }
}

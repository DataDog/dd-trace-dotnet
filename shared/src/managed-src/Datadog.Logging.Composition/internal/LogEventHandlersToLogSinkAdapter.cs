using System;
using System.Collections.Generic;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    internal sealed class LogEventHandlersToLogSinkAdapter : IDisposable
    {
        private readonly ILogSink _targetLogSink;

        public LogEventHandlersToLogSinkAdapter(ILogSink targetLogSink)
        {
            if (targetLogSink == null)
            {
                throw new ArgumentNullException(nameof(targetLogSink));
            }

            _targetLogSink = targetLogSink;
        }

        public ILogSink TargetLogSink
        {
            get { return _targetLogSink; }
        }

        public void Error(string logSourceNamePart1,
                                 string logSourceNamePart2,
                                 int logSourceCallLineNumber,
                                 string logSourceCallMemberName,
                                 string logSourceCallFileName,
                                 string logSourceAssemblyName,
                                 string message,
                                 Exception exception,
                                 IEnumerable<object> dataNamesAndValues)
        {
            _targetLogSink.TryLogError(new LogSourceInfo(logSourceNamePart1,
                                                         logSourceNamePart2,
                                                         logSourceCallLineNumber,
                                                         logSourceCallMemberName,
                                                         logSourceCallFileName,
                                                         logSourceAssemblyName),
                                       message,
                                       exception,
                                       dataNamesAndValues);
        }

        public void Info(string logSourceNamePart1,
                         string logSourceNamePart2,
                         int logSourceCallLineNumber,
                         string logSourceCallMemberName,
                         string logSourceCallFileName,
                         string logSourceAssemblyName,
                         string message,
                         IEnumerable<object> dataNamesAndValues)
        {
            _targetLogSink.TryLogInfo(new LogSourceInfo(logSourceNamePart1,
                                                         logSourceNamePart2,
                                                         logSourceCallLineNumber,
                                                         logSourceCallMemberName,
                                                         logSourceCallFileName,
                                                         logSourceAssemblyName),
                                      message,
                                      dataNamesAndValues);
        }

        public void Debug(string logSourceNamePart1,
                          string logSourceNamePart2,
                          int logSourceCallLineNumber,
                          string logSourceCallMemberName,
                          string logSourceCallFileName,
                          string logSourceAssemblyName,
                          string message,
                          IEnumerable<object> dataNamesAndValues)
        {
            _targetLogSink.TryLogDebug(new LogSourceInfo(logSourceNamePart1,
                                                         logSourceNamePart2,
                                                         logSourceCallLineNumber,
                                                         logSourceCallMemberName,
                                                         logSourceCallFileName,
                                                         logSourceAssemblyName),
                                       message,
                                       dataNamesAndValues);
        }

        public void Dispose()
        {
            if (_targetLogSink is IDisposable disposableLogSink)
            {
                disposableLogSink.Dispose();
            }
        }
    }
}

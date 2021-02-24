using System;
using System.Collections.Generic;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    internal class ComponentGroupCompositionLogSink : ILogSink, IDisposable
    {
        private readonly ILogSink _downstreamLogSink;
        private readonly string _logComponentGroupMoniker;

        public ComponentGroupCompositionLogSink(string logComponentGroupMoniker, ILogSink downstreamLogSink)
        {
            if (downstreamLogSink == null)
            {
                throw new ArgumentNullException(nameof(downstreamLogSink));
            }

            _logComponentGroupMoniker = logComponentGroupMoniker;
            _downstreamLogSink = downstreamLogSink;
        }

        public ILogSink DownstreamLogSink
        {
            get { return _downstreamLogSink; }
        }

        public string LogComponentGroupMoniker
        {
            get { return _logComponentGroupMoniker; }
        }

        public bool TryLogError(LoggingComponentName componentName, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            return _downstreamLogSink.TryLogError(CreateComposedLogComponentName(componentName), message, exception, dataNamesAndValues);
        }

        public bool TryLogInfo(LoggingComponentName componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            return _downstreamLogSink.TryLogInfo(CreateComposedLogComponentName(componentName), message, dataNamesAndValues);
        }

        public bool TryLogDebug(LoggingComponentName componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            return _downstreamLogSink.TryLogDebug(CreateComposedLogComponentName(componentName), message, dataNamesAndValues);
        }

        public void OnErrorLogEvent(string componentName, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            _downstreamLogSink.TryLogError(LoggingComponentName.Create(_logComponentGroupMoniker, componentName), message, exception, dataNamesAndValues);
        }

        public void OnInfoLogEvent(string componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            _downstreamLogSink.TryLogInfo(LoggingComponentName.Create(_logComponentGroupMoniker, componentName), message, dataNamesAndValues);
        }

        public void OnDebugLogEvent(string componentName, string message, IEnumerable<object> dataNamesAndValues)
        {
            _downstreamLogSink.TryLogDebug(LoggingComponentName.Create(_logComponentGroupMoniker, componentName), message, dataNamesAndValues);
        }

        public void Dispose()
        {
            if (_downstreamLogSink is IDisposable disposableLogSink)
            {
                disposableLogSink.Dispose();
            }
        }

        private LoggingComponentName CreateComposedLogComponentName(LoggingComponentName componentName)
        {
            string composedPart1, composedPart2;
            DefaultFormat.ComposeComponentName(_logComponentGroupMoniker, componentName.Part1, componentName.Part2, out composedPart1, out composedPart2);
            return LoggingComponentName.Create(composedPart1, composedPart2);
        }
    }
}

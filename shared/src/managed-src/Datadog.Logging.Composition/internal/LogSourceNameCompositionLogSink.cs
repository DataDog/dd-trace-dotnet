using System;
using System.Collections.Generic;
using Datadog.Logging.Emission;

namespace Datadog.Logging.Composition
{
    internal sealed class LogSourceNameCompositionLogSink : ILogSink, IDisposable
    {
        private readonly ILogSink _downstreamLogSink;
        private readonly string _logSourcesGroupMoniker;

        public LogSourceNameCompositionLogSink(string logSourcesGroupMoniker, ILogSink downstreamLogSink)
        {
            if (downstreamLogSink == null)
            {
                throw new ArgumentNullException(nameof(downstreamLogSink));
            }

            _logSourcesGroupMoniker = logSourcesGroupMoniker;
            _downstreamLogSink = downstreamLogSink;
        }

        public ILogSink DownstreamLogSink
        {
            get { return _downstreamLogSink; }
        }

        public string LogSourcesGroupMoniker
        {
            get { return _logSourcesGroupMoniker; }
        }

        public bool TryLogError(LogSourceInfo logSourceInfo, string message, Exception exception, IEnumerable<object> dataNamesAndValues)
        {
            return _downstreamLogSink.TryLogError(logSourceInfo.WithinLogSourcesGroup(_logSourcesGroupMoniker), message, exception, dataNamesAndValues);
        }

        public bool TryLogInfo(LogSourceInfo logSourceInfo, string message, IEnumerable<object> dataNamesAndValues)
        {
            return _downstreamLogSink.TryLogInfo(logSourceInfo.WithinLogSourcesGroup(_logSourcesGroupMoniker), message, dataNamesAndValues);
        }

        public bool TryLogDebug(LogSourceInfo logSourceInfo, string message, IEnumerable<object> dataNamesAndValues)
        {
            return _downstreamLogSink.TryLogDebug(logSourceInfo.WithinLogSourcesGroup(_logSourcesGroupMoniker), message, dataNamesAndValues);
        }

        public void Dispose()
        {
            if (_downstreamLogSink is IDisposable disposableLogSink)
            {
                disposableLogSink.Dispose();
            }
        }
    }
}

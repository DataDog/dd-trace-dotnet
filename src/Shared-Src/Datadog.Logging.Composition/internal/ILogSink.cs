using System;

namespace Datadog.Logging.Composition
{
    internal interface ILogSink
    {
        bool TryLogError(LoggingComponentName componentName, string message, Exception exception, object[] dataNamesAndValues);

        bool TryLogInfo(LoggingComponentName componentName, string message, object[] dataNamesAndValues);

        bool TryLogDebug(LoggingComponentName componentName, string message, object[] dataNamesAndValues);
    }
}

using System;
using System.Collections.Generic;

namespace Datadog.Logging.Composition
{
    internal interface ILogSink
    {
        bool TryLogError(LoggingComponentName componentName, string message, Exception exception, IEnumerable<object> dataNamesAndValues);

        bool TryLogInfo(LoggingComponentName componentName, string message, IEnumerable<object> dataNamesAndValues);

        bool TryLogDebug(LoggingComponentName componentName, string message, IEnumerable<object> dataNamesAndValues);
    }
}

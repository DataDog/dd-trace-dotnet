using System;

using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal interface IDatadogLogger
    {
        bool IsEnabled(LogEventLevel level);

        void Debug(string messageTemplate);

        void Debug<T>(string messageTemplate, T property);

        void Debug(string messageTemplate, params object[] args);

        void Debug<T>(Exception exception, string messageTemplate, T property);

        void Information(string messageTemplate, params object[] args);

        void Information(Exception exception, string messageTemplate, params object[] args);

        void Warning(string messageTemplate, params object[] args);

        void Warning(Exception ex, string messageTemplate, params object[] args);

        void Error(string messageTemplate, params object[] args);

        void Error(Exception ex, string messageTemplate, params object[] args);
    }
}

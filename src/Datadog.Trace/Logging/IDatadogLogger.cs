using System;

using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.Logging
{
    internal interface IDatadogLogger
    {
        bool IsEnabled(LogEventLevel level);

        void Debug(string messageTemplate);

        void Debug<T>(string messageTemplate, T property);

        void Debug<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Debug<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Debug(string messageTemplate, object[] args);

        void Debug(Exception exception, string messageTemplate);

        void Debug<T>(Exception exception, string messageTemplate, T property);

        void Debug<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1);

        void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Debug(Exception exception, string messageTemplate, object[] args);

        void Information(string messageTemplate);

        void Information<T>(string messageTemplate, T property);

        void Information<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Information<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Information(string messageTemplate, object[] args);

        void Information(Exception exception, string messageTemplate);

        void Information<T>(Exception exception, string messageTemplate, T property);

        void Information<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1);

        void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Information(Exception exception, string messageTemplate, object[] args);

        void Warning(string messageTemplate);

        void Warning<T>(string messageTemplate, T property);

        void Warning<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Warning<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Warning(string messageTemplate, object[] args);

        void Warning(Exception exception, string messageTemplate);

        void Warning<T>(Exception exception, string messageTemplate, T property);

        void Warning<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1);

        void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Warning(Exception exception, string messageTemplate, object[] args);

        void Error(string messageTemplate);

        void Error<T>(string messageTemplate, T property);

        void Error<T0, T1>(string messageTemplate, T0 property0, T1 property1);

        void Error<T0, T1, T2>(string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Error(string messageTemplate, object[] args);

        void Error(Exception exception, string messageTemplate);

        void Error<T>(Exception exception, string messageTemplate, T property);

        void Error<T0, T1>(Exception exception, string messageTemplate, T0 property0, T1 property1);

        void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 property0, T1 property1, T2 property2);

        void Error(Exception exception, string messageTemplate, object[] args);
    }
}

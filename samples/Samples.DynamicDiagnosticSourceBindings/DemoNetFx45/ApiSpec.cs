using Datadog.Util;
using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public class DiagnosticSourceStub
    {
        public bool IsEnabled(string eventName)
        {
            return IsEnabled(eventName, arg1: null, arg2: null);
        }

        public bool IsEnabled(string eventName, object arg1)
        {
            return IsEnabled(eventName, arg1, arg2: null);
        }

        public bool IsEnabled(string eventName, object arg1, object arg2)
        {
            Validate.NotNull(eventName, nameof(eventName));
            // arg1 and arg2 may be null
            return false;
        }

        public void Write(string eventName, object payloadValue)
        {
            Validate.NotNull(eventName, nameof(eventName));
            // payloadValue may be null
        }
    }

    public struct DiagnosticSourceInfo
    {
        public string Name { get; }

        public IDisposable SubscribeToEvents(Action<string, object> eventObserver, Func<string, object, object, bool> isEventEnabledFilter)
        {
            Validate.NotNull(eventObserver, nameof(eventObserver));
            // isEventEnabledFilter may be null
            return null;
        }
    }

    public static class DiagnosticListening
    {
        public static DiagnosticSourceStub CreateNewSource(string diagnosticSourceName)
        {
            return null;
        }

        public static IDisposable SubscribeToAllSources(Action<DiagnosticSourceInfo> diagnosticSourceObserver)
        {
            Validate.NotNull(diagnosticSourceObserver, nameof(diagnosticSourceObserver));
            return null;
        }


    }
}

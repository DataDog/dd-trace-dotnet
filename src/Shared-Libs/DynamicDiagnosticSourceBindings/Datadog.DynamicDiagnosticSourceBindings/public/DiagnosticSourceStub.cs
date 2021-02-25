using System;
using System.Threading;
using StaticSystemDiagnostics = System.Diagnostics;

using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public class DiagnosticSourceStub : IDisposable
    {
        private int _isDisposed = 0;
        private readonly object _diagnosticSourceInstance;

        public DiagnosticSourceStub(object diagnosticSourceInstance)
        {
            _diagnosticSourceInstance = diagnosticSourceInstance;
        }

        public object DiagnosticListenerInstance
        {
            get { return _diagnosticSourceInstance; }
        }

        public void Dispose()
        {
            object diagnosticSourceInstance = _diagnosticSourceInstance;
            if (diagnosticSourceInstance != null)
            {
                int wasDisposed = Interlocked.Exchange(ref _isDisposed, 1);
                if (wasDisposed == 0 && diagnosticSourceInstance is IDisposable disposableDiagnosticSourceInstance)
                {
                    disposableDiagnosticSourceInstance.Dispose();
                }
            }
        }

        public bool TryAsDiagnosticListener(out DiagnosticListenerStub diagnosticListener)
        {
            return DiagnosticListenerStub.TryWrap(_diagnosticSourceInstance, out diagnosticListener);
        }

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

            return ((StaticSystemDiagnostics.DiagnosticSource) _diagnosticSourceInstance).IsEnabled(eventName, arg1, arg2);
        }

        public void Write(string eventName, object payloadValue)
        {
            Validate.NotNull(eventName, nameof(eventName));
            // payloadValue may be null

            ((StaticSystemDiagnostics.DiagnosticSource) _diagnosticSourceInstance).Write(eventName, payloadValue);
        }
    }
}

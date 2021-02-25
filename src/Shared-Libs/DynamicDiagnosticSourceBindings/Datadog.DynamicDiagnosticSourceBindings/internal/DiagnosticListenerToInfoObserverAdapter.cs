using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DiagnosticListenerToInfoObserverAdapter : IObserver<object>
    {
        private readonly Action<DiagnosticListenerStub> _diagnosticListenerObserver;

        public DiagnosticListenerToInfoObserverAdapter(Action<DiagnosticListenerStub> diagnosticListenerObserver)
        {
            _diagnosticListenerObserver = diagnosticListenerObserver;
        }

        public void OnNext(object diagnosticListener)
        {
            Action<DiagnosticListenerStub> diagnosticListenerObserver = _diagnosticListenerObserver;
            if (diagnosticListenerObserver != null)
            {
                if (!DiagnosticListenerStub.TryWrap(diagnosticListener, out DiagnosticListenerStub diagnosticListenerStub))
                {
                    // diagnosticListener must have the wrong type.
                    // This should never happen, as we slotted an IObserver<object> where an IObserver<DiagnosticListener> was expected.
                    // There must be a bug. Log an error and bail out.
                }
                else
                {
                    diagnosticListenerObserver(diagnosticListenerStub);
                }
            }
        }

        public void OnError(Exception error)
        {
            // This should never be invoked in practice. Log error so that we can debug.
        }

        public void OnCompleted()
        {
            // This should never be invoked in practice. Log error so that we can debug.
        }
    }
}
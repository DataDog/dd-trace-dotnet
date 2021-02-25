using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DiagnosticListenerToInfoObserverAdapter : IObserver<object>
    {
        private const string LogComonentMoniker = nameof(DiagnosticListenerToInfoObserverAdapter);

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
                    Log.Error(LogComonentMoniker,
                             $"Could not create a {nameof(DiagnosticListenerStub)} for the {nameof(diagnosticListener)}"
                           + $" instance passed into {nameof(OnNext)}(..). It must have the wrong runtime type."
                           + $" This should never happen becasue we use IObserver<object> where an IObserver<DiagnosticListener>"
                           + $" was expected, so the passed instance should always be of type 'DiagnosticListener'."
                           + $" {diagnosticListenerObserver} will not be invoked.",
                              "Actual Type",
                              diagnosticListener?.GetType()?.FullName);
                }
                else
                {
                    diagnosticListenerObserver(diagnosticListenerStub);
                }
            }
        }

        public void OnError(Exception error)
        {
            Log.Error(LogComonentMoniker, $"An exception was passed to the {nameof(OnError)}(..)-handler.", error);
        }

        public void OnCompleted()
        {
            Log.Error(LogComonentMoniker, $"The {nameof(OnCompleted)}(..)-handler was invoked. This was not expected and should be investogated");
        }
    }
}
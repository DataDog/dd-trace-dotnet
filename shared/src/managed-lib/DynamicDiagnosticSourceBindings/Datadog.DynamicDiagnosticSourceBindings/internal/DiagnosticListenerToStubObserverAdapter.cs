using System;
using System.Runtime.ExceptionServices;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DiagnosticListenerToStubObserverAdapter : IObserver<object>
    {
        private readonly IObserver<DiagnosticListenerStub> _diagnosticListenerObserver;

        public DiagnosticListenerToStubObserverAdapter(IObserver<DiagnosticListenerStub> diagnosticListenerObserver)
        {
            _diagnosticListenerObserver = diagnosticListenerObserver;
        }

        public void OnNext(object diagnosticListenerInstance)
        {
            if (_diagnosticListenerObserver == null)
            {
                return;
            }

            DiagnosticListenerStub diagnosticListenerStub;
            try
            {
                diagnosticListenerStub = DiagnosticListenerStub.Wrap(diagnosticListenerInstance);
            }
            catch(Exception ex)
            {
                Log.Error(ErrorUtil.DynamicInvokerLogComponentMoniker,
                         $" {nameof(_diagnosticListenerObserver)}.{nameof(OnNext)}(..) cannot be invoked:"
                       + $" Could not create a {nameof(DiagnosticListenerStub)} for the {nameof(diagnosticListenerInstance)}"
                       + $" instance passed into {nameof(OnNext)}(..). Does it have the wrong runtime type?"
                       + $" Such a type mismatch should never happen becasue we use IObserver<object> where an"
                       + $" IObserver<DiagnosticListener> was expected, so the passed instance should always be of type"
                       + $" 'DiagnosticListener'. See earlier logged error for detailed type info.",
                          ex);
                return;
            }

            try
            {
                _diagnosticListenerObserver.OnNext(diagnosticListenerStub);
            }
            catch (Exception ex)
            {
                throw LogAndRethrowEscapedError(ex, nameof(OnNext));
            }
        }

        public void OnError(Exception error)
        {
            if (_diagnosticListenerObserver != null)
            {
                try
                {
                    _diagnosticListenerObserver.OnError(error);
                }
                catch (Exception ex)
                {
                    throw LogAndRethrowEscapedError(ex, nameof(OnError));
                }
            }
        }

        public void OnCompleted()
        {
            if (_diagnosticListenerObserver != null)
            {
                try
                {
                    _diagnosticListenerObserver.OnCompleted();
                }
                catch (Exception ex)
                {
                    throw LogAndRethrowEscapedError(ex, nameof(OnCompleted));
                }
            }
        }

        private Exception LogAndRethrowEscapedError(Exception error, string observerMethodName)
        {
            Log.Error(ErrorUtil.DynamicInvokerLogComponentMoniker,
                    $"Exception escaped from the wrapped {nameof(_diagnosticListenerObserver)}."
                  + $" This {nameof(DiagnosticListenerToStubObserverAdapter)} will pass the exception through."
                  + $" This kind of error must be dealt with within the actual underlying observer.",
                     error,
                     "ObserverMethod",
                     observerMethodName,
                     "ObserverType",
                     _diagnosticListenerObserver.GetType().AssemblyQualifiedName);

            ExceptionDispatchInfo.Capture(error).Throw();
            return error;  // line never reached
        }
    }
}
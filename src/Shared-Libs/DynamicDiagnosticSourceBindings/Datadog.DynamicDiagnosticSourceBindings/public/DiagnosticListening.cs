using System;
using StaticSystemDiagnostics = System.Diagnostics;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public static class DiagnosticListening
    {
        public static DiagnosticSourceStub CreateNewSource(string diagnosticSourceName)
        {
            Validate.NotNull(diagnosticSourceName, nameof(diagnosticSourceName));

            object diagnosticSourceInstance = new StaticSystemDiagnostics.DiagnosticListener(diagnosticSourceName);
            var diagnosticSource = new DiagnosticSourceStub(diagnosticSourceInstance);
            return diagnosticSource;
        }

        public static IDisposable SubscribeToAllSources(Action<DiagnosticListenerStub> diagnosticSourceObserver)
        {
            Validate.NotNull(diagnosticSourceObserver, nameof(diagnosticSourceObserver));

            var observerAdapter = new DiagnosticListenerToInfoObserverAdapter(diagnosticSourceObserver);

            IDisposable allSourcesSubscription = StaticSystemDiagnostics.DiagnosticListener.AllListeners.Subscribe(observerAdapter);
            return allSourcesSubscription;
        }
    }
}
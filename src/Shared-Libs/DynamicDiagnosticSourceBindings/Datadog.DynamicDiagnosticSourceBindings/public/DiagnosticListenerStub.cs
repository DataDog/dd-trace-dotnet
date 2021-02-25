using System;
using StaticSystemDiagnostics = System.Diagnostics;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public struct DiagnosticListenerStub
    {
        private static class NoOpSingeltons
        {
            internal static readonly string Name = String.Empty;
            internal static readonly IDisposable EventSubscription = NoOpDisposable.SingeltonInstance;
            internal static readonly DiagnosticListenerStub DiagnosticListenerStub = new DiagnosticListenerStub(null);
        }

        public static DiagnosticListenerStub Wrap(object diagnosticListenerInstance)
        {
            if (diagnosticListenerInstance == null)
            {
                return NoOpSingeltons.DiagnosticListenerStub;
            }

            if (diagnosticListenerInstance is StaticSystemDiagnostics.DiagnosticListener)
            {
                return new DiagnosticListenerStub(diagnosticListenerInstance);
            }

            Type actualInstanceType = diagnosticListenerInstance.GetType();
            throw new ArgumentException($"{nameof(diagnosticListenerInstance)} has an unexpected runtime type:"
                                      + $" {actualInstanceType.Name} ({actualInstanceType.AssemblyQualifiedName})");
        }

        internal static bool TryWrap(object diagnosticListenerInstance, out DiagnosticListenerStub diagnosticListenerStub)
        {
            if (diagnosticListenerInstance == null)
            {
                diagnosticListenerStub = NoOpSingeltons.DiagnosticListenerStub;
                return true;
            }

            if (diagnosticListenerInstance is StaticSystemDiagnostics.DiagnosticListener)
            {
                diagnosticListenerStub = new DiagnosticListenerStub(diagnosticListenerInstance);
                return true;
            }
            else
            {
                diagnosticListenerStub = NoOpSingeltons.DiagnosticListenerStub;
                return false;
            }
        }

        private readonly object _diagnosticListenerInstance;

        private DiagnosticListenerStub(object diagnosticListenerInstance)
        {
            _diagnosticListenerInstance = diagnosticListenerInstance;
        }

        public object DiagnosticListenerInstance { get { return _diagnosticListenerInstance; } }

        public bool IsNoOpStub { get { return (_diagnosticListenerInstance == null); } }

        public string Name
        {
            get
            {
                if (_diagnosticListenerInstance == null)
                {
                    return NoOpSingeltons.Name;
                }

                return ((StaticSystemDiagnostics.DiagnosticListener) _diagnosticListenerInstance).Name;
            }
        }

        public IDisposable SubscribeToEvents(Action<string, object> eventObserver, Func<string, object, object, bool> isEventEnabledFilter)
        {
            if (_diagnosticListenerInstance == null)
            {
                return NoOpSingeltons.EventSubscription;
            }

            Validate.NotNull(eventObserver, nameof(eventObserver));
            // isEventEnabledFilter may be null

            IDisposable eventsSubscription = ((StaticSystemDiagnostics.DiagnosticListener) _diagnosticListenerInstance).Subscribe(
                            new EventObserverAdapter(eventObserver),
                            isEventEnabledFilter);
            return eventsSubscription;
        }
    }
}

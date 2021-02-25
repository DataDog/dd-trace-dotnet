using System;
using StaticSystemDiagnostics = System.Diagnostics;
using Datadog.Util;
using System.Threading;
using System.Collections.Generic;

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

    internal class EventObserverAdapter : IObserver<KeyValuePair<string, object>>
    {
        private readonly Action<string, object> _eventObserver;

        public EventObserverAdapter(Action<string, object> eventObserver)
        {
            _eventObserver = eventObserver;
        }

        public void OnNext(KeyValuePair<string, object> eventInfo)
        {
            Action<string, object> eventObserver = _eventObserver;
            if (eventObserver != null)
            {
                eventObserver(eventInfo.Key, eventInfo.Value);
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

    internal sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable SingeltonInstance = new NoOpDisposable();

        public void Dispose()
        {
        }
    }

    public interface IInvokerHandle
    {
        bool IsValid { get; }
    }


    internal abstract class InvokerHandle<T> : IInvokerHandle where T : class
    {
        private protected abstract T Invoker { get; }

        public bool TryGetInvoker(out T invoker)
        {
            invoker = Invoker;
            return (invoker != null);
        }

        public bool IsValid 
        {
            get { return (Invoker != null); }
        }
    }

    internal class DynamicDiagnosticListenerInvoker
    {
        private class Handle : InvokerHandle<DynamicDiagnosticListenerInvoker>
        {
            private DynamicDiagnosticListenerInvoker _invoker;
            private protected override DynamicDiagnosticListenerInvoker Invoker
            { 
                get { return _invoker; }
            }
        }

        private readonly Handle _handle;

        public DynamicDiagnosticListenerInvoker()
        {
            _handle = new Handle();
        }
    }


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

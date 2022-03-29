using System;
using System.Threading;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvoker : DiagnosticSourceAssembly.IDynamicInvoker
    {
#region Static API
        private class InitializationListenersCollection : ListenerActionsCollection<Action<DiagnosticSourceAssembly.IDynamicInvoker, object>,
                                                                                    DiagnosticSourceAssembly.IDynamicInvoker>
        {
            public InitializationListenersCollection()
                : base(nameof(DynamicInvoker))
            { }

            protected override void InvokeSubscription(Subscription subscription, DiagnosticSourceAssembly.IDynamicInvoker source)
            {
                subscription.Action(source, subscription.State);
            }

            protected override bool GetMustImmediatelyInvokeNewSubscription(Subscription subscription, out DiagnosticSourceAssembly.IDynamicInvoker source)
            {
                if (TryGetCurrent(out DynamicInvoker invoker) && invoker.IsValid)
                {
                    source = invoker;
                    return true;
                }

                source = null;
                return false;
            }
        }

        private static DynamicInvoker s_currentInvoker = null;
        private static readonly InitializationListenersCollection s_initializationListenersCollection = new InitializationListenersCollection();

        public static DynamicInvoker Current
        {
            get
            {
                DynamicInvoker invoker = Volatile.Read(ref s_currentInvoker);
                if (invoker == null)
                {
                    if (!DynamicLoader.EnsureInitialized())
                    {
                        throw new InvalidOperationException($"Cannot obtain a {nameof(Current)} {nameof(DynamicInvoker)}:"
                                                          + $" The {nameof(DynamicLoader)} cannot initialize.");
                    }

                    invoker = Volatile.Read(ref s_currentInvoker);
                    if (invoker == null)
                    {
                        throw new InvalidOperationException($"Cannot obtain a {nameof(Current)} {nameof(DynamicInvoker)}:"
                                                          + $" The {nameof(DynamicLoader)} was initialized, but the invoker is still null.");
                    }
                }

                return invoker;
            }

            internal set
            {
                DynamicInvoker prevInvoker = Interlocked.Exchange(ref s_currentInvoker, value);

                // If the previous invoker and the new one are exactly the same object, we will just continue using it.
                if (Object.ReferenceEquals(prevInvoker, value))
                {
                    return;
                }

                if (prevInvoker != null)
                {
                    prevInvoker.Invalidate();
                }

                if (value != null)
                {
                    NotifyInitializationListeners(value);
                }
            }
        }

        public static bool TryGetCurrent(out DynamicInvoker currentInvoker)
        {
            currentInvoker = Volatile.Read(ref s_currentInvoker);
            return (currentInvoker != null);
        }

        internal static IDisposable SubscribeInitializedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker, object> dynamicInvokerInitializedAction, object state)
        {
            return s_initializationListenersCollection.SubscribeListener(dynamicInvokerInitializedAction, state);
        }

        private static void NotifyInitializationListeners(DiagnosticSourceAssembly.IDynamicInvoker initializedInvoker)
        {
            s_initializationListenersCollection.InvokeAll(initializedInvoker);
        }

#endregion Static API

        private readonly string _diagnosticSourceAssemblyName;
        private readonly DynamicInvoker_DiagnosticSource _diagnosticSourceInvoker;
        private readonly DynamicInvoker_DiagnosticListener _diagnosticListenerInvoker;

        private readonly DynamicInvokerInvalidationListenersCollection _invalidationListeners;

        private int _isValid;

        public DynamicInvoker(string diagnosticSourceAssemblyName, Type diagnosticSourceType, Type diagnosticListenerType)
        {
            Validate.NotNull(diagnosticSourceAssemblyName, nameof(diagnosticSourceAssemblyName));

            _diagnosticSourceAssemblyName = diagnosticSourceAssemblyName;
            _diagnosticSourceInvoker = new DynamicInvoker_DiagnosticSource(diagnosticSourceType);
            _diagnosticListenerInvoker = new DynamicInvoker_DiagnosticListener(diagnosticListenerType);

            _invalidationListeners = new DynamicInvokerInvalidationListenersCollection(nameof(DynamicInvoker), this);

            _isValid = 1;
        }

        public DynamicInvoker_DiagnosticSource DiagnosticSource
        {
            get { return _diagnosticSourceInvoker; }
        }

        public DynamicInvoker_DiagnosticListener DiagnosticListener
        {
            get { return _diagnosticListenerInvoker; }
        }

        public bool IsValid
        {
            get { return (Interlocked.Add(ref _isValid, 0) > 0); }
        }

        public string DiagnosticSourceAssemblyName
        {
            get { return _diagnosticSourceAssemblyName; }
        }

        private void Invalidate()
        {
            int wasValid = Interlocked.Exchange(ref _isValid, 0);

            if (wasValid == 1)
            {
                _diagnosticSourceInvoker.Handle.Invalidate();
                _diagnosticListenerInvoker.Handle.Invalidate();
                _invalidationListeners.InvokeAndClearAll(this);
            }
        }

        public IDisposable SubscribeInvalidatedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker> invokerInvalidatedAction)
        {
            Validate.NotNull(invokerInvalidatedAction, nameof(invokerInvalidatedAction));

            return SubscribeInvalidatedListener((invoker, _) => invokerInvalidatedAction(invoker), state: null);
        }

        public IDisposable SubscribeInvalidatedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker, object> invokerInvalidatedAction, object state)
        {
            Validate.NotNull(invokerInvalidatedAction, nameof(invokerInvalidatedAction));

            return _invalidationListeners.SubscribeListener(invokerInvalidatedAction, state);
        }
    }
}

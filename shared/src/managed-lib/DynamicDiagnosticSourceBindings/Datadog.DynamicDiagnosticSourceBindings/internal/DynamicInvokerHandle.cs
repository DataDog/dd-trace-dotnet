using Datadog.Util;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvokerHandle<T> : DiagnosticSourceAssembly.IDynamicInvoker
        where T : class, DiagnosticSourceAssembly.IDynamicInvoker
    {
        private T _dynamicInvoker;
        private readonly DynamicInvokerInvalidationListenersCollection _invalidationListeners;

        internal DynamicInvokerHandle(T dynamicInvoker)
        {
            Volatile.Write(ref _dynamicInvoker, dynamicInvoker);
            _invalidationListeners = new DynamicInvokerInvalidationListenersCollection($"{nameof(DynamicInvokerHandle<T>)}<{typeof(T).Name}>",
                                                                                       this);
        }

        public bool IsValid
        {
            get { return (Volatile.Read(ref _dynamicInvoker) != null); }
        }

        public string DiagnosticSourceAssemblyName
        {
            get
            {
                if (TryGetInvoker(out T invoker))
                {
                    return invoker.DiagnosticSourceAssemblyName;
                }
                
                return null;
            }
        }

        internal void Invalidate()
        { 
            T invalidatedInvoker = Interlocked.Exchange(ref _dynamicInvoker, null);

            if (invalidatedInvoker != null)  // call listeners no more than once.
            {
                _invalidationListeners.InvokeAndClearAll(this);
            }
        }

        public bool TryGetInvoker(out T dynamicInvoker)
        {
            dynamicInvoker = Volatile.Read(ref _dynamicInvoker);
            return (dynamicInvoker != null);
        }

        public T GetInvoker()
        {
            if (TryGetInvoker(out T dynamicInvoker))
            {
                return dynamicInvoker;
            }

            throw new DynamicInvocationException(typeof(T),
                                                $"Cannot {nameof(GetInvoker)}() because this {nameof(DynamicInvokerHandle<T>)} is invalid."
                                               + " Was the underlying assembly unloaded or reloaded?");
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

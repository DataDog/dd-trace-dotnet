using Datadog.Util;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvokerHandle<T> : IDynamicInvokerHandle
        where T : class
    {
        private T _dynamicInvoker;
        private readonly List<Action<T>> _invalidationListeners;

        internal DynamicInvokerHandle(T dynamicInvoker)
        {
            Volatile.Write(ref _dynamicInvoker, dynamicInvoker);
            _invalidationListeners = new List<Action<T>>(capacity: 0);
        }

        internal void Invalidate()
        { 
            T invalidatedInvoker = Interlocked.Exchange(ref _dynamicInvoker, null);

            lock (_invalidationListeners)
            {
                if (invalidatedInvoker != null)  // call listeners no more than once.
                {
                    while(_invalidationListeners.Count > 0)
                    {
                        Action<T> invokerInvalidationListenerAction = _invalidationListeners[_invalidationListeners.Count - 1];
                        _invalidationListeners.RemoveAt(_invalidationListeners.Count - 1);
                        if (invokerInvalidationListenerAction != null)
                        {
                            try
                            {
                                invokerInvalidationListenerAction(invalidatedInvoker);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(this.GetType().Name, "Error calling a dynamic invoker invalidation listener", ex);
                            }
                        }
                    }
                }
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

        public bool IsValid
        {
            get { return (Volatile.Read(ref _dynamicInvoker) != null); }
        }

        public void AddInvalidationListener(Action<T> invokerInvalidatedAction)
        {
            Validate.NotNull(invokerInvalidatedAction, nameof(invokerInvalidatedAction));

            lock (_invalidationListeners)
            {
                if (! IsValid)
                {
                    invokerInvalidatedAction(null);
                }
                else
                {
                    _invalidationListeners.Add(invokerInvalidatedAction);
                }
            }
        }

        public void RemoveInvalidationListener(Action<T> invokerInvalidatedAction)
        {
            Validate.NotNull(invokerInvalidatedAction, nameof(invokerInvalidatedAction));

            lock (_invalidationListeners)
            {
                _invalidationListeners.Remove(invokerInvalidatedAction);
            }
        }
    }
}

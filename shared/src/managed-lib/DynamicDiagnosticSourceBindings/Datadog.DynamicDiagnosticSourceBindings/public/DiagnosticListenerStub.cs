using System;
using System.Collections.Generic;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public struct DiagnosticListenerStub
    {
#region Static APIs
        private static class NoOpSingeltons
        {
            internal static readonly string Name = String.Empty;
            internal static readonly IDisposable EventSubscription = Disposables.NoOp.SingeltonInstance;
            internal static readonly DiagnosticListenerStub DiagnosticListenerStub = new DiagnosticListenerStub(null, new DynamicInvokerHandle<DynamicInvoker_DiagnosticListener>(null));
        }

        public static DiagnosticListenerStub NoOpStub
        {
            get { return NoOpSingeltons.DiagnosticListenerStub; }
        }

        public static DiagnosticListenerStub Wrap(object diagnosticListenerInstance)
        {
            if (diagnosticListenerInstance == null)
            {
                return NoOpSingeltons.DiagnosticListenerStub;
            }

            DynamicInvoker_DiagnosticListener invoker = null;
            try
            {
                invoker = DynamicInvoker.Current.DiagnosticListener;
                DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> handle = invoker.GetInvokerHandleForInstance(diagnosticListenerInstance);
                return new DiagnosticListenerStub(diagnosticListenerInstance, handle);
            }
            catch(Exception ex)
            {
                throw ErrorUtil.LogAndRethrowStubInvocationError(ErrorUtil.CannotCreateStubMsg,
                                                                 ex,
                                                                 typeof(DynamicInvoker_DiagnosticListener),
                                                                 invoker?.TargetType,
                                                                 diagnosticListenerInstance);
            }
        }

        internal static bool TryWrap(object diagnosticListenerInstance, out DiagnosticListenerStub diagnosticListenerStub)
        {
            if (diagnosticListenerInstance == null)
            {
                diagnosticListenerStub = NoOpSingeltons.DiagnosticListenerStub;
                return true;
            }

            DynamicInvoker_DiagnosticListener invoker = DynamicInvoker.Current.DiagnosticListener;
            if (invoker != null && invoker.TryGetInvokerHandleForInstance(diagnosticListenerInstance, out DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> handle))
            {
                diagnosticListenerStub = new DiagnosticListenerStub(diagnosticListenerInstance, handle);
                return true;
            }
            else
            {
                diagnosticListenerStub = NoOpSingeltons.DiagnosticListenerStub;
                return false;
            }
        }
#endregion Static APIs

        private readonly object _diagnosticListenerInstance;
        private readonly DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> _dynamicInvokerHandle;

        private DiagnosticListenerStub(object diagnosticListenerInstance, DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> dynamicInvokerHandle)
        {
            Validate.NotNull(dynamicInvokerHandle, nameof(dynamicInvokerHandle));

            _diagnosticListenerInstance = diagnosticListenerInstance;
            _dynamicInvokerHandle = dynamicInvokerHandle;
        }

        public object DiagnosticListenerInstance
        {
            get { return _diagnosticListenerInstance; }
        }

        public DiagnosticSourceAssembly.IDynamicInvoker DynamicInvokerHandle
        {
            get { return _dynamicInvokerHandle; }
        }

        public bool IsNoOpStub
        {
            get { return (_diagnosticListenerInstance == null); }
        }

        public string Name
        {
            get
            {
                if (_diagnosticListenerInstance == null)
                {
                    return NoOpSingeltons.Name;
                }

                DynamicInvoker_DiagnosticListener invoker = null;
                try
                {
                    invoker = _dynamicInvokerHandle.GetInvoker();
                    return invoker.Call.get_Name(_diagnosticListenerInstance);
                }
                catch(Exception ex)
                {
                    throw LogAndRethrowStubInvocationError(ex, nameof(DynamicInvoker_DiagnosticListener.StubbedApis.get_Name), isStaticApi: false, invoker?.TargetType);
                }
            }
        }

        /// <summary>
        /// In addition to forwarding all calls from the underlying Diagnostic Listener to the specified <c>eventObserver</c>,
        /// the <c>eventObserver</c>'s <c>OnCompleted()</c> handler will be called if the DiagnosticSource-assembly is unloaded
        /// or dynamically swapped for another version. This allows subscribers to know that no more events are coming.
        /// To handle such case, it is recommended to give up on the existing subscription and to schedule a from-scratch
        /// re-subscription on a timer after a short time period. Such delay ensures that the new assembly version is loaded by then.
        /// Notably, a few events will be lost. This is an explicit design decision in the context of the fact that assembly
        /// unloads are extremely rare.
        /// If the IDisposable returned by this method is disposed, then the above-described notification will not be delivered.
        /// </summary>
        /// <remarks>
        /// Consider using <c>Datadog.Util.ObserverAdapter</c> in shared sources to conveniently create observers suitable for this API.
        /// </remarks>
        public IDisposable SubscribeToEvents(IObserver<KeyValuePair<string, object>> eventObserver, Func<string, object, object, bool> isEventEnabledFilter)
        {
            if (_diagnosticListenerInstance == null)
            {
                return NoOpSingeltons.EventSubscription;
            }

            Validate.NotNull(eventObserver, nameof(eventObserver));
            // isEventEnabledFilter may be null

            DynamicInvoker_DiagnosticListener invoker = null;
            try
            {
                invoker = _dynamicInvokerHandle.GetInvoker();
                IDisposable eventsSubscription = invoker.Call.Subscribe(_diagnosticListenerInstance, eventObserver, isEventEnabledFilter);
                
                Action<DiagnosticSourceAssembly.IDynamicInvoker> invokerInvalidatedAction = (invkr) =>
                    {
                        // DiagnosticListener does NOT call OnCompleted when the subscription is disposed. So we DO need to call it here:
                        eventObserver.OnCompleted();
                        eventsSubscription.Dispose();
                    };

                DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> invokerHandle = _dynamicInvokerHandle;
                IDisposable invokerInvalidatedActionSub = invokerHandle.SubscribeInvalidatedListener(invokerInvalidatedAction);

                IDisposable eventsSubscriptionWrapper = new Disposables.Action(() =>
                    {
                        invokerInvalidatedActionSub.Dispose();
                        eventsSubscription.Dispose();
                    });

                return eventsSubscriptionWrapper;
            }
            catch(Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, nameof(DynamicInvoker_DiagnosticListener.StubbedApis.Subscribe), isStaticApi: false, invoker?.TargetType);
            }
        }

        private Exception LogAndRethrowStubInvocationError(Exception error, string invokedApiName, bool isStaticApi, Type invokerTargetType)
        {
            return ErrorUtil.LogAndRethrowStubInvocationError(ErrorUtil.ErrorInvokingStubbedApiMsg,
                                                              error,
                                                              typeof(DynamicInvoker_DiagnosticListener),
                                                              invokedApiName,
                                                              isStaticApi,
                                                              invokerTargetType,
                                                              _diagnosticListenerInstance);
        }
    }
}

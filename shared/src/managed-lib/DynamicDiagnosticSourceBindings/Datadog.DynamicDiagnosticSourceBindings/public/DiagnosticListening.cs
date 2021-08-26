using System;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public static class DiagnosticListening
    {
        public static DiagnosticSourceStub CreateNewSource(string diagnosticSourceName)
        {
            Validate.NotNull(diagnosticSourceName, nameof(diagnosticSourceName));

            DynamicInvoker_DiagnosticListener invoker = null;
            try
            {
                invoker = DynamicInvoker.Current.DiagnosticListener;
                object diagnosticListenerInstance = invoker.Call.Ctor(diagnosticSourceName);
                return DiagnosticSourceStub.Wrap(diagnosticListenerInstance);
            }
            catch (Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, invoker?.GetType(), nameof(DynamicInvoker_DiagnosticListener.StubbedApis.Ctor), invoker?.TargetType);
            }
        }

        /// <summary>
        /// In addition to forwarding all calls from the underlying Diagnostic Source collection to the specified <c>diagnosticSourcesObserver</c>,
        /// the <c>diagnosticSourcesObserver</c>'s <c>OnCompleted()</c> handler will be called if the DiagnosticSource-assembly is unloaded
        /// or dynamically swapped for another version. This allows subscribers to know that no more events are coming.
        /// To handle such case, it is recommended to give up on the existing subscription and to schedule a from-scratch
        /// re-subscription on a timer after a short time period. Such delay ensures that the new assembly version is loaded by then.
        /// Notably, a few events will be lost. This is an explicit design decision in the context of the fact that assembly
        /// unloads are extremely rare.
        /// If the IDisposable returned by this method is disposed, then the above-described notification will not be delivered.
        /// </summary>
        /// <remarks>Consider using <c>Datadog.Util.ObserverAdapter</c> in shared sources to conveniently create observers suitable for this API.</remarks>
        public static IDisposable SubscribeToAllSources(IObserver<DiagnosticListenerStub> diagnosticSourcesObserver)
        {
            Validate.NotNull(diagnosticSourcesObserver, nameof(diagnosticSourcesObserver));

            DynamicInvoker_DiagnosticListener invoker = null;
            try
            {
                invoker = DynamicInvoker.Current.DiagnosticListener;
                IObservable<object> allSourcesObservable = invoker.Call.get_AllListeners();

                IObserver<object> observerAdapter = new DiagnosticListenerToStubObserverAdapter(diagnosticSourcesObserver);
                IDisposable dsSubscription = allSourcesObservable.Subscribe(observerAdapter);

                Action<DiagnosticSourceAssembly.IDynamicInvoker> invokerInvalidatedAction = (invkr) =>
                    {
                        // DiagnosticListener calls OnCompleted when the subscription is disposed. So we do NOT need to call it here:
                        // observerAdapter.OnCompleted();
                        dsSubscription.Dispose();
                    };

                DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> invokerHandle = invoker.Handle;
                IDisposable invokerInvalidatedActionSub = invokerHandle.SubscribeInvalidatedListener(invokerInvalidatedAction);

                IDisposable dsSubscriptionWrapper = new Disposables.Action(() =>
                    {
                        invokerInvalidatedActionSub.Dispose();
                        dsSubscription.Dispose();
                    });

                return dsSubscriptionWrapper;
            }
            catch (Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, invoker?.GetType(), nameof(DynamicInvoker_DiagnosticListener.StubbedApis.get_AllListeners), invoker?.TargetType);
            }
        }

        private static Exception LogAndRethrowStubInvocationError(Exception error, Type dynamicInvokerType, string invokedApiName, Type invokerTargetType)
        {
            return ErrorUtil.LogAndRethrowStubInvocationError(ErrorUtil.ErrorInvokingStubbedApiMsg,
                                                              error,
                                                              dynamicInvokerType,
                                                              invokedApiName,
                                                              isStaticApi: true,
                                                              invokerTargetType,
                                                              null);
        }
    }
}
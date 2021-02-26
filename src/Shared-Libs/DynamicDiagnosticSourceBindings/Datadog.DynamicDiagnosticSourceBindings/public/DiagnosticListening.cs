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
                invoker = DynamicInvoker.DiagnosticListener;
                object diagnosticListenerInstance = invoker.Call.Ctor(diagnosticSourceName);
                return DiagnosticSourceStub.Wrap(diagnosticListenerInstance);
            }
            catch (Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, invoker.GetType(), nameof(DynamicInvoker_DiagnosticListener.StubbedApis.Ctor), invoker?.TargetType);
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
        /// <br />
        /// Consider using <c>Datadog.Util.ObserverAdapter</c> in shared sources to conveniently create observers suitable for this API.
        /// </summary>
        public static IDisposable SubscribeToAllSources(IObserver<DiagnosticListenerStub> diagnosticSourcesObserver)
        {
            Validate.NotNull(diagnosticSourcesObserver, nameof(diagnosticSourcesObserver));

            DynamicInvoker_DiagnosticListener invoker = null;
            try
            {
                invoker = DynamicInvoker.DiagnosticListener;
                IObservable<object> allSourcesObservable = invoker.Call.get_AllListeners();

                IObserver<object> observerAdapter = new DiagnosticListenerToInfoObserverAdapter(diagnosticSourcesObserver);
                IDisposable dsSubscription = allSourcesObservable.Subscribe(observerAdapter);

                Action<DynamicInvoker_DiagnosticListener> invokerInvalidatedAction = (invkr) => observerAdapter.OnCompleted();

                DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> invokerHandle = invoker.Handle;
                invokerHandle.AddInvalidationListener(invokerInvalidatedAction);

                IDisposable dsSubscriptionnWrapper = new Disposables.Action(() =>
                {
                    try
                    {
                        dsSubscription?.Dispose();
                    }
                    finally
                    {
                        invokerHandle.RemoveInvalidationListener(invokerInvalidatedAction);
                    }
                });

                return dsSubscriptionnWrapper;
            }
            catch (Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, invoker.GetType(), nameof(DynamicInvoker_DiagnosticListener.StubbedApis.get_AllListeners), invoker?.TargetType);
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
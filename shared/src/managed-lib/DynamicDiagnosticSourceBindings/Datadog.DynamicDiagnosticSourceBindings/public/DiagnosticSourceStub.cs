using System;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public struct DiagnosticSourceStub : IDisposable
    {
#region Static APIs
        private static class NoOpSingeltons
        {
            internal static bool IsEnabled = false;
            internal static readonly DiagnosticSourceStub DiagnosticSourceStub = new DiagnosticSourceStub(null, new DynamicInvokerHandle<DynamicInvoker_DiagnosticSource>(null));
        }

        public static DiagnosticSourceStub NoOpStub
        {
            get { return NoOpSingeltons.DiagnosticSourceStub; }
        }

        public static DiagnosticSourceStub Wrap(object diagnosticSourceInstance)
        {
            if (diagnosticSourceInstance == null)
            {
                return NoOpSingeltons.DiagnosticSourceStub;
            }

            DynamicInvoker_DiagnosticSource invoker = null;
            try
            {
                invoker = DynamicInvoker.Current.DiagnosticSource;
                DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> handle = invoker.GetInvokerHandleForInstance(diagnosticSourceInstance);
                return new DiagnosticSourceStub(diagnosticSourceInstance, handle);
            }
            catch (Exception ex)
            {
                throw ErrorUtil.LogAndRethrowStubInvocationError(ErrorUtil.CannotCreateStubMsg,
                                                                 ex,
                                                                 typeof(DynamicInvoker_DiagnosticSource),
                                                                 invoker?.TargetType,
                                                                 diagnosticSourceInstance);
            }
        }

        internal static bool TryWrap(object diagnosticSourceInstance, out DiagnosticSourceStub diagnosticSourceStub)
        {
            if (diagnosticSourceInstance == null)
            {
                diagnosticSourceStub = NoOpSingeltons.DiagnosticSourceStub;
                return true;
            }

            DynamicInvoker_DiagnosticSource invoker = DynamicInvoker.Current.DiagnosticSource;
            if (invoker != null && invoker.TryGetInvokerHandleForInstance(diagnosticSourceInstance, out DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> handle))
            {
                diagnosticSourceStub = new DiagnosticSourceStub(diagnosticSourceInstance, handle);
                return true;
            }
            else
            {
                diagnosticSourceStub = NoOpSingeltons.DiagnosticSourceStub;
                return false;
            }
            
        }
        #endregion Static APIs

        private readonly object _diagnosticSourceInstance;
        private readonly DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> _dynamicInvokerHandle;

        private DiagnosticSourceStub(object diagnosticSourceInstance, DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> dynamicInvokerHandle)
        {
            Validate.NotNull(dynamicInvokerHandle, nameof(dynamicInvokerHandle));

            _diagnosticSourceInstance = diagnosticSourceInstance;
            _dynamicInvokerHandle = dynamicInvokerHandle;
        }

        public object DiagnosticSourceInstance
        {
            get { return _diagnosticSourceInstance; }
        }

        public DiagnosticSourceAssembly.IDynamicInvoker DynamicInvokerHandle
        {
            get { return _dynamicInvokerHandle; }
        }

        public bool IsNoOpStub
        {
            get { return (_diagnosticSourceInstance == null); }
        }

        public void Dispose()
        {
            if (_diagnosticSourceInstance == null)
            {
                return;
            }

            if (_diagnosticSourceInstance is IDisposable disposableDiagnosticSourceInstance)
            {
                disposableDiagnosticSourceInstance.Dispose();
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

            if (_diagnosticSourceInstance == null)
            {
                return NoOpSingeltons.IsEnabled;
            }

            DynamicInvoker_DiagnosticSource invoker = null;
            try
            {
                invoker = _dynamicInvokerHandle.GetInvoker();
                return invoker.Call.IsEnabled(_diagnosticSourceInstance, eventName, arg1, arg2);
            }
            catch (Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, nameof(DynamicInvoker_DiagnosticSource.StubbedApis.IsEnabled), isStaticApi: false, invoker?.TargetType);
            }
        }

        public void Write(string eventName, object payloadValue)
        {
            Validate.NotNull(eventName, nameof(eventName));
            // payloadValue may be null

            if (_diagnosticSourceInstance == null)
            {
                return;
            }

            DynamicInvoker_DiagnosticSource invoker = null;
            try
            {
                invoker = _dynamicInvokerHandle.GetInvoker();
                invoker.Call.Write(_diagnosticSourceInstance, eventName, payloadValue);
            }
            catch (Exception ex)
            {
                throw LogAndRethrowStubInvocationError(ex, nameof(DynamicInvoker_DiagnosticSource.StubbedApis.Write), isStaticApi: false, invoker?.TargetType);
            }
        }

        private Exception LogAndRethrowStubInvocationError(Exception error, string invokedApiName, bool isStaticApi, Type invokerTargetType)
        {
            return ErrorUtil.LogAndRethrowStubInvocationError(ErrorUtil.ErrorInvokingStubbedApiMsg,
                                                              error,
                                                              typeof(DynamicInvoker_DiagnosticSource),
                                                              invokedApiName,
                                                              isStaticApi,
                                                              invokerTargetType,
                                                              _diagnosticSourceInstance);
        }
    }
}

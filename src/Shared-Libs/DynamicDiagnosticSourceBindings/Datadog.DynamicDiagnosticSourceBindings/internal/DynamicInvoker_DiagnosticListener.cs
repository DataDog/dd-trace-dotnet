using Datadog.Util;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using StaticSystemDiagnostics = System.Diagnostics;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvoker_DiagnosticListener
    {
        private const string LogComponentMoniker = nameof(DynamicInvoker_DiagnosticListener);

        private readonly StubbedApis _stubbedApis;
        private readonly Type _diagnosticListenerType;
        private readonly DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> _handle;

        public DynamicInvoker_DiagnosticListener(Type diagnosticListenerType)
        {
            Validate.NotNull(diagnosticListenerType, nameof(diagnosticListenerType));

            _diagnosticListenerType = diagnosticListenerType;
            _handle = new DynamicInvokerHandle<DynamicInvoker_DiagnosticListener>(this);
            _stubbedApis = new StubbedApis(this);
        }

        public Type TargetType
        {
            get { return _diagnosticListenerType; }
        }

        public StubbedApis Call
        {
            get { return _stubbedApis; }
        }

        public bool TryGetInvokerHandleForInstance(object diagnosticListenerInstance, out DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> handle)
        {
            Validate.NotNull(diagnosticListenerInstance, nameof(diagnosticListenerInstance));

            Type actualType = diagnosticListenerInstance.GetType();

            if (_diagnosticListenerType == actualType || _diagnosticListenerType.Equals(actualType))
            {
                handle = _handle;
                return true;
            }

            // Is IsSubclassOf(..) too restrictive? Consider using 'if (_diagnosticListenerType.IsAssignableFrom(actualType)) {..}' instead.
            if (actualType.IsSubclassOf(_diagnosticListenerType))
            {
                handle = _handle;
                return true;
            }

            handle = null;
            return false;
        }

        public DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> GetInvokerHandleForInstance(object diagnosticListenerInstance)
        {
            if (TryGetInvokerHandleForInstance(diagnosticListenerInstance, out DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> handle))
            {
                return handle;
            }
            else
            {
                throw new ArgumentException($"The specified {nameof(diagnosticListenerInstance)} is expected to be of type"
                                          + $" \"{_diagnosticListenerType.FullName}\" or of a compatible subtype,"
                                          + $" however, the actual runtime type is \"{diagnosticListenerInstance.GetType().FullName}\"."
                                          + $" Additional details and type info will be written to the log");
            }
        }

        /// <summary>Just syntax sugar for invocations (aka: 'string result = invoker.Call.get_Name(diagnosticListenerInstance);'</summary>
        public class StubbedApis
        {
            private readonly DynamicInvoker_DiagnosticListener _thisInvoker;

            internal StubbedApis(DynamicInvoker_DiagnosticListener thisInvoker)
            {
                _thisInvoker = thisInvoker;
            }

            public string get_Name(object diagnosticListenerInstance)
            {
                return ((StaticSystemDiagnostics.DiagnosticListener) diagnosticListenerInstance).Name;
            }

            public IDisposable Subscribe(object diagnosticListenerInstance,
                                         IObserver<KeyValuePair<string, object>> eventObserver,
                                         Func<string, object, object, bool> isEventEnabledFilter)
            {
                return ((StaticSystemDiagnostics.DiagnosticListener) diagnosticListenerInstance).Subscribe(eventObserver, isEventEnabledFilter);
            }
        }
    }
}

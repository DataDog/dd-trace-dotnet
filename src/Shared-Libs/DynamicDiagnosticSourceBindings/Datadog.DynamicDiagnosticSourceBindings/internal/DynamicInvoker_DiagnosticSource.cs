using Datadog.Util;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using StaticSystemDiagnostics = System.Diagnostics;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvoker_DiagnosticSource
    {
        private const string LogComponentMoniker = nameof(DynamicInvoker_DiagnosticSource);

        private readonly StubbedApis _stubbedApis;
        private readonly Type _diagnosticSourceType;
        private readonly DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> _handle;

        public DynamicInvoker_DiagnosticSource(Type diagnosticSourceType)
        {
            Validate.NotNull(diagnosticSourceType, nameof(diagnosticSourceType));

            _diagnosticSourceType = diagnosticSourceType;
            _handle = new DynamicInvokerHandle<DynamicInvoker_DiagnosticSource>(this);
            _stubbedApis = new StubbedApis(this);
        }

        public Type TargetType
        {
            get { return _diagnosticSourceType; }
        }

        public StubbedApis Call
        {
            get { return _stubbedApis; }
        }

        public bool TryGetInvokerHandleForInstance(object diagnosticSourceInstance, out DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> handle)
        {
            Validate.NotNull(diagnosticSourceInstance, nameof(diagnosticSourceInstance));

            Type actualType = diagnosticSourceInstance.GetType();

            if (_diagnosticSourceType == actualType || _diagnosticSourceType.Equals(actualType))
            {
                handle = _handle;
                return true;
            }

            // Is IsSubclassOf(..) too restrictive? Consider using 'if (_diagnosticSourceType.IsAssignableFrom(actualType)) {..}' instead.
            if (actualType.IsSubclassOf(_diagnosticSourceType))
            {
                handle = _handle;
                return true;
            }

            handle = null;
            return false;
        }

        public DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> GetInvokerHandleForInstance(object diagnosticSourceInstance)
        {
            if (TryGetInvokerHandleForInstance(diagnosticSourceInstance, out DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> handle))
            {
                return handle;
            }
            else
            {
                throw new ArgumentException($"The specified {nameof(diagnosticSourceInstance)} is expected to be of type"
                                          + $" \"{_diagnosticSourceType.FullName}\" or of a compatible subtype,"
                                          + $" however, the actual runtime type is \"{diagnosticSourceInstance.GetType().FullName}\"."
                                          + $" Additional details and type info will be written to the log");
            }
        }

        /// <summary>Just syntax sugar for invocations (aka: 'int result = invoker.TryCall.ApiName(diagnosticSourceInstance, double arg1, string arg2);'</summary>
        public class StubbedApis
        {
            private readonly DynamicInvoker_DiagnosticSource _thisInvoker;

            internal StubbedApis(DynamicInvoker_DiagnosticSource thisInvoker)
            {
                _thisInvoker = thisInvoker;
            }

            public bool IsEnabled(object diagnosticSourceInstance, string eventName, object arg1, object arg2)
            {
                return ((StaticSystemDiagnostics.DiagnosticSource) diagnosticSourceInstance).IsEnabled(eventName, arg1, arg2);
            }

            public void Write(object diagnosticSourceInstance, string eventName, object payloadValue)
            {
                ((StaticSystemDiagnostics.DiagnosticSource) diagnosticSourceInstance).Write(eventName, payloadValue);
            }
        }
    }
}

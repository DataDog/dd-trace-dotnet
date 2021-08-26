using System;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvoker_DiagnosticSource : DiagnosticSourceAssembly.IDynamicInvoker
    {
        private class CashedDelegates
        {
            public Func<object, string, object, object, bool> IsEnabled;
            public Action<object, string, object> Write;
        }

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

        public DynamicInvokerHandle<DynamicInvoker_DiagnosticSource> Handle
        {
            get { return _handle; }
        }

        public bool IsValid
        {
            get { return _handle.IsValid; }
        }

        public string DiagnosticSourceAssemblyName
        {
            get { return _diagnosticSourceType?.Assembly?.FullName; }
        }

        public StubbedApis Call
        {
            get { return _stubbedApis; }
        }

        public IDisposable SubscribeInvalidatedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker> invokerInvalidatedAction)
        {
            return _handle.SubscribeInvalidatedListener(invokerInvalidatedAction);
        }

        public IDisposable SubscribeInvalidatedListener(Action<DiagnosticSourceAssembly.IDynamicInvoker, object> invokerInvalidatedAction, object state)
        {
            return _handle.SubscribeInvalidatedListener(invokerInvalidatedAction, state);
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
            private readonly CashedDelegates _cashedDelegates;

            internal StubbedApis(DynamicInvoker_DiagnosticSource thisInvoker)
            {
                _thisInvoker = thisInvoker;
                _cashedDelegates = new CashedDelegates();
            }

            public bool IsEnabled(object diagnosticSourceInstance, string eventName, object arg1, object arg2)
            {
                Func<object, string, object, object, bool> invokerDelegate = _cashedDelegates.IsEnabled;
                if (invokerDelegate == null)
                {
                    try
                    {
                        // invokerDelegate = (diagnosticSourceInstance, eventName, arg1, arg2) => 
                        //                              ((Diagnosticource) diagnosticSourceInstance).Subscribe(eventName, arg1, arg2);

                        ParameterExpression exprDiagnosticSourceInstanceParam = Expression.Parameter(typeof(object), "diagnosticSourceInstance");
                        ParameterExpression exprEventNameParam = Expression.Parameter(typeof(string), "eventName");
                        ParameterExpression exprArg1Param = Expression.Parameter(typeof(object), "arg1");
                        ParameterExpression exprArg2Param = Expression.Parameter(typeof(object), "arg2");

                        MethodInfo methodInfo = _thisInvoker._diagnosticSourceType.GetMethod("IsEnabled",
                                                                                             BindingFlags.Instance | BindingFlags.Public,
                                                                                             binder: null,
                                                                                             new Type[] { typeof(string), typeof(object), typeof(object) },
                                                                                             modifiers: null);

                        var exprInvoker = Expression.Lambda<Func<object, string, object, object, bool>>(
                                Expression.Call(
                                        Expression.Convert(exprDiagnosticSourceInstanceParam, _thisInvoker._diagnosticSourceType),
                                        methodInfo,
                                        exprEventNameParam,
                                        exprArg1Param,
                                        exprArg2Param),
                                exprDiagnosticSourceInstanceParam,
                                exprEventNameParam,
                                exprArg1Param,
                                exprArg2Param);

                        invokerDelegate = exprInvoker.Compile();
                        invokerDelegate = Concurrent.TrySetOrGetValue(ref _cashedDelegates.IsEnabled, invokerDelegate);
                    }
                    catch (Exception ex)
                    {
                        throw new DynamicInvocationException(typeof(DynamicInvoker_DiagnosticSource),
                                                            $"Error while building the invocation delegate for the API \"{nameof(IsEnabled)}\".",
                                                             ex);
                    }
                }

                bool result = invokerDelegate(diagnosticSourceInstance, eventName, arg1, arg2);
                return result;
            }

            public void Write(object diagnosticSourceInstance, string eventName, object payloadValue)
            {
                Action<object, string, object> invokerDelegate = _cashedDelegates.Write;
                if (invokerDelegate == null)
                {
                    try
                    {
                        // invokerDelegate = (diagnosticSourceInstance, eventName, payloadValue) => 
                        //                              ((Diagnosticource) diagnosticSourceInstance).Write(eventName, payloadValue);

                        ParameterExpression exprDiagnosticSourceInstanceParam = Expression.Parameter(typeof(object), "diagnosticSourceInstance");
                        ParameterExpression exprEventNameParam = Expression.Parameter(typeof(string), "eventName");
                        ParameterExpression exprPayloadValueParam = Expression.Parameter(typeof(object), "payloadValue");

                        MethodInfo methodInfo = _thisInvoker._diagnosticSourceType.GetMethod("Write",
                                                                                             BindingFlags.Instance | BindingFlags.Public,
                                                                                             binder: null,
                                                                                             new Type[] { typeof(string), typeof(object) },
                                                                                             modifiers: null);

                        var exprInvoker = Expression.Lambda<Action<object, string, object>>(
                                Expression.Call(
                                        Expression.Convert(exprDiagnosticSourceInstanceParam, _thisInvoker._diagnosticSourceType),
                                        methodInfo,
                                        exprEventNameParam,
                                        exprPayloadValueParam),
                                exprDiagnosticSourceInstanceParam,
                                exprEventNameParam,
                                exprPayloadValueParam);

                        invokerDelegate = exprInvoker.Compile();
                        invokerDelegate = Concurrent.TrySetOrGetValue(ref _cashedDelegates.Write, invokerDelegate);
                    }
                    catch (Exception ex)
                    {
                        throw new DynamicInvocationException(typeof(DynamicInvoker_DiagnosticSource),
                                                            $"Error while building the invocation delegate for the API \"{nameof(Write)}\".",
                                                             ex);
                    }
                }

                invokerDelegate(diagnosticSourceInstance, eventName, payloadValue);
            }
        }
    }
}

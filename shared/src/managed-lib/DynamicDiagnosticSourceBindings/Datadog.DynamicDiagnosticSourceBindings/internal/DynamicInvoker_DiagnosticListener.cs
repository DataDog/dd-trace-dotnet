using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvoker_DiagnosticListener : DiagnosticSourceAssembly.IDynamicInvoker
    {
        private class CashedDelegates
        {
            public Func<string, object> Ctor;
            public Func<IObservable<object>> get_AllListeners;
            public Func<object, string> get_Name;
            public Func<object, IObserver<KeyValuePair<string, object>>, Func<string, object, object, bool>, IDisposable> Subscribe;
        }

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

        public DynamicInvokerHandle<DynamicInvoker_DiagnosticListener> Handle
        {
            get { return _handle; }
        }

        public bool IsValid
        {
            get { return _handle.IsValid; }
        }

        public string DiagnosticSourceAssemblyName
        {
            get { return _diagnosticListenerType?.Assembly?.FullName; }
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
            private readonly CashedDelegates _cashedDelegates;

            internal StubbedApis(DynamicInvoker_DiagnosticListener thisInvoker)
            {
                _thisInvoker = thisInvoker;
                _cashedDelegates = new CashedDelegates();
            }

            public object Ctor(string diagnosticSourceName)
            {
                Func<string, object> invokerDelegate = _cashedDelegates.Ctor;
                if (invokerDelegate == null)
                {
                    try
                    {
                        // invokerDelegate = (diagnosticSourceName) => new DiagnosticListener(diagnosticSourceName);

                        ParameterExpression exprDiagnosticSourceNameParam = Expression.Parameter(typeof(string), "diagnosticSourceName");

                        ConstructorInfo ctorInfo = _thisInvoker._diagnosticListenerType.GetConstructor(new Type[] { typeof(string) });

                        var exprInvoker = Expression.Lambda<Func<string, object>>(
                                Expression.New(
                                        ctorInfo,
                                        exprDiagnosticSourceNameParam),
                                exprDiagnosticSourceNameParam);

                        invokerDelegate = exprInvoker.Compile();
                        invokerDelegate = Concurrent.TrySetOrGetValue(ref _cashedDelegates.Ctor, invokerDelegate);
                    }
                    catch (Exception ex)
                    {
                        throw new DynamicInvocationException(typeof(DynamicInvoker_DiagnosticListener),
                                                            $"Error while building the invocation delegate for the API \"{nameof(Ctor)}\".",
                                                             ex);
                    }
                }

                object result = invokerDelegate(diagnosticSourceName);
                return result;
            }

            public IObservable<object> get_AllListeners()
            {
                Func<IObservable<object>> invokerDelegate = _cashedDelegates.get_AllListeners;
                if (invokerDelegate == null)
                {
                    try
                    {
                        // invokerDelegate = () => DiagnosticListener.AllListeners;

                        PropertyInfo propertyInfo = _thisInvoker._diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Static | BindingFlags.Public);

                        var exprInvoker = Expression.Lambda<Func<IObservable<object>>>(
                                Expression.Property(
                                        null,
                                        propertyInfo));

                        invokerDelegate = exprInvoker.Compile();
                        invokerDelegate = Concurrent.TrySetOrGetValue(ref _cashedDelegates.get_AllListeners, invokerDelegate);
                    }
                    catch (Exception ex)
                    {
                        throw new DynamicInvocationException(typeof(DynamicInvoker_DiagnosticListener),
                                                            $"Error while building the invocation delegate for the API \"{nameof(get_AllListeners)}\".",
                                                             ex);
                    }
                }

                IObservable<object> result = invokerDelegate();
                return result;
            }

            public string get_Name(object diagnosticListenerInstance)
            {
                Func<object, string> invokerDelegate = _cashedDelegates.get_Name;
                if (invokerDelegate == null)
                {
                    try
                    {
                        // invokerDelegate = (diagnosticListenerInstance) => ((DiagnosticListener) diagnosticListenerInstance).Name;

                        ParameterExpression exprDiagnosticListenerInstanceParam = Expression.Parameter(typeof(object), "diagnosticListenerInstance");

                        PropertyInfo propertyInfo = _thisInvoker._diagnosticListenerType.GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);

                        var exprInvoker = Expression.Lambda<Func<object, string>>(
                                Expression.Property(
                                        Expression.Convert(exprDiagnosticListenerInstanceParam, _thisInvoker._diagnosticListenerType),
                                        propertyInfo),
                                exprDiagnosticListenerInstanceParam);

                        invokerDelegate = exprInvoker.Compile();
                        invokerDelegate = Concurrent.TrySetOrGetValue(ref _cashedDelegates.get_Name, invokerDelegate);
                    }
                    catch (Exception ex)
                    {
                        throw new DynamicInvocationException(typeof(DynamicInvoker_DiagnosticListener),
                                                            $"Error while building the invocation delegate for the API \"{nameof(get_Name)}\".",
                                                             ex);
                    }
                }

                string result = invokerDelegate(diagnosticListenerInstance);
                return result;
            }

            public IDisposable Subscribe(object diagnosticListenerInstance,
                                         IObserver<KeyValuePair<string, object>> eventObserver,
                                         Func<string, object, object, bool> isEventEnabledFilter)
            {
                Func<object, IObserver<KeyValuePair<string, object>>, Func<string, object, object, bool>, IDisposable> invokerDelegate = _cashedDelegates.Subscribe;
                if (invokerDelegate == null)
                {
                    try
                    {
                        // invokerDelegate = (diagnosticListenerInstance, eventObserver, isEventEnabledFilter) => 
                        //                              ((DiagnosticListener) diagnosticListenerInstance).Subscribe(eventObserver, isEventEnabledFilter);

                        ParameterExpression exprDiagnosticListenerInstanceParam = Expression.Parameter(typeof(object), "diagnosticListenerInstance");
                        ParameterExpression exprEventObserverParam = Expression.Parameter(typeof(IObserver<KeyValuePair<string, object>>), "eventObserver");
                        ParameterExpression exprIsEventEnabledFilterParam = Expression.Parameter(typeof(Func<string, object, object, bool>), "isEventEnabledFilter");

                        MethodInfo methodInfo = _thisInvoker._diagnosticListenerType.GetMethod("Subscribe",
                                                                                               BindingFlags.Instance | BindingFlags.Public,
                                                                                               binder: null,
                                                                                               new Type[] { typeof(IObserver<KeyValuePair<string, object>>), 
                                                                                                            typeof(Func<string, object, object, bool>) },
                                                                                               modifiers: null);

                        var exprInvoker = Expression.Lambda<Func<object, IObserver<KeyValuePair<string, object>>, Func<string, object, object, bool>, IDisposable>>(
                                Expression.Call(
                                        Expression.Convert(exprDiagnosticListenerInstanceParam, _thisInvoker._diagnosticListenerType),
                                        methodInfo,
                                        exprEventObserverParam,
                                        exprIsEventEnabledFilterParam),
                                exprDiagnosticListenerInstanceParam,
                                exprEventObserverParam,
                                exprIsEventEnabledFilterParam);

                        invokerDelegate = exprInvoker.Compile();
                        invokerDelegate = Concurrent.TrySetOrGetValue(ref _cashedDelegates.Subscribe, invokerDelegate);
                    }
                    catch (Exception ex)
                    {
                        throw new DynamicInvocationException(typeof(DynamicInvoker_DiagnosticListener),
                                                            $"Error while building the invocation delegate for the API \"{nameof(Subscribe)}\".",
                                                             ex);
                    }
                }

                IDisposable result = invokerDelegate(diagnosticListenerInstance, eventObserver, isEventEnabledFilter);
                return result;
            }
        }
    }
}

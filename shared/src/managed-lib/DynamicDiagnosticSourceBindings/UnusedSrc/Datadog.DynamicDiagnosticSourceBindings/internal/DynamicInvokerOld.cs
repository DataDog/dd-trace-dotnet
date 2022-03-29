using System;
using System.Collections.Generic;
using System.Linq.Expressions;

using Datadog.Util;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal class DynamicInvokerOld
    {
        public readonly DynamicActivityInvoker Activity;
        public readonly DynamicDiagnosticListenerInvoker DiagnosticListener;
        public readonly DynamicActivitySourceInvoker ActivitySource;
        
        private readonly SupportedFeatures _supportedFeatures;

        internal DynamicInvokerOld(
                            SupportedFeatures supportedFeatures,
                            Type activityType,
                            Type activityListenerType,
                            Type activitySourceType)
        {
            Validate.NotNull(supportedFeatures, nameof(supportedFeatures));
            _supportedFeatures = supportedFeatures;

            Activity = new DynamicActivityInvoker(activityType);
            DiagnosticListener = new DynamicDiagnosticListenerInvoker(activityListenerType);
            ActivitySource = (activitySourceType == null) ? null : new DynamicActivitySourceInvoker(activitySourceType);
        }

        public SupportedFeatures SupportedFeatures { get { return _supportedFeatures; } }

        internal class DynamicActivityInvoker
        {
            private class CashedDelegates
            {
                public Action<object, string, string> AddBagage = null;
                public Func<object, string, string> GetBaggageItem = null;
                public Func<object, IEnumerable<KeyValuePair<string, string>>> get_Baggage;
            }

            private readonly CashedDelegates _cashedDelegates = new CashedDelegates();
            private readonly Type _activityType;

            internal DynamicActivityInvoker(Type activityType)
            {
                Validate.NotNull(activityType, nameof(activityType));
                _activityType = activityType;
            }

            public void ValidateType(object activityInstance)
            {
                Validate.NotNull(activityInstance, nameof(activityInstance));

                Type actualType = activityInstance.GetType();
                if (!_activityType.Equals(actualType))
                {
                    if (_activityType.IsAssignableFrom(actualType))
                    {
                        throw new ArgumentException($"The specified {nameof(activityInstance)} is expected to be of type {_activityType.AssemblyQualifiedName},"
                                                  + $" but the actual runtime type is {actualType.AssemblyQualifiedName}."
                                                  + $" Notably, the expected type {_activityType.Name} is assignable from the actual runtime type {actualType.Name}."
                                                  + $" However, an exact match is required.");
                    }
                    else
                    {
                        throw new ArgumentException($"The specified {nameof(activityInstance)} is expected to be of type {_activityType.AssemblyQualifiedName},"
                                                  + $" but the actual runtime type is {actualType.AssemblyQualifiedName}.");
                    }
                }
            }

            //public object StartNewActivity(string operationName)
            //{
            //    invoker = (operationName) =>
            //    {
            //        Activity activity = new Activity(operationName);
            //        ActivityStub activityStub = ActivityStub.Wrap(activity);
            //        PreStartInitializationCallback(activityStub);
            //        autoInstrumentationDiagnosticSource.StartActivity(actvitiy, activity);
            //    }
            //}

            public void AddBaggage(object activityInstance, string key, string value)
            {
                // Activity API signature:
                // public Activity AddBaggage(string key, string value)

                // invoker = (activityInstance, key, value) => ((Activity) activityInstance).AddBaggage(key, value);

                ValidateType(activityInstance);

                Action<object, string, string> invoker = _cashedDelegates.AddBagage;
                if (invoker == null)
                {
                    ParameterExpression exprActivityInstance = Expression.Parameter(_activityType, "activityInstance");
                    ParameterExpression exprKey = Expression.Parameter(typeof(string), "key");
                    ParameterExpression exprValue = Expression.Parameter(typeof(string), "value");

                    var exprInvoker = Expression.Lambda<Action<object, string, string>>(
                            Expression.Call(
                                    Expression.Convert(exprActivityInstance, _activityType),
                                    "AddBaggage", new[] { typeof(string), typeof(string) }, exprKey, exprValue),
                            exprActivityInstance, exprKey, exprValue);

                    invoker = exprInvoker.Compile();
                    invoker = Concurrent.TrySetOrGetValue(ref _cashedDelegates.AddBagage, invoker);
                }

                invoker(activityInstance, key, value);
            }

            internal ActivityIdFormatStub get_DefaultIdFormat()
            {
                throw new NotImplementedException();
            }

            internal object get_Current()
            {
                throw new NotImplementedException();
            }

            public string GetBaggageItem(object activityInstance, string key)
            {
                // Activity API signature:
                // public string GetBaggageItem(string key)

                // invoker = (activityInstance, key) => ((Activity) activityInstance).GetBaggageItem(key);

                ValidateType(activityInstance);

                Func<object, string, string> invoker = _cashedDelegates.GetBaggageItem;
                if (invoker == null)
                {
                    ParameterExpression exprActivityInstance = Expression.Parameter(_activityType, "activityInstance");
                    ParameterExpression exprKey = Expression.Parameter(typeof(string), "key");

                    var exprInvoker = Expression.Lambda<Func<object, string, string>>(
                            Expression.Call(
                                    Expression.Convert(exprActivityInstance, _activityType),
                                    "GetBaggageItem", new[] { typeof(string) }, exprKey),
                            exprActivityInstance, exprKey);

                    invoker = exprInvoker.Compile();
                    invoker = Concurrent.TrySetOrGetValue(ref _cashedDelegates.GetBaggageItem, invoker);
                }

                string result = invoker(activityInstance, key);
                return result;
            }

            public IEnumerable<KeyValuePair<string, string>> get_Baggage(object activityInstance)
            {
                // Activity API signature:
                // public IEnumerable<KeyValuePair<string, string>> Baggage { get; }

                // invoker = (activityInstance) => ((Activity) activityInstance).Baggage;

                ValidateType(activityInstance);

                Func<object, IEnumerable<KeyValuePair<string, string>>> invoker = _cashedDelegates.get_Baggage;
                if (invoker == null)
                {
                    ParameterExpression exprActivityInstance = Expression.Parameter(_activityType, "activityInstance");

                    var exprInvoker = Expression.Lambda<Func<object, IEnumerable<KeyValuePair<string, string>>>>(
                            Expression.Property(
                                    Expression.Convert(exprActivityInstance, _activityType),
                                    "Baggage"),
                            exprActivityInstance);

                    invoker = exprInvoker.Compile();
                    invoker = Concurrent.TrySetOrGetValue(ref _cashedDelegates.get_Baggage, invoker);
                }

                IEnumerable<KeyValuePair<string, string>> result = invoker(activityInstance);
                return result;
            }

            internal object Ctor(string operationName)
            {
                throw new NotImplementedException();
            }

            internal void SetParentId(object activityInstance, string parentId)
            {
                throw new NotImplementedException();
            }

            internal void AddTag(object activityInstance, string key, string value)
            {
                throw new NotImplementedException();
            }


            //private static MethodInfo GetMethodInfo(Type containingType, string methodName, bool isStatic)
            //{
            //    BindingFlags staticOrInstanceFlag = (isStatic ? BindingFlags.Static : BindingFlags.Instance);

            //    MethodInfo methodInfo = containingType.GetMethod(methodName, BindingFlags.Public | staticOrInstanceFlag);
            //    if (methodInfo == null)
            //    {
            //        throw new InvalidOperationException($"Cannot reflect over the method \"{methodName}\" with the BindingFlags Public and {staticOrInstanceFlag.ToString()}."
            //                                          + $" The type being reflected is \"{containingType.AssemblyQualifiedName}\".");
            //    }

            //    return methodInfo;
            //}


        }

        internal class DynamicActivitySourceInvoker
        {
            private class CashedDelegates
            {
                
            }

            private readonly CashedDelegates _cashedDelegates = new CashedDelegates();
            private readonly Type _activitySourceType;

            internal DynamicActivitySourceInvoker(Type activitySourceType)
            {
                Validate.NotNull(activitySourceType, nameof(activitySourceType));
                _activitySourceType = activitySourceType;
            }

            internal object StartActivity(string operationName, ActivityKindStub activityKind, ActivityContextStub parentContext, IEnumerable<KeyValuePair<string, string>> tags)
            {
                throw new NotImplementedException();
            }
        }

        internal class DynamicDiagnosticListenerInvoker
        {
            private class CashedDelegates
            {

            }

            private readonly CashedDelegates _cashedDelegates = new CashedDelegates();
            private readonly Type _diagnosticListenerType;

            public object DefaultDiagnosticSource { get { throw new NotImplementedException(); } }

            internal DynamicDiagnosticListenerInvoker(Type diagnosticListenerType)
            {
                Validate.NotNull(diagnosticListenerType, nameof(diagnosticListenerType));
                _diagnosticListenerType = diagnosticListenerType;
            }

            internal void StartActivity(object activityInstance1, object activityInstance2)
            {
                throw new NotImplementedException();
            }
        }
    }
}

// <copyright file="ActivityListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal class ActivityListener
    {
        private const int InitializationBackoffPerRetry = 10000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListener));

        private static int _initializationRetries = 5;

        private static Type _diagnosticListenerType;
        private static Type _observerDiagnosticListenerType;

        private static MethodInfo _onSetListenerMethodInfo;
        private static MethodInfo _onNextActivityMethodInfo;

        private static Type _activityListenerType;
        private static Type _activityType;
        private static Type _activitySourceType;
        private static Type _activityCreationOptionsType;
        private static Type _activitySamplingResultType;
        private static Type _activityContextType;
        private static Type _sampleActivityType;

        private static MethodInfo _onActivityStartedMethodInfo;
        private static MethodInfo _onActivityStoppedMethodInfo;
        private static MethodInfo _onSampleMethodInfo;
        private static MethodInfo _onSampleUsingParentIdMethodInfo;
        private static MethodInfo _onShouldListenToMethodInfo;

        private static Func<object> _getCurrentActivity;
        private static Action<string, KeyValuePair<string, object>, object> _onNextActivityDelegate;
        private static Func<object, bool> _onSetListenerDelegate;

        private static int _initialized = 0;

        public static void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            // Try to resolve System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource
            _diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource");
            if (_diagnosticListenerType is null)
            {
                // Because we cannot resolve the DiagnosticListener type we allow a later initialization.
                // For this case we are going to do some retries with a back-off.
                if (Interlocked.Decrement(ref _initializationRetries) > 0)
                {
                    Task.Delay(InitializationBackoffPerRetry).ContinueWith(_ =>
                    {
                        Interlocked.Exchange(ref _initialized, 0);
                        Initialize();
                    });
                }

                return;
            }

            // If we found a we load the shared types.
            _activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource");
            _onShouldListenToMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnShouldListenTo", BindingFlags.Static | BindingFlags.Public);

            // Initialize
            var diagnosticSourceAssemblyName = _diagnosticListenerType.Assembly.GetName();
            Log.Information($"DiagnosticSource: {diagnosticSourceAssemblyName.FullName}");

            var version = diagnosticSourceAssemblyName.Version;
            if (version?.Major is 5 or 6)
            {
                CreateActivityListenerInstance();
                return;
            }

            if (version >= new Version(4, 0, 2) && _activityType is not null)
            {
                CreateDiagnosticSourceListenerInstance();
                return;
            }

            Log.Information($"An activity listener was found but version {version} is not supported.");
        }

        private static void CreateActivityListenerInstance()
        {
            _activityListenerType = Type.GetType("System.Diagnostics.ActivityListener, System.Diagnostics.DiagnosticSource");
            _activitySourceType = Type.GetType("System.Diagnostics.ActivitySource, System.Diagnostics.DiagnosticSource");
            _activityCreationOptionsType = Type.GetType("System.Diagnostics.ActivityCreationOptions`1, System.Diagnostics.DiagnosticSource");
            _activitySamplingResultType = Type.GetType("System.Diagnostics.ActivitySamplingResult, System.Diagnostics.DiagnosticSource");
            _activityContextType = Type.GetType("System.Diagnostics.ActivityContext, System.Diagnostics.DiagnosticSource");
            _sampleActivityType = Type.GetType("System.Diagnostics.SampleActivity`1, System.Diagnostics.DiagnosticSource");

            _onActivityStartedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStarted", BindingFlags.Static | BindingFlags.Public);
            _onActivityStoppedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStopped", BindingFlags.Static | BindingFlags.Public);
            _onSampleMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSample", BindingFlags.Static | BindingFlags.Public);
            _onSampleUsingParentIdMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSampleUsingParentId", BindingFlags.Static | BindingFlags.Public);

            Log.Information($"Activity listener: {_activityListenerType.AssemblyQualifiedName ?? "(null)"}");

            // Create the ActivityListener instance
            var activityListenerInstance = Activator.CreateInstance(_activityListenerType);
            var activityListenerProxy = activityListenerInstance.DuckCast<IActivityListener>();

            activityListenerProxy.ActivityStarted = ActivityListenerDelegatesBuilder.CreateOnActivityStartedDelegate();
            activityListenerProxy.ActivityStopped = ActivityListenerDelegatesBuilder.CreateOnActivityStoppedDelegate();
            activityListenerProxy.Sample = ActivityListenerDelegatesBuilder.CreateOnSampleDelegate();
            activityListenerProxy.SampleUsingParentId = ActivityListenerDelegatesBuilder.CreateOnSampleUsingParentIdDelegate();
            activityListenerProxy.ShouldListenTo = ActivityListenerDelegatesBuilder.CreateOnShouldListenToDelegate();

            var addActivityListenerMethodInfo = _activitySourceType.GetMethod("AddActivityListener", BindingFlags.Static | BindingFlags.Public);
            addActivityListenerMethodInfo.Invoke(null, new[] { activityListenerInstance });
        }

        private static void CreateDiagnosticSourceListenerInstance()
        {
            Log.Information($"DiagnosticListener listener: {_diagnosticListenerType.AssemblyQualifiedName ?? "(null)"}");

            _observerDiagnosticListenerType = typeof(IObserver<>).MakeGenericType(_diagnosticListenerType);
            _onSetListenerMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod("OnSetListener", BindingFlags.Static | BindingFlags.NonPublic);
            _onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod("OnNextActivity", BindingFlags.Static | BindingFlags.NonPublic);

            // Create Activity.Current delegate.
            var activityCurrentProperty = _activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            var activityCurrentMethodInfo = activityCurrentProperty?.GetMethod;
            var activityCurrentDynMethod = new DynamicMethod("ActivityCurrent", typeof(object), Type.EmptyTypes, typeof(ActivityListener).Module, true);
            var activityCurrentDynMethodIl = activityCurrentDynMethod.GetILGenerator();
            activityCurrentDynMethodIl.EmitCall(OpCodes.Call, activityCurrentMethodInfo, null);
            activityCurrentDynMethodIl.Emit(OpCodes.Ret);
            _getCurrentActivity = (Func<object>)activityCurrentDynMethod.CreateDelegate(typeof(Func<object>));

            // Create delegate for OnNext + Activity
            var onNextActivityDynMethod = new DynamicMethod("OnNextActivityDyn", typeof(void), new[] { typeof(string), typeof(KeyValuePair<string, object>), typeof(object) }, typeof(ActivityListener).Module, true);
            var onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity), _activityType);
            var onNextActivityMethod = _onNextActivityMethodInfo.MakeGenericMethod(onNextActivityProxyResult.ProxyType);
            var onNextActivityProxyTypeCtor = onNextActivityProxyResult.ProxyType.GetConstructors()[0];
            var onNextActivityDynMethodIl = onNextActivityDynMethod.GetILGenerator();
            onNextActivityDynMethodIl.Emit(OpCodes.Ldarg_0);
            onNextActivityDynMethodIl.Emit(OpCodes.Ldarg_1);
            onNextActivityDynMethodIl.Emit(OpCodes.Ldarg_2);
            onNextActivityDynMethodIl.Emit(OpCodes.Newobj, onNextActivityProxyTypeCtor);
            onNextActivityDynMethodIl.EmitCall(OpCodes.Call, onNextActivityMethod, null);
            onNextActivityDynMethodIl.Emit(OpCodes.Ret);
            _onNextActivityDelegate = (Action<string, KeyValuePair<string, object>, object>)onNextActivityDynMethod.CreateDelegate(typeof(Action<string, KeyValuePair<string, object>, object>));

            // Create delegate for OnSetListener + Source
            var onSetListenerDynMethod = new DynamicMethod("OnShouldListenToDyn", typeof(bool), new[] { typeof(object) }, typeof(ActivityListener).Module, true);
            var onSetListenerProxyResult = DuckType.GetOrCreateProxyType(typeof(ISource), _diagnosticListenerType);
            var onSetListenerMethod = _onShouldListenToMethodInfo.MakeGenericMethod(onSetListenerProxyResult.ProxyType);
            var onSetListenerProxyTypeCtor = onSetListenerProxyResult.ProxyType.GetConstructors()[0];
            var onSetListenerDynMethodIl = onSetListenerDynMethod.GetILGenerator();
            onSetListenerDynMethodIl.Emit(OpCodes.Ldarg_0);
            onSetListenerDynMethodIl.Emit(OpCodes.Newobj, onSetListenerProxyTypeCtor);
            onSetListenerDynMethodIl.EmitCall(OpCodes.Call, onSetListenerMethod, null);
            onSetListenerDynMethodIl.Emit(OpCodes.Ret);
            _onSetListenerDelegate = (Func<object, bool>)onSetListenerDynMethod.CreateDelegate(typeof(Func<object, bool>));

            // Initialize and subscribe to DiagnosticListener.AllListeners.Subscribe
            var diagObserverType = CreateDiagnosticObserverType();
            var diagListener = Activator.CreateInstance(diagObserverType);
            var allListenersPropertyInfo = _diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
            var subscribeMethodInfo = allListenersPropertyInfo?.PropertyType.GetMethod("Subscribe");
            subscribeMethodInfo?.Invoke(allListenersPropertyInfo.GetValue(null), new[] { diagListener });
        }

        private static Type CreateDiagnosticObserverType()
        {
            var assemblyName = new AssemblyName("Datadog.DiagnosticObserverListener.Dynamic");
            assemblyName.Version = typeof(ActivityListener).Assembly.GetName().Version;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            DuckType.EnsureTypeVisibility(moduleBuilder, typeof(ActivityListener));

            var typeBuilder = moduleBuilder.DefineType(
                "DiagnosticObserver",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed,
                typeof(object),
                new[] { _observerDiagnosticListenerType });

            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

            // OnCompleted
            var onCompletedMethod = typeBuilder.DefineMethod("OnCompleted", methodAttributes, typeof(void), Type.EmptyTypes);
            var onCompletedMethodIl = onCompletedMethod.GetILGenerator();
            onCompletedMethodIl.Emit(OpCodes.Ret);

            // OnError
            var onErrorMethod = typeBuilder.DefineMethod("OnError", methodAttributes, typeof(void), new[] { typeof(Exception) });
            var onErrorMethodIl = onErrorMethod.GetILGenerator();
            onErrorMethodIl.Emit(OpCodes.Ret);

            // OnNext
            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { _diagnosticListenerType });
            var onNextMethodIl = onNextMethod.GetILGenerator();
            onNextMethodIl.Emit(OpCodes.Ldarg_1);
            onNextMethodIl.EmitCall(OpCodes.Call, _onSetListenerMethodInfo, null);
            onNextMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo().AsType();
        }

        internal class ActivityListenerDelegatesBuilder
        {
            public static Delegate CreateOnActivityStartedDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnActivityStartedDyn",
                    typeof(void),
                    new[] { _activityType },
                    typeof(ActivityListener).Module,
                    true);

                var activityProxyType = typeof(IActivity5);
                if (_activityType.Assembly.GetName().Version?.Major is 6)
                {
                    activityProxyType = typeof(IActivity6);
                }

                var proxyResult = DuckType.GetOrCreateProxyType(activityProxyType, _activityType);
                var method = _onActivityStartedMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(_activityType));
            }

            public static Delegate CreateOnActivityStoppedDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnActivityStoppedDyn",
                    typeof(void),
                    new[] { _activityType },
                    typeof(ActivityListener).Module,
                    true);

                var activityProxyType = typeof(IActivity5);
                if (_activityType.Assembly.GetName().Version?.Major is 6)
                {
                    activityProxyType = typeof(IActivity6);
                }

                var proxyResult = DuckType.GetOrCreateProxyType(activityProxyType, _activityType);
                var method = _onActivityStoppedMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(_activityType));
            }

            public static Delegate CreateOnSampleDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnSampleDyn",
                    _activitySamplingResultType,
                    new[] { _activityCreationOptionsType.MakeGenericType(_activityContextType).MakeByRefType() },
                    typeof(ActivityListener).Module,
                    true);

                var il = dynMethod.GetILGenerator();
                il.EmitCall(OpCodes.Call, _onSampleMethodInfo, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(_sampleActivityType.MakeGenericType(_activityContextType));
            }

            public static Delegate CreateOnSampleUsingParentIdDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnSampleUsingParentIdDyn",
                    _activitySamplingResultType,
                    new[] { _activityCreationOptionsType.MakeGenericType(typeof(string)).MakeByRefType() },
                    typeof(ActivityListener).Module,
                    true);

                var il = dynMethod.GetILGenerator();
                il.EmitCall(OpCodes.Call, _onSampleUsingParentIdMethodInfo, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(_sampleActivityType.MakeGenericType(typeof(string)));
            }

            public static Delegate CreateOnShouldListenToDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnShouldListenToDyn",
                    typeof(bool),
                    new[] { _activitySourceType },
                    typeof(ActivityListener).Module,
                    true);

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivitySource), _activitySourceType);
                var method = _onShouldListenToMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Func<,>).MakeGenericType(_activitySourceType, typeof(bool)));
            }
        }

        internal class DiagnosticSourceEventListener : IObserver<KeyValuePair<string, object>>
        {
            private readonly string _sourceName;

            private DiagnosticSourceEventListener(string sourceName)
            {
                _sourceName = sourceName;
            }

            internal static void OnSetListener(object value)
            {
                try
                {
                    if (_onSetListenerDelegate(value))
                    {
                        ((IObservable<KeyValuePair<string, object>>)value).Subscribe(new DiagnosticSourceEventListener(value.DuckCast<Source>().Name));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }

            internal static void OnNextActivity<T>(string sourceName, KeyValuePair<string, object> value, T activity)
                where T : IActivity
            {
                try
                {
                    var dotIndex = value.Key.LastIndexOf('.');
                    var operationName = value.Key.Substring(0, dotIndex);
                    var suffix = value.Key.Substring(dotIndex + 1);

                    if (activity?.Instance != null && activity.OperationName != operationName)
                    {
                        // Activity is not associated with the event we received.
                        // clearing the Activity variable.
                        activity = default;
                    }

                    if (suffix.Equals("Start", StringComparison.Ordinal) && activity?.Instance is not null)
                    {
                        ActivityListenerHandler.OnActivityWithSourceStarted(sourceName, activity);
                    }
                    else if (suffix.Equals("Stop", StringComparison.Ordinal))
                    {
                        ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(KeyValuePair<string, object> value)
            {
                try
                {
                    _onNextActivityDelegate(_sourceName, value, _getCurrentActivity());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }
        }
    }
}

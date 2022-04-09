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
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListener
    {
        private const int InitializationBackoffPerRetry = 10000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListener));

        private static int _initializationRetries = 5;

        private static Type _activitySourceType;
        private static Func<object> _getCurrentActivity;
        private static Action<string, KeyValuePair<string, object>, object> _onNextActivityDelegate;
        private static Func<object, bool> _onSetListenerDelegate;
        private static object _activityListenerInstance;

        private static int _initialized = 0;
        private static int _stopped = 0;

        public static IActivity GetCurrentActivity()
        {
            object activity = null;

            try
            {
                activity = _getCurrentActivity?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calling Activity.Current");
            }

            if (activity is null)
            {
                return null;
            }

            if (activity.TryDuckCast<IActivity6>(out var activity6))
            {
                return activity6;
            }

            if (activity.TryDuckCast<IActivity5>(out var activity5))
            {
                return activity5;
            }

            if (activity.TryDuckCast<IW3CActivity>(out var w3cActivity))
            {
                return w3cActivity;
            }

            return activity.TryDuckCast<IActivity>(out var activity4) ? activity4 : null;
        }

        public static void Initialize() => Initialize(CancellationToken.None);

        public static void Initialize(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            // Try to resolve System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource
            var diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource");
            if (diagnosticListenerType is null)
            {
                // Because we cannot resolve the DiagnosticListener type we allow a later initialization.
                // For this case we are going to do some retries with a back-off.
                if (Interlocked.Decrement(ref _initializationRetries) > 0)
                {
                    Task.Delay(InitializationBackoffPerRetry, cancellationToken).ContinueWith(
                        _ =>
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        Interlocked.Exchange(ref _initialized, 0);
                        Initialize();
                    },
                        cancellationToken);
                }

                return;
            }

            // Initialize
            var diagnosticSourceAssemblyName = diagnosticListenerType.Assembly.GetName();
            Log.Information("DiagnosticSource: {diagnosticSourceAssemblyNameFullName}", diagnosticSourceAssemblyName.FullName);

            var activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource");
            var version = diagnosticSourceAssemblyName.Version;

            // Check if Version >= 5 loaded (Uses ActivityListener implementation)
            if (version?.Major >= 5)
            {
                if (activityType is null)
                {
                    Log.Error("the Activity type cannot be found.");
                    return;
                }

                CreateCurrentActivityDelegates(activityType);
                ChangeActivityDefaultFormat(activityType);
                CreateActivityListenerInstance(activityType);
                return;
            }

            // Check if Version is 4.0.4 or greater (Uses DiagnosticListener implementation / Nuget version 4.6.0)
            if (version >= new Version(4, 0, 4))
            {
                if (activityType is null)
                {
                    Log.Error("the Activity type cannot be found.");
                    return;
                }

                CreateCurrentActivityDelegates(activityType);
                ChangeActivityDefaultFormat(activityType);
                CreateDiagnosticSourceListenerInstance(diagnosticListenerType, activityType);
                return;
            }

            Log.Information("An activity listener was found but version {version} is not supported.", version.ToString());

            static void CreateCurrentActivityDelegates(Type activityType)
            {
                // Create Activity.Current delegate.
                var activityCurrentProperty = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
                if (activityCurrentProperty is null)
                {
                    throw new NullReferenceException("Activity.Current property cannot be found in the Activity type.");
                }

                var activityCurrentMethodInfo = activityCurrentProperty.GetMethod;
                var activityCurrentDynMethod = new DynamicMethod("ActivityCurrent", typeof(object), Type.EmptyTypes, typeof(ActivityListener).Module, true);
                var activityCurrentDynMethodIl = activityCurrentDynMethod.GetILGenerator();
                activityCurrentDynMethodIl.EmitCall(OpCodes.Call, activityCurrentMethodInfo, null);
                activityCurrentDynMethodIl.Emit(OpCodes.Ret);
                _getCurrentActivity = (Func<object>)activityCurrentDynMethod.CreateDelegate(typeof(Func<object>));
            }

            static void ChangeActivityDefaultFormat(Type activityType)
            {
                // We change the default ID format to W3C (so traceid and spanid gets populated)
                if (Activator.CreateInstance(activityType, string.Empty).TryDuckCast<IActivityFormat>(out var activityFormat))
                {
                    activityFormat.DefaultIdFormat = ActivityIdFormat.W3C;
                }
            }
        }

        private static void CreateActivityListenerInstance(Type activityType)
        {
            var activityListenerType = Type.GetType("System.Diagnostics.ActivityListener, System.Diagnostics.DiagnosticSource", throwOnError: true);
            var activitySourceType = Type.GetType("System.Diagnostics.ActivitySource, System.Diagnostics.DiagnosticSource", throwOnError: true);
            var activityCreationOptionsType = Type.GetType("System.Diagnostics.ActivityCreationOptions`1, System.Diagnostics.DiagnosticSource", throwOnError: true);
            var activitySamplingResultType = Type.GetType("System.Diagnostics.ActivitySamplingResult, System.Diagnostics.DiagnosticSource", throwOnError: true);
            var activityContextType = Type.GetType("System.Diagnostics.ActivityContext, System.Diagnostics.DiagnosticSource", throwOnError: true);
            var sampleActivityType = Type.GetType("System.Diagnostics.SampleActivity`1, System.Diagnostics.DiagnosticSource", throwOnError: true);

            var onActivityStartedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStarted", BindingFlags.Static | BindingFlags.Public);
            var onActivityStoppedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStopped", BindingFlags.Static | BindingFlags.Public);
            var onSampleMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSample", BindingFlags.Static | BindingFlags.Public);
            var onSampleUsingParentIdMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSampleUsingParentId", BindingFlags.Static | BindingFlags.Public);
            var onShouldListenToMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnShouldListenTo", BindingFlags.Static | BindingFlags.Public);

            Log.Information("Activity listener: {activityListenerType}", activityListenerType.AssemblyQualifiedName ?? "(null)");

            // Create the ActivityListener instance
            var activityListener = Activator.CreateInstance(activityListenerType);
            var activityListenerProxy = activityListener.DuckCast<IActivityListener>();

            activityListenerProxy.ActivityStarted = ActivityListenerDelegatesBuilder.CreateOnActivityStartedDelegate(activityType, onActivityStartedMethodInfo);
            activityListenerProxy.ActivityStopped = ActivityListenerDelegatesBuilder.CreateOnActivityStoppedDelegate(activityType, onActivityStoppedMethodInfo);
            activityListenerProxy.Sample = ActivityListenerDelegatesBuilder.CreateOnSampleDelegate(activitySamplingResultType, activityCreationOptionsType, activityContextType, sampleActivityType, onSampleMethodInfo);
            activityListenerProxy.SampleUsingParentId = ActivityListenerDelegatesBuilder.CreateOnSampleUsingParentIdDelegate(activitySamplingResultType, activityCreationOptionsType, sampleActivityType, onSampleUsingParentIdMethodInfo);
            activityListenerProxy.ShouldListenTo = ActivityListenerDelegatesBuilder.CreateOnShouldListenToDelegate(activitySourceType, onShouldListenToMethodInfo);

            var addActivityListenerMethodInfo = activitySourceType.GetMethod("AddActivityListener", BindingFlags.Static | BindingFlags.Public);
            if (addActivityListenerMethodInfo is null)
            {
                throw new NullReferenceException("ActivitySource.AddActivityListener method cannot be found.");
            }

            addActivityListenerMethodInfo.Invoke(null, new[] { activityListener });

            // Set the global field after calling the `AddActivityListener` method
            _activitySourceType = activitySourceType;
            _activityListenerInstance = activityListener;
        }

        private static void CreateDiagnosticSourceListenerInstance(Type diagnosticListenerType, Type activityType)
        {
            Log.Information("DiagnosticListener listener: {diagnosticListenerType}", diagnosticListenerType.AssemblyQualifiedName ?? "(null)");

            var onShouldListenToMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnShouldListenTo", BindingFlags.Static | BindingFlags.Public);

            var observerDiagnosticListenerType = typeof(IObserver<>).MakeGenericType(diagnosticListenerType);
            var onSetListenerMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod("OnSetListener", BindingFlags.Static | BindingFlags.NonPublic);
            var onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod("OnNextActivity", BindingFlags.Static | BindingFlags.NonPublic);

            // Create delegate for OnNext + Activity
            var onNextActivityDynMethod = new DynamicMethod("OnNextActivityDyn", typeof(void), new[] { typeof(string), typeof(KeyValuePair<string, object>), typeof(object) }, typeof(ActivityListener).Module, true);

            DuckType.CreateTypeResult onNextActivityProxyResult;
            if (activityType.GetField("_traceId", DuckAttribute.DefaultFlags) is not null)
            {
                onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IW3CActivity), activityType);
            }
            else
            {
                onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity), activityType);
            }

            var onNextActivityMethod = onNextActivityMethodInfo.MakeGenericMethod(onNextActivityProxyResult.ProxyType);
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
            var onSetListenerProxyResult = DuckType.GetOrCreateProxyType(typeof(ISource), diagnosticListenerType);
            var onSetListenerMethod = onShouldListenToMethodInfo.MakeGenericMethod(onSetListenerProxyResult.ProxyType);
            var onSetListenerProxyTypeCtor = onSetListenerProxyResult.ProxyType.GetConstructors()[0];
            var onSetListenerDynMethodIl = onSetListenerDynMethod.GetILGenerator();
            onSetListenerDynMethodIl.Emit(OpCodes.Ldarg_0);
            onSetListenerDynMethodIl.Emit(OpCodes.Newobj, onSetListenerProxyTypeCtor);
            onSetListenerDynMethodIl.EmitCall(OpCodes.Call, onSetListenerMethod, null);
            onSetListenerDynMethodIl.Emit(OpCodes.Ret);
            _onSetListenerDelegate = (Func<object, bool>)onSetListenerDynMethod.CreateDelegate(typeof(Func<object, bool>));

            // Initialize and subscribe to DiagnosticListener.AllListeners.Subscribe
            var diagObserverType = CreateDiagnosticObserverType(diagnosticListenerType, observerDiagnosticListenerType, onSetListenerMethodInfo);
            var diagListener = Activator.CreateInstance(diagObserverType);
            var allListenersPropertyInfo = diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
            var subscribeMethodInfo = allListenersPropertyInfo?.PropertyType.GetMethod("Subscribe", new[] { observerDiagnosticListenerType });
            subscribeMethodInfo?.Invoke(allListenersPropertyInfo.GetValue(null), new[] { diagListener });
        }

        private static Type CreateDiagnosticObserverType(Type diagnosticListenerType, Type observerDiagnosticListenerType, MethodInfo onSetListenerMethodInfo)
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
                new[] { observerDiagnosticListenerType });

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
            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { diagnosticListenerType });
            var onNextMethodIl = onNextMethod.GetILGenerator();
            onNextMethodIl.Emit(OpCodes.Ldarg_1);
            onNextMethodIl.EmitCall(OpCodes.Call, onSetListenerMethodInfo, null);
            onNextMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo().AsType();
        }

        internal static void StopListeners()
        {
            // If there's an activity listener instance we detach the instance and clear it.
            if (_activityListenerInstance is { } activityListenerInstance)
            {
                var detachListenerMethodInfo = _activitySourceType?.GetMethod("DetachListener", BindingFlags.Static | BindingFlags.Public);
                detachListenerMethodInfo?.Invoke(null, new[] { activityListenerInstance });
                _activityListenerInstance = null;
            }

            Interlocked.Exchange(ref _stopped, 1);
        }

        internal static class ActivityListenerDelegatesBuilder
        {
            public static Delegate CreateOnActivityStartedDelegate(Type activityType, MethodInfo onActivityStartedMethodInfo)
            {
                var dynMethod = new DynamicMethod(
                    "OnActivityStartedDyn",
                    typeof(void),
                    new[] { activityType },
                    typeof(ActivityListener).Module,
                    true);

                var activityProxyType = typeof(IActivity5);
                if (activityType.Assembly.GetName().Version?.Major is 6)
                {
                    activityProxyType = typeof(IActivity6);
                }

                var proxyResult = DuckType.GetOrCreateProxyType(activityProxyType, activityType);
                var method = onActivityStartedMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(activityType));
            }

            public static Delegate CreateOnActivityStoppedDelegate(Type activityType, MethodInfo onActivityStoppedMethodInfo)
            {
                var dynMethod = new DynamicMethod(
                    "OnActivityStoppedDyn",
                    typeof(void),
                    new[] { activityType },
                    typeof(ActivityListener).Module,
                    true);

                var activityProxyType = typeof(IActivity5);
                if (activityType.Assembly.GetName().Version?.Major is 6)
                {
                    activityProxyType = typeof(IActivity6);
                }

                var proxyResult = DuckType.GetOrCreateProxyType(activityProxyType, activityType);
                var method = onActivityStoppedMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(activityType));
            }

            public static Delegate CreateOnSampleDelegate(Type activitySamplingResultType, Type activityCreationOptionsType, Type activityContextType, Type sampleActivityType, MethodInfo onSampleMethodInfo)
            {
                var dynMethod = new DynamicMethod(
                    "OnSampleDyn",
                    activitySamplingResultType,
                    new[] { activityCreationOptionsType.MakeGenericType(activityContextType).MakeByRefType() },
                    typeof(ActivityListener).Module,
                    true);

                var il = dynMethod.GetILGenerator();
                il.EmitCall(OpCodes.Call, onSampleMethodInfo, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(sampleActivityType.MakeGenericType(activityContextType));
            }

            public static Delegate CreateOnSampleUsingParentIdDelegate(Type activitySamplingResultType, Type activityCreationOptionsType, Type sampleActivityType, MethodInfo onSampleUsingParentIdMethodInfo)
            {
                var dynMethod = new DynamicMethod(
                    "OnSampleUsingParentIdDyn",
                    activitySamplingResultType,
                    new[] { activityCreationOptionsType.MakeGenericType(typeof(string)).MakeByRefType() },
                    typeof(ActivityListener).Module,
                    true);

                var il = dynMethod.GetILGenerator();
                il.EmitCall(OpCodes.Call, onSampleUsingParentIdMethodInfo, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(sampleActivityType.MakeGenericType(typeof(string)));
            }

            public static Delegate CreateOnShouldListenToDelegate(Type activitySourceType, MethodInfo onShouldListenToMethodInfo)
            {
                var dynMethod = new DynamicMethod(
                    "OnShouldListenToDyn",
                    typeof(bool),
                    new[] { activitySourceType },
                    typeof(ActivityListener).Module,
                    true);

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivitySource), activitySourceType);
                var method = onShouldListenToMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Func<,>).MakeGenericType(activitySourceType, typeof(bool)));
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
                    Log.Error(ex, "Error handling DiagnosticSourceEventListener event with sourcename");
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
                    if (Interlocked.CompareExchange(ref ActivityListener._stopped, 1, 1) == 1)
                    {
                        return;
                    }

                    _onNextActivityDelegate(_sourceName, value, _getCurrentActivity());
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling DiagnosticSourceEventListener event");
                }
            }
        }
    }
}

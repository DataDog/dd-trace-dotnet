// <copyright file="ActivityListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

#pragma warning disable SA1401
namespace Datadog.Trace.Activity
{
    internal class ActivityListener
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListener));

        private static readonly Type DiagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource");
        private static readonly Type ObserverDiagnosticListenerType = typeof(IObserver<>).MakeGenericType(DiagnosticListenerType);

        private static readonly MethodInfo OnSetListenerMethodInfo = typeof(ActivityListener).GetMethod("OnSetListener", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo OnNextActivityMethodInfo = typeof(ActivityListener).GetMethod("OnNextActivity", BindingFlags.Static | BindingFlags.NonPublic);

        private static readonly Type ActivityListenerType = Type.GetType("System.Diagnostics.ActivityListener, System.Diagnostics.DiagnosticSource");
        private static readonly Type ActivityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource");
        private static readonly Type ActivitySourceType = Type.GetType("System.Diagnostics.ActivitySource, System.Diagnostics.DiagnosticSource");
        private static readonly Type ActivityCreationOptionsType = Type.GetType("System.Diagnostics.ActivityCreationOptions`1, System.Diagnostics.DiagnosticSource");
        private static readonly Type ActivitySamplingResultType = Type.GetType("System.Diagnostics.ActivitySamplingResult, System.Diagnostics.DiagnosticSource");
        private static readonly Type ActivityContextType = Type.GetType("System.Diagnostics.ActivityContext, System.Diagnostics.DiagnosticSource");
        private static readonly Type SampleActivityType = Type.GetType("System.Diagnostics.SampleActivity`1, System.Diagnostics.DiagnosticSource");

        private static readonly MethodInfo OnActivityStartedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStarted", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo OnActivityStoppedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStopped", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo OnSampleMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSample", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo OnSampleUsingParentIdMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSampleUsingParentId", BindingFlags.Static | BindingFlags.Public);
        private static readonly MethodInfo OnShouldListenToMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnShouldListenTo", BindingFlags.Static | BindingFlags.Public);

        private static DiagnosticSourceEventListener _listener;
        private static Func<object> _getCurrentActivity;
        private static Action<KeyValuePair<string, object>, object> _onNextActivityDelegate;

        public static void Initialize()
        {
            if (DiagnosticListenerType is null)
            {
                return;
            }

            var diagnosticSourceAssemblyName = DiagnosticListenerType.Assembly.GetName();
            Log.Information($"DiagnosticSource: {diagnosticSourceAssemblyName.FullName}");

            var version = diagnosticSourceAssemblyName.Version;
            if (version.Major is 5 or 6)
            {
                CreateActivityListenerInstance();
                return;
            }

            if (version >= new Version(4, 0, 1))
            {
                CreateDiagnosticSourceListenerInstance();
                return;
            }

            Log.Information($"An activity listener was found but version {version} is not supported.");
        }

        private static void CreateActivityListenerInstance()
        {
            Log.Information($"Activity listener: {ActivityListenerType.AssemblyQualifiedName ?? "(null)"}");

            // Create the ActivityListener instance
            var activityListenerInstance = Activator.CreateInstance(ActivityListenerType);
            var activityListenerProxy = activityListenerInstance.DuckCast<IActivityListener>();

            Log.Information($"Activity Listener Proxy: {activityListenerProxy.GetType().FullName}");

            activityListenerProxy.ActivityStarted = ActivityListenerDelegatesBuilder.CreateOnActivityStartedDelegate();
            activityListenerProxy.ActivityStopped = ActivityListenerDelegatesBuilder.CreateOnActivityStoppedDelegate();
            activityListenerProxy.Sample = ActivityListenerDelegatesBuilder.CreateOnSampleDelegate();
            activityListenerProxy.SampleUsingParentId = ActivityListenerDelegatesBuilder.CreateOnSampleUsingParentIdDelegate();
            activityListenerProxy.ShouldListenTo = ActivityListenerDelegatesBuilder.CreateOnShouldListenToDelegate();

            var addActivityListenerMethodInfo = ActivitySourceType.GetMethod("AddActivityListener", BindingFlags.Static | BindingFlags.Public);
            addActivityListenerMethodInfo.Invoke(null, new[] { activityListenerInstance });
        }

        private static void CreateDiagnosticSourceListenerInstance()
        {
            Log.Information($"DiagnosticListener listener: {DiagnosticListenerType.AssemblyQualifiedName ?? "(null)"}");

            // Create Activity.Current delegate.
            var activityCurrentProperty = ActivityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            var activityCurrentMethodInfo = activityCurrentProperty?.GetMethod;
            var activityCurrentDynMethod = new DynamicMethod("ActivityCurrent", typeof(object), Type.EmptyTypes, typeof(ActivityListener).Module, true);
            var activityCurrentDynMethodIl = activityCurrentDynMethod.GetILGenerator();
            activityCurrentDynMethodIl.EmitCall(OpCodes.Call, activityCurrentMethodInfo, null);
            activityCurrentDynMethodIl.Emit(OpCodes.Ret);
            _getCurrentActivity = (Func<object>)activityCurrentDynMethod.CreateDelegate(typeof(Func<object>));

            // Create delegate for OnNext + Activity
            var onNextActivityDynMethod = new DynamicMethod("OnNextActivityDyn", typeof(void), new[] { typeof(KeyValuePair<string, object>), typeof(object) }, typeof(ActivityListener).Module, true);
            var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity), ActivityType);
            var method = OnNextActivityMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
            var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];
            var onNextActivityDynMethodIl = onNextActivityDynMethod.GetILGenerator();
            onNextActivityDynMethodIl.Emit(OpCodes.Ldarg_0);
            onNextActivityDynMethodIl.Emit(OpCodes.Ldarg_1);
            onNextActivityDynMethodIl.Emit(OpCodes.Newobj, proxyTypeCtor);
            onNextActivityDynMethodIl.EmitCall(OpCodes.Call, method, null);
            onNextActivityDynMethodIl.Emit(OpCodes.Ret);
            _onNextActivityDelegate = (Action<KeyValuePair<string, object>, object>)onNextActivityDynMethod.CreateDelegate(typeof(Action<KeyValuePair<string, object>, object>));

            // Initialize and subscribe to DiagnosticListener.AllListeners.Subscribe
            _listener = new DiagnosticSourceEventListener();
            var diagObserverType = CreateDiagnosticObserverType();
            var diagListener = Activator.CreateInstance(diagObserverType);
            var allListenersPropertyInfo = DiagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
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
                new[] { ObserverDiagnosticListenerType });

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
            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { DiagnosticListenerType });
            var onNextMethodIl = onNextMethod.GetILGenerator();
            onNextMethodIl.Emit(OpCodes.Ldarg_1);
            onNextMethodIl.EmitCall(OpCodes.Call, OnSetListenerMethodInfo, null);
            onNextMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo().AsType();
        }

        internal static void OnSetListener(object value)
        {
            ((IObservable<KeyValuePair<string, object>>)value).Subscribe(_listener);
        }

        internal static void OnNextActivity<T>(KeyValuePair<string, object> value, T activity)
            where T : IActivity
        {
            if (activity.Instance is null)
            {
                return;
            }

            try
            {
                var dotIndex = value.Key.LastIndexOf('.');
                var operationName = value.Key.Substring(0, dotIndex);
                var suffix = value.Key.Substring(dotIndex + 1);

                if (suffix.Equals("Start", StringComparison.Ordinal) && operationName == activity.OperationName)
                {
                    ActivityListenerHandler.OnActivityStarted(activity);
                }
                else if (suffix.Equals("Stop", StringComparison.Ordinal) && operationName == activity.OperationName)
                {
                    ActivityListenerHandler.OnActivityStopped(activity);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        internal class ActivityListenerDelegatesBuilder
        {
            public static Delegate CreateOnActivityStartedDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnActivityStartedDyn",
                    typeof(void),
                    new[] { ActivityType },
                    typeof(ActivityListener).Module,
                    true);

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity5), ActivityType);
                var method = OnActivityStartedMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(ActivityType));
            }

            public static Delegate CreateOnActivityStoppedDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnActivityStoppedDyn",
                    typeof(void),
                    new[] { ActivityType },
                    typeof(ActivityListener).Module,
                    true);

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity5), ActivityType);
                var method = OnActivityStoppedMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Action<>).MakeGenericType(ActivityType));
            }

            public static Delegate CreateOnSampleDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnSampleDyn",
                    ActivitySamplingResultType,
                    new[] { ActivityCreationOptionsType.MakeGenericType(ActivityContextType).MakeByRefType() },
                    typeof(ActivityListener).Module,
                    true);

                var il = dynMethod.GetILGenerator();
                il.EmitCall(OpCodes.Call, OnSampleMethodInfo, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(SampleActivityType.MakeGenericType(ActivityContextType));
            }

            public static Delegate CreateOnSampleUsingParentIdDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnSampleUsingParentIdDyn",
                    ActivitySamplingResultType,
                    new[] { ActivityCreationOptionsType.MakeGenericType(typeof(string)).MakeByRefType() },
                    typeof(ActivityListener).Module,
                    true);

                var il = dynMethod.GetILGenerator();
                il.EmitCall(OpCodes.Call, OnSampleUsingParentIdMethodInfo, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(SampleActivityType.MakeGenericType(typeof(string)));
            }

            public static Delegate CreateOnShouldListenToDelegate()
            {
                var dynMethod = new DynamicMethod(
                    "OnShouldListenToDyn",
                    typeof(bool),
                    new[] { ActivitySourceType },
                    typeof(ActivityListener).Module,
                    true);

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivitySource), ActivitySourceType);
                var method = OnShouldListenToMethodInfo.MakeGenericMethod(proxyResult.ProxyType);
                var proxyTypeCtor = proxyResult.ProxyType.GetConstructors()[0];

                var il = dynMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Newobj, proxyTypeCtor);
                il.EmitCall(OpCodes.Call, method, null);
                il.Emit(OpCodes.Ret);

                return dynMethod.CreateDelegate(typeof(Func<,>).MakeGenericType(ActivitySourceType, typeof(bool)));
            }
        }

        internal class DiagnosticSourceEventListener : IObserver<KeyValuePair<string, object>>
        {
            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void OnNext(KeyValuePair<string, object> value)
            {
                _onNextActivityDelegate(value, _getCurrentActivity());
            }
        }
    }
}

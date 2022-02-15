// <copyright file="ActivityListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal class ActivityListener
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListener));

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

        public static void Initialize()
        {
            Log.Information($"Activity listener: {ActivityListenerType?.AssemblyQualifiedName ?? "(null)"}");

            if (ActivityListenerType is null)
            {
                return;
            }

            var version = ActivityListenerType.Assembly.GetName().Version;
            if (version.Major is 5 or 6)
            {
                CreateActivityListenerInstance();
                return;
            }

            if (version >= new Version(4, 4, 1))
            {
                // Create the compatibility with 4.4.x
                return;
            }

            Log.Information($"An activity listener was found but version {version} is not supported.");
            return;
        }

        private static void CreateActivityListenerInstance()
        {
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

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity), ActivityType);
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

                var proxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity), ActivityType);
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
    }
}

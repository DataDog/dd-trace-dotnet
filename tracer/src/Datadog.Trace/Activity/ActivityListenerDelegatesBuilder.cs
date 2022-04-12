// <copyright file="ActivityListenerDelegatesBuilder.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.Activity
{
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
            if (activityType.Assembly.GetName().Version?.Major >= 6)
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
            if (activityType.Assembly.GetName().Version?.Major >= 6)
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
}

// <copyright file="DiagnosticObserverListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Activity
{
    internal static class DiagnosticObserverListener
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticObserverListener));
        private static readonly Action<object> OnShouldListenToDelegate;

        static DiagnosticObserverListener()
        {
            try
            {
                var diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource");
                var sourceProxyResult = DuckType.GetOrCreateProxyType(typeof(ISource), diagnosticListenerType);

                var onShouldListenToMethodInfo = typeof(ActivityListenerHandler).GetMethod(nameof(ActivityListenerHandler.OnShouldListenTo), BindingFlags.Static | BindingFlags.Public);
                if (onShouldListenToMethodInfo is null)
                {
                    throw new NullReferenceException("ActivityListenerHandler.OnShouldListenTo cannot be found.");
                }

                var onShouldListenToMethod = onShouldListenToMethodInfo.MakeGenericMethod(sourceProxyResult.ProxyType);

                var onShouldListenToResultMethodInfo = typeof(DiagnosticObserverListener).GetMethod(nameof(DiagnosticObserverListener.OnShouldListenToResult), BindingFlags.Static | BindingFlags.Public);
                if (onShouldListenToResultMethodInfo is null)
                {
                    throw new NullReferenceException("DiagnosticObserverListener.OnShouldListenToResult cannot be found.");
                }

                var onShouldListenToResultMethod = onShouldListenToResultMethodInfo.MakeGenericMethod(sourceProxyResult.ProxyType);

                // Create delegate for OnShouldListenTo<T> where T is Source
                var onShouldListenToDyn = new DynamicMethod("OnShouldListenToDyn", typeof(void), new[] { typeof(object) }, typeof(ActivityListener).Module, true);
                var sourceProxyResultProxyTypeCtor = sourceProxyResult.ProxyType.GetConstructors()[0];
                var onShouldListenToDynIl = onShouldListenToDyn.GetILGenerator();
                onShouldListenToDynIl.Emit(OpCodes.Ldarg_0);
                onShouldListenToDynIl.Emit(OpCodes.Newobj, sourceProxyResultProxyTypeCtor);
                onShouldListenToDynIl.Emit(OpCodes.Dup);
                onShouldListenToDynIl.EmitCall(OpCodes.Call, onShouldListenToMethod, null);
                onShouldListenToDynIl.EmitCall(OpCodes.Call, onShouldListenToResultMethod, null);
                onShouldListenToDynIl.Emit(OpCodes.Ret);
                OnShouldListenToDelegate = (Action<object>)onShouldListenToDyn.CreateDelegate(typeof(Action<object>));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error on DiagnosticObserverListener static constructor");
                OnShouldListenToDelegate = o => { };
            }
        }

        public static void OnSetListener(object value)
        {
            try
            {
                OnShouldListenToDelegate(value);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }
        }

        public static void OnShouldListenToResult<T>(T source, bool shouldListen)
            where T : ISource
        {
            if (shouldListen)
            {
                ((IObservable<KeyValuePair<string, object>>)source.Instance).Subscribe(new DiagnosticSourceEventListener(source.Name));
            }
        }
    }
}

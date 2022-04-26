// <copyright file="DiagnosticSourceEventListener.cs" company="Datadog">
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
    internal class DiagnosticSourceEventListener : IObserver<KeyValuePair<string, object>>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticObserverListener));
        private static readonly Action<string, KeyValuePair<string, object>, object?> OnNextActivityDelegate;
        private readonly string _sourceName;

        static DiagnosticSourceEventListener()
        {
            try
            {
                var activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource", throwOnError: true);
                var onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod(nameof(DiagnosticSourceEventListener.OnNextActivity), BindingFlags.Static | BindingFlags.NonPublic);
                if (onNextActivityMethodInfo is null)
                {
                    throw new NullReferenceException("DiagnosticSourceEventListener.OnNextActivity cannot be found.");
                }

                // Create delegate for OnNext + Activity
                var onNextActivityDynMethod = new DynamicMethod("OnNextActivityDyn", typeof(void), new[] { typeof(string), typeof(KeyValuePair<string, object>), typeof(object) }, typeof(ActivityListener).Module, true);

                DuckType.CreateTypeResult onNextActivityProxyResult;
                if (activityType!.GetField("_traceId", DuckAttribute.DefaultFlags) is not null)
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
                OnNextActivityDelegate = (Action<string, KeyValuePair<string, object>, object?>)onNextActivityDynMethod.CreateDelegate(typeof(Action<string, KeyValuePair<string, object>, object?>));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error on DiagnosticSourceEventListener static constructor");
                OnNextActivityDelegate = (s, pair, arg3) => { };
            }
        }

        public DiagnosticSourceEventListener(string sourceName)
        {
            _sourceName = sourceName;
        }

        internal static void OnNextActivity<T>(string sourceName, KeyValuePair<string, object> value, T activity)
            where T : struct, IActivity
        {
            try
            {
                var dotIndex = value.Key.LastIndexOf('.');
                if (dotIndex == -1)
                {
                    return;
                }

                if (activity.Instance != null && activity.OperationName.Length != dotIndex && string.Compare(activity.OperationName, 0, value.Key, 0, dotIndex, StringComparison.Ordinal) != 0)
                {
                    // Activity is not associated with the event we received.
                    // clearing the Activity variable.
                    activity = default;
                }

                if (value.Key.Length == dotIndex + 5 + 1 && value.Key.LastIndexOf("Start", StringComparison.Ordinal) == dotIndex + 1 && activity.Instance is not null)
                {
                    ActivityListenerHandler.OnActivityWithSourceStarted(sourceName, activity);
                }
                else if (value.Key.Length == dotIndex + 4 + 1 && value.Key.LastIndexOf("Stop", StringComparison.Ordinal) == dotIndex + 1)
                {
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticSourceEventListener event with {sourceName}", sourceName);
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
                if (ActivityListener.IsRunning)
                {
                    OnNextActivityDelegate(_sourceName, value, ActivityListener.GetCurrentActivityObject());
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticSourceEventListener event");
            }
        }
    }
}

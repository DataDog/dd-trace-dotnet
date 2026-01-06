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
using Datadog.Trace.Util;

namespace Datadog.Trace.Activity
{
    internal sealed class DiagnosticSourceEventListener : IObserver<KeyValuePair<string, object>>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticSourceEventListener));
        private static readonly Action<string, KeyValuePair<string, object>, object?> OnNextActivityDelegate;
        private readonly string _sourceName;

        private enum OnNextOperation
        {
            Ignored,
            OnNextStart,
            OnNextStop,
            OnNextStopInvalidActivity,
        }

        static DiagnosticSourceEventListener()
        {
            try
            {
                // Based on the version of Activity we have available, we use one of the specialized versions of activity
                // Making this one-time check reduces the need for if/as checks in the downstream code, and
                // in turn reduces all the boxing we are otherwise required to do
                var activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource", throwOnError: true);
                if (activityType is null)
                {
                    ThrowHelper.ThrowNullReferenceException("System.Diagnostics.Activity cannot be found.");
                }

                MethodInfo? onNextActivityMethodInfo;
                DuckType.CreateTypeResult onNextActivityProxyResult;
                if (activityType.GetProperty(nameof(IActivity6.StatusDescription), DuckAttribute.DefaultFlags) is not null)
                {
                    onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity6), activityType);
                    onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod(nameof(OnNextActivity6), BindingFlags.Static | BindingFlags.NonPublic);
                }
                else if (activityType.GetProperty(nameof(IActivity5.DisplayName), DuckAttribute.DefaultFlags) is not null)
                {
                    onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity5), activityType);
                    onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod(nameof(OnNextActivity5), BindingFlags.Static | BindingFlags.NonPublic);
                }
                else if (activityType.GetField("_traceId", DuckAttribute.DefaultFlags) is not null)
                {
                    onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IW3CActivity), activityType);
                    onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod(nameof(OnNextActivityW3C), BindingFlags.Static | BindingFlags.NonPublic);
                }
                else
                {
                    onNextActivityProxyResult = DuckType.GetOrCreateProxyType(typeof(IActivity), activityType);
                    onNextActivityMethodInfo = typeof(DiagnosticSourceEventListener).GetMethod(nameof(OnNextActivityBasic), BindingFlags.Static | BindingFlags.NonPublic);
                }

                if (onNextActivityMethodInfo is null)
                {
                    ThrowHelper.ThrowNullReferenceException("DiagnosticSourceEventListener.OnNextActivity cannot be found.");
                }

                var onNextActivityProxyResultProxyType = onNextActivityProxyResult.ProxyType;
                if (onNextActivityProxyResultProxyType is null)
                {
                    ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {activityType} is null");
                }

                // Create delegate for OnNext + Activity
                var onNextActivityDynMethod = new DynamicMethod("OnNextActivityDyn", typeof(void), [typeof(string), typeof(KeyValuePair<string, object>), typeof(object)], typeof(ActivityListener).Module, skipVisibility: true);
                var onNextActivityMethod = onNextActivityMethodInfo.MakeGenericMethod(onNextActivityProxyResultProxyType);
                var onNextActivityProxyTypeCtor = onNextActivityProxyResultProxyType.GetConstructors()[0];
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

        internal static void OnNextActivityBasic<T>(string sourceName, KeyValuePair<string, object> value, T activity)
            where T : struct, IActivity
        {
            try
            {
                var operation = TryGetOperationToRun(value, activity.Instance, activity.OperationName);
                if (operation == OnNextOperation.OnNextStart)
                {
                    ActivityListenerHandler.OnActivityWithSourceStarted(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStop)
                {
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStopInvalidActivity)
                {
                    activity = default;
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticSourceEventListener.OnNextActivityBasic event with {SourceName}", sourceName);
            }
        }

        internal static void OnNextActivityW3C<T>(string sourceName, KeyValuePair<string, object> value, T activity)
            where T : struct, IW3CActivity
        {
            try
            {
                var operation = TryGetOperationToRun(value, activity.Instance, activity.OperationName);
                if (operation == OnNextOperation.OnNextStart)
                {
                    ActivityListenerHandler.OnActivityWithSourceStarted(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStop)
                {
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStopInvalidActivity)
                {
                    activity = default;
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticSourceEventListener.OnNextActivityW3C event with {SourceName}", sourceName);
            }
        }

        internal static void OnNextActivity5<T>(string sourceName, KeyValuePair<string, object> value, T activity)
            where T : struct, IActivity5
        {
            try
            {
                var operation = TryGetOperationToRun(value, activity.Instance, activity.OperationName);
                if (operation == OnNextOperation.OnNextStart)
                {
                    ActivityListenerHandler.OnActivityWithSourceStarted(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStop)
                {
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStopInvalidActivity)
                {
                    activity = default;
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticSourceEventListener.OnNextActivity5 event with {SourceName}", sourceName);
            }
        }

        internal static void OnNextActivity6<T>(string sourceName, KeyValuePair<string, object> value, T activity)
            where T : struct, IActivity6
        {
            try
            {
                var operation = TryGetOperationToRun(value, activity.Instance, activity.OperationName);
                if (operation == OnNextOperation.OnNextStart)
                {
                    ActivityListenerHandler.OnActivityWithSourceStarted(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStop)
                {
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
                else if (operation == OnNextOperation.OnNextStopInvalidActivity)
                {
                    activity = default;
                    ActivityListenerHandler.OnActivityWithSourceStopped(sourceName, activity);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticSourceEventListener.OnNextActivity6 event with {SourceName}", sourceName);
            }
        }

        private static OnNextOperation TryGetOperationToRun(KeyValuePair<string, object> value, object? activityInstance, string? activityOperationName)
        {
            var dotIndex = value.Key.LastIndexOf('.');
            if (dotIndex == -1)
            {
                return OnNextOperation.Ignored;
            }

            var activityIsInvalid =
                activityInstance is not null
             && activityOperationName is not null
             && activityOperationName.Length != dotIndex
             && string.Compare(activityOperationName, 0, value.Key, 0, dotIndex, StringComparison.Ordinal) != 0;

            if (!activityIsInvalid && value.Key.Length == dotIndex + 5 + 1 && value.Key.LastIndexOf("Start", StringComparison.Ordinal) == dotIndex + 1)
            {
                return OnNextOperation.OnNextStart;
            }
            else if (value.Key.Length == dotIndex + 4 + 1 && value.Key.LastIndexOf("Stop", StringComparison.Ordinal) == dotIndex + 1)
            {
                return activityIsInvalid ? OnNextOperation.OnNextStopInvalidActivity : OnNextOperation.OnNextStop;
            }

            return OnNextOperation.Ignored;
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

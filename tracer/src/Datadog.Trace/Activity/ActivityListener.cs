// <copyright file="ActivityListener.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Activity
{
    internal static class ActivityListener
    {
        private const int InitializationBackoffPerRetry = 10000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ActivityListener));

        private static int _initializationRetries = 5;

        private static object? _activityListenerInstance;
        private static Func<object>? _getCurrentActivity;
        private static Action<object, int>? _setKindProperty;

        private static int _initialized = 0;
        private static int _stopped = 0;

        public static bool IsRunning
        {
            get
            {
                return Interlocked.CompareExchange(ref _initialized, 1, 1) == 1 &&
                       Interlocked.CompareExchange(ref _stopped, 0, 0) == 0;
            }
        }

        internal static object? GetCurrentActivityObject()
        {
            try
            {
                return _getCurrentActivity?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calling Activity.Current");
            }

            return null;
        }

        public static IActivity? GetCurrentActivity()
        {
            var activity = GetCurrentActivityObject();

            if (activity is null)
            {
                return null;
            }

            return activity.DuckAs<IActivity6>() ??
                   activity.DuckAs<IActivity5>() ??
                   activity.DuckAs<IW3CActivity>() ??
                   activity.DuckAs<IActivity>();
        }

        public static void SetActivityKind(IActivity5 activity, ActivityKind activityKind)
        {
            if (activity.Instance is null)
            {
                return;
            }

            try
            {
                _setKindProperty?.Invoke(activity.Instance, (int)activityKind);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error calling Activity.SetKindProperty");
            }
        }

        public static void Initialize() => Initialize(CancellationToken.None);

        public static void Initialize(CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            // Try to resolve System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource
            var diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource", throwOnError: false);
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
                        Initialize(cancellationToken);
                    },
                        cancellationToken,
                        TaskContinuationOptions.None,
                        TaskScheduler.Default);
                }

                return;
            }

            // Initialize
            var diagnosticSourceAssemblyName = diagnosticListenerType.Assembly.GetName();
            Log.Information("DiagnosticSource: {DiagnosticSourceAssemblyNameFullName}", diagnosticSourceAssemblyName.FullName);

            var activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource", throwOnError: false);
            var version = diagnosticSourceAssemblyName.Version;

            // Check version: First version where the Activity objects has traceId and spanId
            if (version >= new Version(4, 0, 4))
            {
                if (activityType is null)
                {
                    Log.Error("The Activity type cannot be found.");
                    return;
                }

                CreateCurrentActivityDelegates(activityType);
                ChangeActivityDefaultFormat(activityType);

                if (version.Major >= 5)
                {
                    // if Version >= 5 loaded (Uses ActivityListener implementation)
                    CreateActivityListenerInstance(activityType);
                    var activityKindType = Type.GetType("System.Diagnostics.ActivityKind, System.Diagnostics.DiagnosticSource", throwOnError: false);
                    if (activityKindType is not null)
                    {
                        CreateActivityKindSetter(activityType);
                    }
                }
                else
                {
                    // if Version is 4.0.4 or greater (Uses DiagnosticListener implementation / Nuget version 4.6.0)
                    DiagnosticListeners.DiagnosticObserverFactory.SubscribeWithStaticCallback(
                                                                            diagnosticListenerType,
                                                                            typeof(DiagnosticObserverListener),
                                                                            nameof(DiagnosticObserverListener.OnSetListener),
                                                                            "Datadog.DiagnosticObserverListener.Dynamic",
                                                                            typeof(ActivityListener));
                }

                return;
            }

            Log.Information("An activity listener was found but version {Version} is not supported.", version?.ToString() ?? "(null)");

            static void CreateCurrentActivityDelegates(Type activityType)
            {
                // Create Activity.Current delegate.
                var activityCurrentMethodInfo = activityType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetMethod;
                if (activityCurrentMethodInfo is null)
                {
                    throw new NullReferenceException("Activity.Current property cannot be found in the Activity type.");
                }

                var activityCurrentDynMethod = new DynamicMethod("ActivityCurrent", typeof(object), Type.EmptyTypes, typeof(ActivityListener).Module, true);
                var activityCurrentDynMethodIl = activityCurrentDynMethod.GetILGenerator();
                activityCurrentDynMethodIl.EmitCall(OpCodes.Call, activityCurrentMethodInfo, null);
                activityCurrentDynMethodIl.Emit(OpCodes.Ret);
                _getCurrentActivity = (Func<object>)activityCurrentDynMethod.CreateDelegate(typeof(Func<object>));
            }

            static void ChangeActivityDefaultFormat(Type activityType)
            {
                // Instantiate an (unused) Activity object so we can use DuckTyping to update the static property Activity.DefaultIdFormat, changing the default ID format to W3C (so traceid and spanid gets populated).
                // Note: We're specifically passing in a non-empty string parameter to the Activity constructor because the constructor may throw an exception if the string is null or empty.
                if (Activator.CreateInstance(activityType, "operationName").TryDuckCast<IActivityFormat>(out var activityFormat))
                {
                    activityFormat.DefaultIdFormat = ActivityIdFormat.W3C;
                }
            }
        }

        public static void StopListeners()
        {
            // If there's an activity listener instance we dispose the instance and clear it.
            if (_activityListenerInstance is IDisposable disposableListener)
            {
                _activityListenerInstance = null;
                disposableListener.Dispose();
                Interlocked.Exchange(ref _stopped, 1);
            }
            else
            {
                Log.Error("ActivityListener cannot be cast as IDisposable.");
            }
        }

        private static void CreateActivityListenerInstance(Type activityType)
        {
            var activityListenerType = Type.GetType("System.Diagnostics.ActivityListener, System.Diagnostics.DiagnosticSource", throwOnError: true)!;
            var activitySourceType = Type.GetType("System.Diagnostics.ActivitySource, System.Diagnostics.DiagnosticSource", throwOnError: true)!;
            var activityCreationOptionsType = Type.GetType("System.Diagnostics.ActivityCreationOptions`1, System.Diagnostics.DiagnosticSource", throwOnError: true)!;
            var activitySamplingResultType = Type.GetType("System.Diagnostics.ActivitySamplingResult, System.Diagnostics.DiagnosticSource", throwOnError: true)!;
            var activityContextType = Type.GetType("System.Diagnostics.ActivityContext, System.Diagnostics.DiagnosticSource", throwOnError: true)!;
            var sampleActivityType = Type.GetType("System.Diagnostics.SampleActivity`1, System.Diagnostics.DiagnosticSource", throwOnError: true)!;

            var onActivityStartedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStarted", BindingFlags.Static | BindingFlags.Public)!;
            var onActivityStoppedMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnActivityStopped", BindingFlags.Static | BindingFlags.Public)!;
            var onSampleMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSample", BindingFlags.Static | BindingFlags.Public)!;
            var onSampleUsingParentIdMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnSampleUsingParentId", BindingFlags.Static | BindingFlags.Public)!;
            var onShouldListenToMethodInfo = typeof(ActivityListenerHandler).GetMethod("OnShouldListenTo", BindingFlags.Static | BindingFlags.Public)!;

            Log.Information("Activity listener: {ActivityListenerType}", activityListenerType!.AssemblyQualifiedName ?? "(null)");

            // Create the ActivityListener instance
            var activityListener = Activator.CreateInstance(activityListenerType);
            var activityListenerProxy = activityListener.DuckCast<IActivityListener>();
            if (activityListenerProxy is null)
            {
                ThrowHelper.ThrowNullReferenceException($"Resulting proxy type after ducktyping {activityListenerType} is null");
            }

            activityListenerProxy.ActivityStarted = ActivityListenerDelegatesBuilder.CreateOnActivityStartedDelegate(activityType, onActivityStartedMethodInfo);
            activityListenerProxy.ActivityStopped = ActivityListenerDelegatesBuilder.CreateOnActivityStoppedDelegate(activityType, onActivityStoppedMethodInfo);
            activityListenerProxy.Sample = ActivityListenerDelegatesBuilder.CreateOnSampleDelegate(activitySamplingResultType, activityCreationOptionsType, activityContextType, sampleActivityType, onSampleMethodInfo);
            activityListenerProxy.SampleUsingParentId = ActivityListenerDelegatesBuilder.CreateOnSampleUsingParentIdDelegate(activitySamplingResultType, activityCreationOptionsType, sampleActivityType, onSampleUsingParentIdMethodInfo);
            activityListenerProxy.ShouldListenTo = ActivityListenerDelegatesBuilder.CreateOnShouldListenToDelegate(activitySourceType, onShouldListenToMethodInfo);

            var addActivityListenerMethodInfo = activitySourceType!.GetMethod("AddActivityListener", BindingFlags.Static | BindingFlags.Public);
            if (addActivityListenerMethodInfo is null)
            {
                ThrowHelper.ThrowNullReferenceException("ActivitySource.AddActivityListener method cannot be found.");
            }

            addActivityListenerMethodInfo.Invoke(null, new[] { activityListener });

            // Set the global field after calling the `AddActivityListener` method
            _activityListenerInstance = activityListener;
        }

        private static void CreateActivityKindSetter(Type activityType)
        {
            var activityKindProperty = activityType.GetProperty("Kind");
            if (activityKindProperty is null)
            {
                Log.Warning("Activity Kind property was not found.");
                return;
            }

            if (activityKindProperty?.SetMethod == null)
            {
                Log.Warning("ActivityKind did not have a setter.");
                return;
            }

            // Create dynamic method: (object activity, int kind) => activity.Kind = kind
            var dynMethod = new DynamicMethod(
                "ActivityKindSetter",
                null,
                [typeof(object), typeof(int)],
                typeof(DuckType).Module,
                skipVisibility: true);

            var il = dynMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);                  // Load activity object
            il.Emit(OpCodes.Castclass, activityType);  // Cast to Activity type
            il.Emit(OpCodes.Ldarg_1);                  // Load kind int
            il.Emit(OpCodes.Call, activityKindProperty.SetMethod); // Call Kind property setter
            il.Emit(OpCodes.Ret);
            _setKindProperty = (Action<object, int>)dynMethod.CreateDelegate(typeof(Action<object, int>));
        }
    }
}

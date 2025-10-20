// <copyright file="QuartzDiagnosticObserver.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Datadog.Trace.Activity;
using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

#nullable enable

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Instruments Quartz.NET job scheduler.
    /// <para/>
    /// This observer listens to Quartz diagnostic events to trace job execution,
    /// scheduling, and other Quartz-related operations.
    /// </summary>
#if !NETFRAMEWORK
    internal sealed class QuartzDiagnosticObserver : DiagnosticObserver
    {
        private const string DiagnosticListenerName = "Quartz";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<QuartzDiagnosticObserver>();

        protected override string ListenerName => DiagnosticListenerName;

        protected override void OnNext(string eventName, object arg)
        {
            HandleEvent(eventName, arg);
        }

        /// <summary>
        /// Handles Quartz diagnostic events. This method is shared between .NET Framework and modern .NET.
        /// </summary>
        private static void HandleEvent(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Quartz.Job.Execute.Start":
                case "Quartz.Job.Veto.Start":
                    var currentActivity = ActivityListener.GetCurrentActivity();
                    if (currentActivity is IActivity5 activity5)
                    {
                        QuartzCommon.EnhanceActivityMetadata(activity5);
                        QuartzCommon.SetActivityKind(activity5);
                    }
                    else
                    {
                        Log.Debug("The activity was not Activity5 (Less than .NET 5.0). Unable to enhance the span metadata.");
                    }

                    break;
                case "Quartz.Job.Execute.Stop":
                case "Quartz.Job.Veto.Stop":
                    break;
                case "Quartz.Job.Execute.Exception":
                case "Quartz.Job.Veto.Exception":
                    // setting an exception manually
                    var closingActivity = ActivityListener.GetCurrentActivity();
                    if (closingActivity?.Instance is not null)
                    {
                        QuartzCommon.AddException(arg, closingActivity);
                    }

                    break;
            }
        }
    }
#else
    internal static class QuartzDiagnosticObserver
    {
        private const string DiagnosticListenerName = "Quartz";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(QuartzDiagnosticObserver));

        private static int _initialized = 0;
        private static IDisposable? _subscription;

        /// <summary>
        /// Duck type interface for DiagnosticListener to access Name and Subscribe without compile-time dependency.
        /// </summary>
        public interface IDiagnosticListener : IDuckType
        {
            string Name { get; }

            IDisposable? Subscribe(IObserver<KeyValuePair<string, object>> observer);
        }

        /// <summary>
        /// Initialize the Quartz diagnostic observer for .NET Framework.
        /// This method uses reflection to subscribe to DiagnosticListener without a compile-time dependency.
        /// </summary>
        public static void Initialize()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) == 1)
            {
                return;
            }

            try
            {
                // Try to resolve System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource
                var diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource", throwOnError: false);
                if (diagnosticListenerType is null)
                {
                    Log.Debug("System.Diagnostics.DiagnosticListener not found. Quartz diagnostic instrumentation will not be available.");
                    return;
                }

                var keyValuePairType = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), typeof(object));
                var observerKeyValuePairType = typeof(IObserver<>).MakeGenericType(keyValuePairType);
                var observerDiagnosticListenerType = typeof(IObserver<>).MakeGenericType(diagnosticListenerType);

                // Create the dynamic observer types
                var diagnosticListenerObserverType = CreateDiagnosticListenerObserverType(diagnosticListenerType, observerDiagnosticListenerType, observerKeyValuePairType, keyValuePairType);
                if (diagnosticListenerObserverType is null)
                {
                    Log.Warning("Failed to create DiagnosticListener observer type for Quartz instrumentation.");
                    return;
                }

                // Subscribe to DiagnosticListener.AllListeners
                var diagnosticListenerInstance = Activator.CreateInstance(diagnosticListenerObserverType);
                var allListenersPropertyInfo = diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
                if (allListenersPropertyInfo is null)
                {
                    Log.Warning("DiagnosticListener.AllListeners property not found.");
                    return;
                }

                var subscribeMethodInfo = allListenersPropertyInfo.PropertyType.GetMethod("Subscribe", new[] { observerDiagnosticListenerType });
                if (subscribeMethodInfo is null)
                {
                    Log.Warning("Subscribe method not found on DiagnosticListener.AllListeners.");
                    return;
                }

                _subscription = subscribeMethodInfo.Invoke(allListenersPropertyInfo.GetValue(null), new[] { diagnosticListenerInstance }) as IDisposable;
                Log.Debug("Quartz diagnostic observer initialized successfully for .NET Framework.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Quartz diagnostic observer for .NET Framework.");
                Interlocked.Exchange(ref _initialized, 0);
            }
        }

        /// <summary>
        /// Stop and dispose the Quartz diagnostic observer.
        /// </summary>
        public static void Stop()
        {
            if (_subscription is not null)
            {
                _subscription.Dispose();
                _subscription = null;
            }
        }

        /// <summary>
        /// Called when a DiagnosticListener is encountered.
        /// Checks if it's the Quartz listener and subscribes to it.
        /// </summary>
        public static void OnDiagnosticListenerNext(object diagnosticListener)
        {
            try
            {
                // Use duck typing to get the Name property and Subscribe method
                if (diagnosticListener.DuckCast<IDiagnosticListener>() is not { } listener)
                {
                    return;
                }

                if (!string.Equals(listener.Name, DiagnosticListenerName, StringComparison.Ordinal))
                {
                    return;
                }

                Log.Debug("Found Quartz DiagnosticListener, subscribing to events.");

                // Subscribe to this specific listener's events
                var observerInstance = listener.Subscribe(new QuartzEventObserver());
                if (observerInstance is not null)
                {
                    Log.Debug("Successfully subscribed to Quartz DiagnosticListener.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error subscribing to Quartz DiagnosticListener.");
            }
        }

        /// <summary>
        /// Creates a dynamic type that implements IObserver&lt;DiagnosticListener&gt;
        /// to receive notifications about new DiagnosticListeners.
        /// </summary>
        private static Type? CreateDiagnosticListenerObserverType(
            Type diagnosticListenerType,
            Type observerDiagnosticListenerType,
            Type observerKeyValuePairType,
            Type keyValuePairType)
        {
            var assemblyName = new AssemblyName("Datadog.QuartzDiagnosticObserver.Dynamic");
            assemblyName.Version = typeof(QuartzDiagnosticObserver).Assembly.GetName().Version;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            DuckType.EnsureTypeVisibility(moduleBuilder, typeof(QuartzDiagnosticObserver));

            var typeBuilder = moduleBuilder.DefineType(
                "QuartzDiagnosticListenerObserver",
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

            // OnNext - calls QuartzDiagnosticObserver.OnDiagnosticListenerNext
            var onDiagnosticListenerNextMethodInfo = typeof(QuartzDiagnosticObserver).GetMethod(
                nameof(OnDiagnosticListenerNext),
                BindingFlags.Static | BindingFlags.Public);
            if (onDiagnosticListenerNextMethodInfo is null)
            {
                return null;
            }

            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { diagnosticListenerType });
            var onNextMethodIl = onNextMethod.GetILGenerator();
            onNextMethodIl.Emit(OpCodes.Ldarg_1);
            onNextMethodIl.EmitCall(OpCodes.Call, onDiagnosticListenerNextMethodInfo, null);
            onNextMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()?.AsType();
        }

        /// <summary>
        /// Handles Quartz diagnostic events for .NET Framework.
        /// </summary>
        private static void HandleEvent(string eventName, object arg)
        {
            switch (eventName)
            {
                case "Quartz.Job.Execute.Start":
                case "Quartz.Job.Veto.Start":
                    var currentActivity = ActivityListener.GetCurrentActivity();
                    if (currentActivity is IActivity5 activity5)
                    {
                        QuartzCommon.EnhanceActivityMetadata(activity5);
                        QuartzCommon.SetActivityKind(activity5);
                    }
                    else
                    {
                        Log.Debug("The activity was not Activity5 (Less than .NET 5.0). Unable to enhance the span metadata.");
                    }

                    break;
                case "Quartz.Job.Execute.Stop":
                case "Quartz.Job.Veto.Stop":
                    break;
                case "Quartz.Job.Execute.Exception":
                case "Quartz.Job.Veto.Exception":
                    // setting an exception manually
                    var closingActivity = ActivityListener.GetCurrentActivity();
                    if (closingActivity?.Instance is not null)
                    {
                        QuartzCommon.AddException(arg, closingActivity);
                    }

                    break;
            }
        }

        /// <summary>
        /// Observer class that handles Quartz diagnostic events.
        /// This class implements IObserver&lt;KeyValuePair&lt;string, object&gt;&gt; directly
        /// because on .NET Framework we have System.Diagnostics.DiagnosticSource available at runtime.
        /// </summary>
        private sealed class QuartzEventObserver : IObserver<KeyValuePair<string, object>>
        {
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
                    HandleEvent(value.Key, value.Value);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error handling Quartz event: {EventName}", value.Key);
                }
            }
        }
    }
#endif
}

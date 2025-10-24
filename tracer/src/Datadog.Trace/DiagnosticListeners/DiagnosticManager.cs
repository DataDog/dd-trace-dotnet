// <copyright file="DiagnosticManager.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.DiagnosticListeners.DuckTypes;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.DiagnosticListeners
{
    internal sealed class DiagnosticManager : IDiagnosticManager, IObserver<IDiagnosticListener>, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<DiagnosticManager>();

        private readonly IEnumerable<DiagnosticObserver> _diagnosticObservers;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private IDisposable? _allListenersSubscription;

        public DiagnosticManager(IEnumerable<DiagnosticObserver> diagnosticSubscribers)
        {
            if (diagnosticSubscribers == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(diagnosticSubscribers));
            }

            _diagnosticObservers = diagnosticSubscribers;
        }

        public static DiagnosticManager? Instance { get; set; }

        public bool IsRunning => _allListenersSubscription != null;

        public void Start()
        {
            if (_allListenersSubscription == null)
            {
                Log.Debug("Starting DiagnosticListener.AllListeners subscription");

                try
                {
                    // Get the DiagnosticListener type
                    var diagnosticListenerType = Type.GetType("System.Diagnostics.DiagnosticListener, System.Diagnostics.DiagnosticSource");
                    if (diagnosticListenerType == null)
                    {
                        Log.Warning("Unable to find DiagnosticListener type");
                        return;
                    }

                    // Get the AllListeners static property
                    var allListenersProperty = diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
                    if (allListenersProperty == null)
                    {
                        Log.Warning("Unable to find DiagnosticListener.AllListeners property");
                        return;
                    }

                    // Get the value (IObservable<DiagnosticListener>)
                    var allListenersObservable = allListenersProperty.GetValue(null);
                    if (allListenersObservable == null)
                    {
                        Log.Warning("DiagnosticListener.AllListeners returned null");
                        return;
                    }

                    // Create a dynamic type that implements IObserver<DiagnosticListener>
                    var observerType = CreateDiagnosticListenerObserverType(diagnosticListenerType);
                    if (observerType == null)
                    {
                        Log.Warning("Failed to create dynamic observer type");
                        return;
                    }

                    // Create an instance of the dynamic observer, passing this manager instance
                    var observerInstance = Activator.CreateInstance(observerType, this);
                    if (observerInstance == null)
                    {
                        Log.Warning("Failed to create observer instance");
                        return;
                    }

                    // Use reflection to call Subscribe with the observer
                    var subscribeMethod = allListenersProperty.PropertyType.GetMethod("Subscribe");
                    if (subscribeMethod == null)
                    {
                        Log.Warning("Unable to find Subscribe method on AllListeners");
                        return;
                    }

                    _allListenersSubscription = (IDisposable?)subscribeMethod.Invoke(allListenersObservable, new object[] { observerInstance });
                    Log.Debug("Successfully subscribed to DiagnosticListener.AllListeners");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error starting DiagnosticListener.AllListeners subscription");
                }
            }
        }

        void IObserver<IDiagnosticListener>.OnCompleted()
        {
        }

        void IObserver<IDiagnosticListener>.OnError(Exception error)
        {
        }

        void IObserver<IDiagnosticListener>.OnNext(IDiagnosticListener listener)
        {
            foreach (var subscriber in _diagnosticObservers)
            {
                if (!subscriber.IsSubscriberEnabled())
                {
                    continue;
                }

                IDisposable subscription = subscriber.SubscribeIfMatch(listener.DuckAs<IDiagnosticListener>());

                if (subscription != null)
                {
                    if (Log.IsEnabled(LogEventLevel.Debug))
                    {
                        Log.Debug(
                            "Subscriber '{SubscriberType}' returned subscription for '{ListenerName}'",
                            subscriber.GetType().Name,
                            listener.Name);
                    }

                    _subscriptions.Add(subscription);
                }
            }
        }

        public void Stop()
        {
            if (_allListenersSubscription != null)
            {
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Debug("Stopping DiagnosticListener.AllListeners subscription");
                }

                _allListenersSubscription.Dispose();
                _allListenersSubscription = null;

                foreach (var subscription in _subscriptions)
                {
                    subscription.Dispose();
                }

                _subscriptions.Clear();
            }
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Creates a dynamic type that implements IObserver&lt;DiagnosticListener&gt;
        /// All the code here is developed by Cursor.
        /// to receive notifications about new DiagnosticListeners.
        /// </summary>
        private static Type? CreateDiagnosticListenerObserverType(Type diagnosticListenerType)
        {
            try
            {
                // Get the IObserver<DiagnosticListener> type
                var observerType = typeof(IObserver<>).MakeGenericType(diagnosticListenerType);

                var assemblyName = new AssemblyName("Datadog.DiagnosticManager.Dynamic");
                assemblyName.Version = typeof(DiagnosticManager).Assembly.GetName().Version;
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

                // Ensure type visibility
                DuckType.EnsureTypeVisibility(moduleBuilder, typeof(DiagnosticManager));

                var typeBuilder = moduleBuilder.DefineType(
                    "DiagnosticListenerObserver",
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed,
                    typeof(object),
                    new[] { observerType });

                // Add a field to hold the DiagnosticManager instance
                var managerField = typeBuilder.DefineField("_manager", typeof(DiagnosticManager), FieldAttributes.Private | FieldAttributes.InitOnly);

                // Define constructor that takes DiagnosticManager
                var constructor = typeBuilder.DefineConstructor(
                    MethodAttributes.Public,
                    CallingConventions.Standard,
                    new[] { typeof(DiagnosticManager) });

                var ctorIl = constructor.GetILGenerator();
                ctorIl.Emit(OpCodes.Ldarg_0);
                var baseConstructor = typeof(object).GetConstructor(Type.EmptyTypes);
                if (baseConstructor is null)
                {
                    throw new NullReferenceException("Could not get Object constructor.");
                }

                ctorIl.Emit(OpCodes.Call, baseConstructor);
                ctorIl.Emit(OpCodes.Ldarg_0);
                ctorIl.Emit(OpCodes.Ldarg_1);
                ctorIl.Emit(OpCodes.Stfld, managerField);
                ctorIl.Emit(OpCodes.Ret);

                var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

                // OnCompleted
                var onCompletedMethod = typeBuilder.DefineMethod("OnCompleted", methodAttributes, typeof(void), Type.EmptyTypes);
                var onCompletedMethodIl = onCompletedMethod.GetILGenerator();
                onCompletedMethodIl.Emit(OpCodes.Ret);

                // OnError
                var onErrorMethod = typeBuilder.DefineMethod("OnError", methodAttributes, typeof(void), new[] { typeof(Exception) });
                var onErrorMethodIl = onErrorMethod.GetILGenerator();
                onErrorMethodIl.Emit(OpCodes.Ret);

                // OnNext - calls DiagnosticManager.OnDiagnosticListenerNext
                var onDiagnosticListenerNextMethodInfo = typeof(DiagnosticManager).GetMethod(
                    nameof(OnDiagnosticListenerNext),
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (onDiagnosticListenerNextMethodInfo == null)
                {
                    Log.Warning("Unable to find OnDiagnosticListenerNext method");
                    return null;
                }

                var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { diagnosticListenerType });
                var onNextMethodIl = onNextMethod.GetILGenerator();
                onNextMethodIl.Emit(OpCodes.Ldarg_0);
                onNextMethodIl.Emit(OpCodes.Ldfld, managerField);
                onNextMethodIl.Emit(OpCodes.Ldarg_1);
                onNextMethodIl.EmitCall(OpCodes.Callvirt, onDiagnosticListenerNextMethodInfo, null);
                onNextMethodIl.Emit(OpCodes.Ret);

                var createdType = typeBuilder.CreateTypeInfo()?.AsType();
                // Store the manager in a way that the created instance can access it
                // We'll pass it to the constructor instead
                return createdType;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating dynamic DiagnosticListener observer type");
                return null;
            }
        }

        /// <summary>
        /// Called when a new DiagnosticListener is created.
        /// This method is called by the dynamically created observer type.
        /// </summary>
        private void OnDiagnosticListenerNext(object diagnosticListener)
        {
            try
            {
                // Duck type the actual DiagnosticListener to our interface
                var listener = diagnosticListener.DuckAs<IDiagnosticListener>();
                if (listener?.Instance != null)
                {
                    ((IObserver<IDiagnosticListener>)this).OnNext(listener);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error handling DiagnosticListener notification");
            }
        }
    }
}
#endif

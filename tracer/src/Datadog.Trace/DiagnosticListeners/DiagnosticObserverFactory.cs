// <copyright file="DiagnosticObserverFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.DiagnosticListeners
{
    /// <summary>
    /// Factory for creating dynamic observer types that can subscribe to DiagnosticListener.AllListeners.
    /// Uses Reflection.Emit to generate types at runtime that implement IObserver&lt;DiagnosticListener&gt;
    /// without directly referencing the DiagnosticSource assembly.
    /// Based on the implementation in ActivityListener.CreateDiagnosticObserverType.
    /// </summary>
    internal static class DiagnosticObserverFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticObserverFactory));

        /// <summary>
        /// Creates a module builder with the specified assembly name and visibility configuration.
        /// </summary>
        private static ModuleBuilder CreateModuleBuilder(string assemblyName, Type visibilityType)
        {
            var asmName = new AssemblyName(assemblyName);
            asmName.Version = visibilityType.Assembly.GetName().Version;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            DuckType.EnsureTypeVisibility(moduleBuilder, visibilityType);

            return moduleBuilder;
        }

        /// <summary>
        /// Creates a TypeBuilder for an observer type that implements IObserver&lt;DiagnosticListener&gt;.
        /// </summary>
        private static TypeBuilder CreateObserverTypeBuilder(ModuleBuilder moduleBuilder, Type observerDiagnosticListenerType)
        {
            return moduleBuilder.DefineType(
                "DiagnosticObserver",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed,
                typeof(object),
                new[] { observerDiagnosticListenerType });
        }

        /// <summary>
        /// Emits the OnCompleted method for the observer (empty implementation).
        /// </summary>
        private static void EmitOnCompletedMethod(TypeBuilder typeBuilder, MethodAttributes methodAttributes)
        {
            var onCompletedMethod = typeBuilder.DefineMethod("OnCompleted", methodAttributes, typeof(void), Type.EmptyTypes);
            var onCompletedMethodIl = onCompletedMethod.GetILGenerator();
            onCompletedMethodIl.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Emits the OnError method for the observer (empty implementation).
        /// </summary>
        private static void EmitOnErrorMethod(TypeBuilder typeBuilder, MethodAttributes methodAttributes)
        {
            var onErrorMethod = typeBuilder.DefineMethod("OnError", methodAttributes, typeof(void), new[] { typeof(Exception) });
            var onErrorMethodIl = onErrorMethod.GetILGenerator();
            onErrorMethodIl.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates a constructor that accepts a manager instance and stores it in a field.
        /// Returns the field that holds the manager reference.
        /// </summary>
        private static FieldBuilder EmitConstructorWithManagerField(TypeBuilder typeBuilder, Type managerType)
        {
            // Add a field to hold the manager instance
            var managerField = typeBuilder.DefineField("_manager", managerType, FieldAttributes.Private | FieldAttributes.InitOnly);

            // Define constructor that takes manager as parameter
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { managerType });

            var ctorIl = constructor.GetILGenerator();

            // Call base object constructor
            ctorIl.Emit(OpCodes.Ldarg_0);
            var baseConstructor = typeof(object).GetConstructor(Type.EmptyTypes);
            if (baseConstructor is null)
            {
                throw new NullReferenceException("Could not get Object constructor.");
            }

            ctorIl.Emit(OpCodes.Call, baseConstructor);

            // Store the manager field: this._manager = manager;
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_1);
            ctorIl.Emit(OpCodes.Stfld, managerField);
            ctorIl.Emit(OpCodes.Ret);

            return managerField;
        }

        /// <summary>
        /// Creates and subscribes a dynamic observer with a static callback to DiagnosticListener.AllListeners.
        /// </summary>
        public static void SubscribeWithStaticCallback(
            Type diagnosticListenerType,
            Type callbackType,
            string callbackMethodName,
            string assemblyName,
            Type visibilityType)
        {
            Log.Information("DiagnosticListener listener: {DiagnosticListenerType}", diagnosticListenerType.AssemblyQualifiedName ?? "(null)");

            var observerDiagnosticListenerType = typeof(IObserver<>).MakeGenericType(diagnosticListenerType);

            // Initialize and subscribe to DiagnosticListener.AllListeners.Subscribe
            var diagnosticObserverType = CreateObserverTypeWithStaticCallback(
                diagnosticListenerType,
                observerDiagnosticListenerType,
                callbackType,
                callbackMethodName,
                assemblyName,
                visibilityType);

            if (diagnosticObserverType is null)
            {
                throw new NullReferenceException("DiagnosticObserverFactory.CreateObserverTypeWithStaticCallback returned null.");
            }

            var diagnosticListenerInstance = Activator.CreateInstance(diagnosticObserverType);
            var allListenersPropertyInfo = diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
            if (allListenersPropertyInfo is null)
            {
                throw new NullReferenceException("DiagnosticListener.AllListeners method cannot be found.");
            }

            var subscribeMethodInfo = allListenersPropertyInfo.PropertyType.GetMethod("Subscribe", new[] { observerDiagnosticListenerType });
            subscribeMethodInfo?.Invoke(allListenersPropertyInfo.GetValue(null), new[] { diagnosticListenerInstance });
        }

        /// <summary>
        /// Creates and subscribes a dynamic observer with an instance callback to DiagnosticListener.AllListeners.
        /// Based on DiagnosticManager.Start() pattern but returns the subscription.
        /// </summary>
        public static IDisposable? SubscribeWithInstanceCallback(
            Type diagnosticListenerType,
            object managerInstance,
            Type managerType,
            string callbackMethodName,
            string assemblyName,
            Type visibilityType)
        {
            // Get the IObserver<DiagnosticListener> type
            var observerDiagnosticListenerType = typeof(IObserver<>).MakeGenericType(diagnosticListenerType);

            // Get the AllListeners static property
            var allListenersProperty = diagnosticListenerType.GetProperty("AllListeners", BindingFlags.Public | BindingFlags.Static);
            if (allListenersProperty == null)
            {
                Log.Warning("Unable to find DiagnosticListener.AllListeners property");
                return null;
            }

            // Get the value (IObservable<DiagnosticListener>)
            var allListenersObservable = allListenersProperty.GetValue(null);
            if (allListenersObservable == null)
            {
                Log.Warning("DiagnosticListener.AllListeners returned null");
                return null;
            }

            // Create a dynamic type that implements IObserver<DiagnosticListener> using the shared factory
            var observerType = CreateObserverTypeWithInstanceCallback(
                diagnosticListenerType,
                observerDiagnosticListenerType,
                managerType,
                callbackMethodName,
                assemblyName,
                visibilityType);

            if (observerType == null)
            {
                Log.Warning("Failed to create dynamic observer type");
                return null;
            }

            // Create an instance of the dynamic observer, passing this manager instance
            var observerInstance = Activator.CreateInstance(observerType, managerInstance);
            if (observerInstance == null)
            {
                Log.Warning("Failed to create observer instance");
                return null;
            }

            // Use reflection to call Subscribe with the observer
            var subscribeMethod = allListenersProperty.PropertyType.GetMethod("Subscribe");
            if (subscribeMethod == null)
            {
                Log.Warning("Unable to find Subscribe method on AllListeners");
                return null;
            }

            return (IDisposable?)subscribeMethod.Invoke(allListenersObservable, new object[] { observerInstance });
        }

        /// <summary>
        /// Creates a dynamic type that implements IObserver&lt;DiagnosticListener&gt;
        /// with a static method callback (no instance state).
        /// Uses the exact same IL emission pattern as ActivityListener.CreateDiagnosticObserverType.
        /// </summary>
        /// <param name="diagnosticListenerType">The Type of DiagnosticListener obtained via reflection</param>
        /// <param name="observerDiagnosticListenerType">The IObserver&lt;DiagnosticListener&gt; type</param>
        /// <param name="callbackType">The type containing the static callback method</param>
        /// <param name="callbackMethodName">The name of the static method to call when OnNext is invoked</param>
        /// <param name="assemblyName">The name for the dynamic assembly</param>
        /// <param name="visibilityType">A type from the calling assembly to ensure visibility</param>
        /// <returns>A dynamically created Type that implements IObserver&lt;DiagnosticListener&gt;, or null if creation fails</returns>
        public static Type? CreateObserverTypeWithStaticCallback(
            Type diagnosticListenerType,
            Type observerDiagnosticListenerType,
            Type callbackType,
            string callbackMethodName,
            string assemblyName,
            Type visibilityType)
        {
            var moduleBuilder = CreateModuleBuilder(assemblyName, visibilityType);
            var typeBuilder = CreateObserverTypeBuilder(moduleBuilder, observerDiagnosticListenerType);

            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

            EmitOnCompletedMethod(typeBuilder, methodAttributes);
            EmitOnErrorMethod(typeBuilder, methodAttributes);

            // OnNext - call static method
            var onSetListenerMethodInfo = callbackType.GetMethod(callbackMethodName, BindingFlags.Static | BindingFlags.Public)!;
            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { diagnosticListenerType });
            var onNextMethodIl = onNextMethod.GetILGenerator();
            onNextMethodIl.Emit(OpCodes.Ldarg_1);
            onNextMethodIl.EmitCall(OpCodes.Call, onSetListenerMethodInfo, null);
            onNextMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()?.AsType();
        }

        /// <summary>
        /// Creates a dynamic type that implements IObserver&lt;DiagnosticListener&gt;
        /// with an instance method callback (holds a reference to a manager object).
        /// Uses the same IL emission pattern as the static callback version, but adds a constructor
        /// and instance field to hold the manager reference.
        /// </summary>
        /// <param name="diagnosticListenerType">The Type of DiagnosticListener obtained via reflection</param>
        /// <param name="observerDiagnosticListenerType">The IObserver&lt;DiagnosticListener&gt; type</param>
        /// <param name="managerType">The type of the manager instance to store</param>
        /// <param name="callbackMethodName">The name of the instance method to call when OnNext is invoked</param>
        /// <param name="assemblyName">The name for the dynamic assembly</param>
        /// <param name="visibilityType">A type from the calling assembly to ensure visibility</param>
        /// <returns>A dynamically created Type that implements IObserver&lt;DiagnosticListener&gt;, or null if creation fails</returns>
        public static Type? CreateObserverTypeWithInstanceCallback(
            Type diagnosticListenerType,
            Type observerDiagnosticListenerType,
            Type managerType,
            string callbackMethodName,
            string assemblyName,
            Type visibilityType)
        {
            var moduleBuilder = CreateModuleBuilder(assemblyName, visibilityType);
            var typeBuilder = CreateObserverTypeBuilder(moduleBuilder, observerDiagnosticListenerType);

            // Create constructor and get the manager field
            var managerField = EmitConstructorWithManagerField(typeBuilder, managerType);

            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

            EmitOnCompletedMethod(typeBuilder, methodAttributes);
            EmitOnErrorMethod(typeBuilder, methodAttributes);

            // OnNext - call instance method on manager
            var onSetListenerMethodInfo = managerType.GetMethod(callbackMethodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (onSetListenerMethodInfo == null)
            {
                Log.Warning("Unable to find {CallbackMethodName} method on {ManagerType}", callbackMethodName, managerType.Name);
                return null;
            }

            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { diagnosticListenerType });
            var onNextMethodIl = onNextMethod.GetILGenerator();
            onNextMethodIl.Emit(OpCodes.Ldarg_0);                    // Load 'this'
            onNextMethodIl.Emit(OpCodes.Ldfld, managerField);        // Load this._manager
            onNextMethodIl.Emit(OpCodes.Ldarg_1);                    // Load the listener parameter
            onNextMethodIl.EmitCall(OpCodes.Callvirt, onSetListenerMethodInfo, null); // Call instance method
            onNextMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()?.AsType();
        }
    }
}

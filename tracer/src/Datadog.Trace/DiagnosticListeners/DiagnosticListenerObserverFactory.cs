// <copyright file="DiagnosticListenerObserverFactory.cs" company="Datadog">
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
    /// </summary>
    internal static class DiagnosticListenerObserverFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DiagnosticListenerObserverFactory));

        /// <summary>
        /// Creates a dynamic type that implements IObserver&lt;DiagnosticListener&gt;
        /// to receive notifications about new DiagnosticListeners.
        /// </summary>
        /// <param name="diagnosticListenerType">The Type of DiagnosticListener obtained via reflection</param>
        /// <returns>A dynamically created Type that implements IObserver&lt;DiagnosticListener&gt;, or null if creation fails</returns>
        public static Type? CreateObserverType(Type diagnosticListenerType)
        {
            try
            {
                // Get the IObserver<DiagnosticListener> type
                var observerType = typeof(IObserver<>).MakeGenericType(diagnosticListenerType);

                // Create a dynamic assembly to hold our observer type
                var assemblyName = new AssemblyName("Datadog.DiagnosticManager.Dynamic");
                assemblyName.Version = typeof(DiagnosticManager).Assembly.GetName().Version;
                var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
                var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

                // Ensure type visibility for DuckType infrastructure
                DuckType.EnsureTypeVisibility(moduleBuilder, typeof(DiagnosticManager));

                // Define the observer type that will implement IObserver<DiagnosticListener>
                var typeBuilder = moduleBuilder.DefineType(
                    "DiagnosticListenerObserver",
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed,
                    typeof(object),
                    new[] { observerType });

                // Add a field to hold the DiagnosticManager instance that will handle callbacks
                var managerField = typeBuilder.DefineField("_manager", typeof(DiagnosticManager), FieldAttributes.Private | FieldAttributes.InitOnly);

                // Define constructor that takes DiagnosticManager as parameter
                CreateConstructor(typeBuilder, managerField);

                // Define the three IObserver methods
                CreateOnCompletedMethod(typeBuilder);
                CreateOnErrorMethod(typeBuilder);
                var success = CreateOnNextMethod(typeBuilder, managerField, diagnosticListenerType);

                if (!success)
                {
                    return null;
                }

                // Create and return the final type
                var createdType = typeBuilder.CreateTypeInfo()?.AsType();
                return createdType;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating dynamic DiagnosticListener observer type");
                return null;
            }
        }

        /// <summary>
        /// Creates the constructor for the observer type.
        /// Constructor signature: .ctor(DiagnosticManager manager)
        /// </summary>
        private static void CreateConstructor(TypeBuilder typeBuilder, FieldInfo managerField)
        {
            var constructor = typeBuilder.DefineConstructor(
                MethodAttributes.Public,
                CallingConventions.Standard,
                new[] { typeof(DiagnosticManager) });

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
        }

        /// <summary>
        /// Creates the OnCompleted method (no-op implementation).
        /// </summary>
        private static void CreateOnCompletedMethod(TypeBuilder typeBuilder)
        {
            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;
            var onCompletedMethod = typeBuilder.DefineMethod("OnCompleted", methodAttributes, typeof(void), Type.EmptyTypes);
            var il = onCompletedMethod.GetILGenerator();
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates the OnError method (no-op implementation).
        /// </summary>
        private static void CreateOnErrorMethod(TypeBuilder typeBuilder)
        {
            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;
            var onErrorMethod = typeBuilder.DefineMethod("OnError", methodAttributes, typeof(void), new[] { typeof(Exception) });
            var il = onErrorMethod.GetILGenerator();
            il.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// Creates the OnNext method that forwards to DiagnosticManager.OnDiagnosticListenerNext.
        /// Method signature: void OnNext(DiagnosticListener listener)
        /// Implementation: this._manager.OnDiagnosticListenerNext(listener);
        /// </summary>
        private static bool CreateOnNextMethod(TypeBuilder typeBuilder, FieldInfo managerField, Type diagnosticListenerType)
        {
            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

            // Find the callback method on DiagnosticManager
            var onDiagnosticListenerNextMethodInfo = typeof(DiagnosticManager).GetMethod(
                nameof(DiagnosticManager.OnDiagnosticListenerNext),
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (onDiagnosticListenerNextMethodInfo == null)
            {
                Log.Warning("Unable to find OnDiagnosticListenerNext method on DiagnosticManager");
                return false;
            }

            // Define OnNext method
            var onNextMethod = typeBuilder.DefineMethod("OnNext", methodAttributes, typeof(void), new[] { diagnosticListenerType });
            var il = onNextMethod.GetILGenerator();

            // Generate: this._manager.OnDiagnosticListenerNext(listener);
            il.Emit(OpCodes.Ldarg_0);                                      // Load 'this'
            il.Emit(OpCodes.Ldfld, managerField);                          // Load this._manager
            il.Emit(OpCodes.Ldarg_1);                                      // Load the listener parameter
            il.EmitCall(OpCodes.Callvirt, onDiagnosticListenerNextMethodInfo, null); // Call the method
            il.Emit(OpCodes.Ret);                                          // Return

            return true;
        }
    }
}

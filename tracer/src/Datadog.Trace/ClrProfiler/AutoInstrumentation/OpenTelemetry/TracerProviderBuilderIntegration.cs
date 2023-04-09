// <copyright file="TracerProviderBuilderIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.OpenTelemetry
{
    /// <summary>
    /// OpenTelemetry.Trace.TracerProviderBuilderExtensions.Build calltarget instrumentation,
    /// aka Sdk.CreateTracerProviderBuilder().Build()
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "OpenTelemetry",
        TypeName = "OpenTelemetry.Trace.TracerProviderBuilderExtensions",
        MethodName = "Build",
        ReturnTypeName = "OpenTelemetry.Trace.TracerProvider",
        ParameterTypeNames = new[] { "OpenTelemetry.Trace.TracerProviderBuilder" },
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.0.0",
        IntegrationName = IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class TracerProviderBuilderIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.OpenTelemetry);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.OpenTelemetry;
        private static Func<object, object, object> _cachedAddProcessorDelegate;
        private static Type _cachedProcessorType;

        static TracerProviderBuilderIntegration()
        {
            _cachedAddProcessorDelegate = CreateAddProcessorDelegate();
            _cachedProcessorType = CreateProcessorType();
        }

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TTracerProviderBuilder">Type of the span kind</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="tracerProviderBuilder">The TracerProviderBuilder instance.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TTracerProviderBuilder>(TTarget instance, TTracerProviderBuilder tracerProviderBuilder)
        {
            if (Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                if (_cachedAddProcessorDelegate is not null
                    && _cachedProcessorType is not null)
                {
                    _cachedAddProcessorDelegate(tracerProviderBuilder, Activator.CreateInstance(_cachedProcessorType));
                }
            }

            return CallTargetState.GetDefault();
        }

        private static Func<object, object, object> CreateAddProcessorDelegate()
        {
            Type builderExtensionsType = Type.GetType("OpenTelemetry.Trace.TracerProviderBuilderExtensions, OpenTelemetry", throwOnError: false);
            Type builderType = Type.GetType("OpenTelemetry.Trace.TracerProviderBuilder, OpenTelemetry.Api", throwOnError: false);
            Type baseProcessorType = Type.GetType("OpenTelemetry.BaseProcessor`1, OpenTelemetry", throwOnError: false);
            Type activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource", throwOnError: false);

            if (builderExtensionsType is null || builderType is null || baseProcessorType is null || activityType is null)
            {
                return null;
            }

            // Get the extension method from the API
            Type baseProcessorOfActivityType = baseProcessorType.MakeGenericType(activityType);
            var targetAddProcessorMethod = builderExtensionsType?.GetMethod("AddProcessor", new Type[] { builderType, baseProcessorOfActivityType });
            if (targetAddProcessorMethod is null)
            {
                return null;
            }

            DynamicMethod dynMethod = new DynamicMethod(
                     $"{nameof(TracerProviderBuilderIntegration)}.AddProcessor",
                     typeof(object),
                     new Type[] { typeof(object), typeof(object) },
                     typeof(TracerProviderBuilderIntegration).Module,
                     true);
            ILGenerator ilWriter = dynMethod.GetILGenerator();
            ilWriter.Emit(OpCodes.Ldarg_0);
            ilWriter.Emit(OpCodes.Castclass, builderType); // Cast to OpenTelemetry.Trace.TracerProviderBuilder

            ilWriter.Emit(OpCodes.Ldarg_1);
            ilWriter.Emit(OpCodes.Castclass, baseProcessorOfActivityType); // Cast to OpenTelemetry.BaseProcessor<System.Diagnostics.Activity>

            ilWriter.EmitCall(OpCodes.Call, targetAddProcessorMethod, null);
            ilWriter.Emit(OpCodes.Ret);

            return (Func<object, object, object>)dynMethod.CreateDelegate(typeof(Func<object, object, object>));
        }

        private static Type CreateProcessorType()
        {
            Type activityType = Type.GetType("System.Diagnostics.Activity, System.Diagnostics.DiagnosticSource", throwOnError: false);
            Type baseProcessorType = Type.GetType("OpenTelemetry.BaseProcessor`1, OpenTelemetry", throwOnError: false);

            if (activityType is null || baseProcessorType is null)
            {
                return null;
            }

            Type baseProcessorOfActivityType = baseProcessorType.MakeGenericType(activityType);

            var assemblyName = new AssemblyName("Datadog.OpenTelemetry.Dynamic");
            assemblyName.Version = typeof(TracerProviderBuilderIntegration).Assembly.GetName().Version;
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            DuckType.EnsureTypeVisibility(moduleBuilder, typeof(TracerProviderBuilderIntegration));

            var typeBuilder = moduleBuilder.DefineType(
                $"{typeof(TracerProviderBuilderIntegration).Namespace}.ResourceAttributeProcessor",
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass | TypeAttributes.BeforeFieldInit | TypeAttributes.AutoLayout | TypeAttributes.Sealed,
                parent: baseProcessorOfActivityType,
                interfaces: null);

            var methodAttributes = MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.HideBySig;

            // OnStart
            var onStartHelperMethodInfo = typeof(ResourceAttributeProcessorHelper).GetMethod(nameof(ResourceAttributeProcessorHelper.OnStart), BindingFlags.Static | BindingFlags.Public)!;
            var onStartMethod = typeBuilder.DefineMethod("OnStart", methodAttributes, typeof(void), new[] { activityType });
            var onStartMethodIl = onStartMethod.GetILGenerator();
            onStartMethodIl.Emit(OpCodes.Ldarg_0);
            onStartMethodIl.Emit(OpCodes.Ldarg_1);
            onStartMethodIl.EmitCall(OpCodes.Call, onStartHelperMethodInfo, null);
            onStartMethodIl.Emit(OpCodes.Ret);

            return typeBuilder.CreateTypeInfo()?.AsType();
        }
    }
}

// <copyright file="HookRegistryCtorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.Gauge
{
    /// <summary>
    /// Gauge.Dotnet.Models.HookRegistry.ctor calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Gauge.Dotnet",
        TypeName = "Gauge.Dotnet.Models.HookRegistry",
        MethodName = ".ctor",
        ParameterTypeNames = new[] { "Gauge.Dotnet.IAssemblyLoader" },
        ReturnTypeName = ClrNames.Void,
        MinimumVersion = "0.4.0",
        MaximumVersion = "0.5.0",
        IntegrationName = IntegrationName)]
    public static class HookRegistryCtorIntegration
    {
        internal const string IntegrationName = "Gauge";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TAssemblyLoader">Type of the IAssemblyLoader</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="assemblyLoader">AssemblyLoader instance</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TAssemblyLoader>(TTarget instance, TAssemblyLoader assemblyLoader)
        {
            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
            where TTarget : IHookRegistry
        {
            instance.AddHookOfType(LibType.BeforeScenario, new[] { typeof(HooksHandlers).GetMethod(nameof(HooksHandlers.BeforeScenario)) });
            instance.AddHookOfType(LibType.AfterScenario, new[] { typeof(HooksHandlers).GetMethod(nameof(HooksHandlers.AfterScenario)) });

            instance.AddHookOfType(LibType.BeforeStep, new[] { typeof(HooksHandlers).GetMethod(nameof(HooksHandlers.BeforeStep)) });
            instance.AddHookOfType(LibType.AfterStep, new[] { typeof(HooksHandlers).GetMethod(nameof(HooksHandlers.AfterStep)) });

            return CallTargetReturn.GetDefault();
        }
    }
}

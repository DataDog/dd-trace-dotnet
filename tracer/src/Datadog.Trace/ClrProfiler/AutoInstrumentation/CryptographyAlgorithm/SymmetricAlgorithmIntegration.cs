// <copyright file="SymmetricAlgorithmIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm
{
    /// <summary>
    /// System.Security.Cryptography.HashAlgorithm instrumentation
    /// </summary>
    /// /// RC2CryptoServiceProvider
    [InstrumentMethod(
       AssemblyNames = new[] { "System.Security.Cryptography.Primitives" },
       TypeNames = new[] { "System.Security.Cryptography.SymmetricAlgorithm" },
       ParameterTypeNames = new string[] { },
       MethodName = ".ctor",
       ReturnTypeName = ClrNames.Void,
       MinimumVersion = "1.0.0",
       MaximumVersion = "7.*.*",
       InstrumentationCategory = InstrumentationCategory.Iast,
       IntegrationName = nameof(Configuration.IntegrationId.SymmetricAlgorithm))]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class SymmetricAlgorithmIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
        {
            if (instance is System.Security.Cryptography.SymmetricAlgorithm algorithm)
            {
                return new CallTargetState(scope: SymmetricAlgorithmIntegrationCommon.CreateScope(algorithm));
            }

            return CallTargetState.GetDefault();
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TReturn">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="returnValue">the return value processce</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>CallTargetReturn</returns>
        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception exception, CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}

// <copyright file="InsecureHashingIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Security.Cryptography;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm
{
    /// <summary>
    /// System.Security.Cryptography.HashAlgorithm instrumentation
    /// </summary>
    [InstrumentMethod(
       AssemblyNames = new[] { "mscorlib", "System.Security.Cryptography.Primitives" },
       TypeNames = new[] { "System.Security.Cryptography.HashAlgorithm" },
       ParameterTypeNames = new[] { ClrNames.ByteArray, ClrNames.Int32, ClrNames.Int32 },
       MethodName = "ComputeHash",
       ReturnTypeName = ClrNames.ByteArray,
       MinimumVersion = "1.0.0",
       MaximumVersion = "7.*.*",
       IntegrationName = nameof(Configuration.IntegrationId.HashAlgorithm))]

    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class InsecureHashingIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="array">The input to compute the hash code for.</param>
        /// <param name="offset">The offset into the byte array from which to begin using data.</param>
        /// <param name="count">The number of bytes in the array to use as data.</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, byte[] array, int offset, int count)
        {
            if (instance is System.Security.Cryptography.HashAlgorithm algorithm)
            {
                return new CallTargetState(scope: InsecureHashingCommon.CreateScope(algorithm));
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

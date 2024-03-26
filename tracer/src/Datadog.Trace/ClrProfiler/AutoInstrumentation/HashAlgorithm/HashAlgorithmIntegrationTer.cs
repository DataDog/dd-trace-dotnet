// <copyright file="HashAlgorithmIntegrationTer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;

/// <summary>
/// System.Security.Cryptography.HashAlgorithm instrumentation
/// </summary>
[InstrumentMethod(
   AssemblyNames = new[] { "System.Security.Cryptography.Primitives" },
   TypeNames = new[] { "System.Security.Cryptography.HashAlgorithm" },
   ParameterTypeNames = new[] { ClrNames.Stream, ClrNames.CancellationToken },
   MethodName = "ComputeHashAsync",
   ReturnTypeName = ClrNames.Task,
   MinimumVersion = "1.0.0",
   MaximumVersion = "6.*.*",
   InstrumentationCategory = InstrumentationCategory.Iast,
   IntegrationName = nameof(Configuration.IntegrationId.HashAlgorithm))]
[InstrumentMethod(
   AssemblyNames = new[] { "System.Security.Cryptography" },
   TypeNames = new[] { "System.Security.Cryptography.HashAlgorithm" },
   ParameterTypeNames = new[] { ClrNames.Stream, ClrNames.CancellationToken },
   MethodName = "ComputeHashAsync",
   ReturnTypeName = ClrNames.Task,
   MinimumVersion = "7.0.0",
   MaximumVersion = "8.*.*",
   InstrumentationCategory = InstrumentationCategory.Iast,
   IntegrationName = nameof(Configuration.IntegrationId.HashAlgorithm))]

[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class HashAlgorithmIntegrationTer
{
    /// <summary>
    /// OnMethodBegin callback
    /// </summary>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="data">The input to compute the hash code for.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <returns>Calltarget state value</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance, Stream data, CancellationToken token)
    {
        return new CallTargetState(scope: HashAlgorithmIntegrationCommon.CreateScope(instance));
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
#endif

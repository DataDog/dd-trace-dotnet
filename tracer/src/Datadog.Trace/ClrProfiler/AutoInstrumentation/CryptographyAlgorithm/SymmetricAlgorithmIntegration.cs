// <copyright file="SymmetricAlgorithmIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;
using Datadog.Trace.Iast.Telemetry;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CryptographyAlgorithm;

/// <summary>
/// System.Security.Cryptography.HashAlgorithm instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "System.Security.Cryptography.Primitives",
    TypeName = "System.Security.Cryptography.SymmetricAlgorithm",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "1.0.0",
    MaximumVersion = "6.*.*",
    InstrumentationCategory = InstrumentationCategory.Iast,
    IntegrationName = nameof(Configuration.IntegrationId.SymmetricAlgorithm))]
[InstrumentMethod(
    AssemblyName = "System.Security.Cryptography",
    TypeName = "System.Security.Cryptography.SymmetricAlgorithm",
    MethodName = ".ctor",
    ReturnTypeName = ClrNames.Void,
    MinimumVersion = "7.0.0",
    MaximumVersion = "7.*.*",
    InstrumentationCategory = InstrumentationCategory.Iast,
    IntegrationName = nameof(Configuration.IntegrationId.SymmetricAlgorithm))]
[IastInstrumentation(AspectType.Sink, VulnerabilityType.WeakCipher, times: 2)]
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
        return new CallTargetState(scope: SymmetricAlgorithmIntegrationCommon.CreateScope(instance));
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>CallTargetReturn</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception exception, CallTargetState state)
    {
        state.Scope.DisposeWithException(exception);
        return CallTargetReturn.GetDefault();
    }
}

#endif

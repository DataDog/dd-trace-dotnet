// <copyright file="HashAlgorithmIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.HashAlgorithm;

/// <summary>
/// System.Security.Cryptography.HashAlgorithm instrumentation
/// </summary>
[InstrumentMethod(
   AssemblyNames = new[] { "System.Security.Cryptography.Primitives" },
   TypeNames = new[] { "System.Security.Cryptography.HashAlgorithm" },
   ParameterTypeNames = new[] { ClrNames.ByteArray, ClrNames.Int32, ClrNames.Int32 },
   MethodName = "ComputeHash",
   ReturnTypeName = ClrNames.ByteArray,
   MinimumVersion = "1.0.0",
   MaximumVersion = "6.*.*",
   InstrumentationCategory = InstrumentationCategory.Iast,
   IntegrationName = nameof(Configuration.IntegrationId.HashAlgorithm))]
[InstrumentMethod(
    AssemblyNames = new[] { "System.Security.Cryptography" },
    TypeNames = new[] { "System.Security.Cryptography.HashAlgorithm" },
    ParameterTypeNames = new[] { ClrNames.ByteArray, ClrNames.Int32, ClrNames.Int32 },
    MethodName = "ComputeHash",
    ReturnTypeName = ClrNames.ByteArray,
    MinimumVersion = "7.0.0",
    MaximumVersion = SupportedVersions.LatestDotNet,
    InstrumentationCategory = InstrumentationCategory.Iast,
    IntegrationName = nameof(Configuration.IntegrationId.HashAlgorithm))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class HashAlgorithmIntegration
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
        HashAlgorithmIntegrationCommon.OnHashAlgorithm(instance);
        return CallTargetState.GetDefault();
    }
}

#endif

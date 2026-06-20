// <copyright file="SampleCallTargetNativeAotDuckTypeAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.CallTargetNativeAot;

/// <summary>
/// Provides the DuckType-constrained async-result sample integration used to prove that CallTarget NativeAOT can
/// proxy the completed async result through DuckType AOT and unwrap it back into the application type.
/// </summary>
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteDuckAsync",
    ReturnTypeName = "System.Threading.Tasks.Task`1<SampleCallTargetNativeAotApp.DuckPayload>",
    ParameterTypeNames = ["System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class SampleCallTargetNativeAotDuckTypeAsyncIntegration
{
    /// <summary>
    /// Defines the marker emitted by the duck-typed async callback.
    /// </summary>
    internal const string DuckAsyncMarker = "CALLTARGET_AOT_DUCK_ASYNC";

    /// <summary>
    /// Defines the duck-typed instance shape used by the async sample binding.
    /// </summary>
    internal interface IDuckInstrumentedTarget
    {
        /// <summary>
        /// Gets the marker value exposed by the target instance.
        /// </summary>
        int DuckValue { get; }
    }

    /// <summary>
    /// Defines the duck-typed payload shape used for the completed async result.
    /// </summary>
    internal interface IDuckPayload
    {
        /// <summary>
        /// Gets the payload marker value.
        /// </summary>
        int Value { get; }
    }

    /// <summary>
    /// Reads the duck-typed instance before the async target method executes.
    /// </summary>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 arg1)
        where TTarget : IDuckInstrumentedTarget, IDuckType
    {
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Preserves the original task return value so the async continuation path, not the end callback, is responsible
    /// for proxying the completed result through DuckType AOT.
    /// </summary>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        where TTarget : IDuckInstrumentedTarget, IDuckType
    {
        return new CallTargetReturn<TReturn>(returnValue);
    }

    /// <summary>
    /// Reads the duck-typed completed async result and preserves it so the generated async adapter must unwrap the
    /// proxy before the application receives the final value.
    /// </summary>
    [PreserveContext]
    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, CallTargetState state)
        where TTarget : IDuckInstrumentedTarget, IDuckType
        where TReturn : IDuckPayload, IDuckType
    {
        await Task.Yield();
        Console.WriteLine($"{DuckAsyncMarker}:{instance.DuckValue}:{returnValue.Value}");
        return returnValue;
    }
}

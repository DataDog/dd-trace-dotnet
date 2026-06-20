// <copyright file="SampleCallTargetNativeAotDuckTypeIntegration.cs" company="Datadog">
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
/// Provides DuckType-constrained sample integrations that exercise the CallTarget NativeAOT companion DuckType
/// registry for instance, argument, return-value, and async-result bindings.
/// </summary>
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteDuckBegin",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = ["SampleCallTargetNativeAotApp.DuckPayload", "System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteDuckReturn",
    ReturnTypeName = "SampleCallTargetNativeAotApp.DuckPayload",
    ParameterTypeNames = ["System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class SampleCallTargetNativeAotDuckTypeIntegration
{
    /// <summary>
    /// Defines the marker emitted by the duck-typed begin callback.
    /// </summary>
    internal const string DuckBeginMarker = "CALLTARGET_AOT_DUCK_BEGIN";

    /// <summary>
    /// Defines the marker emitted by the duck-typed void end callback.
    /// </summary>
    internal const string DuckEndMarker = "CALLTARGET_AOT_DUCK_END";

    /// <summary>
    /// Defines the marker emitted by the duck-typed return callback.
    /// </summary>
    internal const string DuckReturnMarker = "CALLTARGET_AOT_DUCK_RETURN";

    /// <summary>
    /// Defines the duck-typed shape used for the instrumented target instance.
    /// </summary>
    internal interface IDuckInstrumentedTarget
    {
        /// <summary>
        /// Gets the marker value exposed by the target instance.
        /// </summary>
        int DuckValue { get; }
    }

    /// <summary>
    /// Defines the duck-typed shape used for payload arguments and return values.
    /// </summary>
    internal interface IDuckPayload
    {
        /// <summary>
        /// Gets the payload marker value.
        /// </summary>
        int Value { get; }
    }

    /// <summary>
    /// Reads both a duck-typed instance and a duck-typed argument to prove the generated begin adapter can close the
    /// binding against generated proxies instead of runtime IL emission.
    /// </summary>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2>(TTarget instance, TArg1 arg1, TArg2 arg2)
        where TTarget : IDuckInstrumentedTarget, IDuckType
        where TArg1 : IDuckPayload, IDuckType
    {
        Console.WriteLine($"{DuckBeginMarker}:{instance.DuckValue}:{arg1.Value}:{arg2}");
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Reads a duck-typed instance for the single-argument duck-return and duck-async sample methods.
    /// </summary>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 arg1)
        where TTarget : IDuckInstrumentedTarget, IDuckType
    {
        Console.WriteLine($"{DuckBeginMarker}:{instance.DuckValue}:{arg1}");
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Emits the duck-typed void end marker to prove the generated adapter can proxy the target instance on the end
    /// path as well as at begin.
    /// </summary>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IDuckInstrumentedTarget, IDuckType
    {
        Console.WriteLine($"{DuckEndMarker}:{instance.DuckValue}");
        return CallTargetReturn.GetDefault();
    }

    /// <summary>
    /// Reads a duck-typed return value and preserves it so the generated end adapter must unwrap the proxy back into
    /// the original target return type before the app receives it.
    /// </summary>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        where TTarget : IDuckInstrumentedTarget, IDuckType
        where TReturn : IDuckPayload, IDuckType
    {
        Console.WriteLine($"{DuckReturnMarker}:{instance.DuckValue}:{returnValue.Value}");
        return new CallTargetReturn<TReturn>(returnValue);
    }
}

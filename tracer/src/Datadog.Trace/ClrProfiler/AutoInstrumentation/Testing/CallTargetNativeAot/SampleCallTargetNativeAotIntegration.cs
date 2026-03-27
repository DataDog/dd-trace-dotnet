// <copyright file="SampleCallTargetNativeAotIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.CallTargetNativeAot;

/// <summary>
/// Provides a fixed integration used to prove that the CallTarget NativeAOT pipeline can discover,
/// rewrite, bootstrap, and execute a real integration callback without runtime IL emission.
/// </summary>
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "Execute",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteWithValue",
    ReturnTypeName = "System.Int32",
    ParameterTypeNames = ["System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteSlowBegin",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = ["System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32", "System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteAsync",
    ReturnTypeName = "System.Threading.Tasks.Task",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteAsyncWithValue",
    ReturnTypeName = "System.Threading.Tasks.Task`1<System.Int32>",
    ParameterTypeNames = ["System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteValueAsync",
    ReturnTypeName = "System.Threading.Tasks.ValueTask",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteValueAsyncWithValue",
    ReturnTypeName = "System.Threading.Tasks.ValueTask`1<System.Int32>",
    ParameterTypeNames = ["System.Int32"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteDerived",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedBase",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0",
    CallTargetIntegrationKind = CallTargetKind.Derived)]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteInterface",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.IInstrumentedContract",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0",
    CallTargetIntegrationKind = CallTargetKind.Interface)]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteStaticUnsupported",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteGenericUnsupported",
    ReturnTypeName = "!!0",
    ParameterTypeNames = ["!!0"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteByRefUnsupported",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = ["System.Int32&"],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteByRefReturnUnsupported",
    ReturnTypeName = "System.Int32&",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotApp",
    TypeName = "SampleCallTargetNativeAotApp.InstrumentedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class SampleCallTargetNativeAotIntegration
{
    /// <summary>
    /// Defines the marker emitted by the begin callback when the rewritten method enters the integration.
    /// </summary>
    internal const string BeginMarker = "CALLTARGET_AOT_INTEGRATION_BEGIN:1";

    /// <summary>
    /// Defines the marker emitted by the end callback when the rewritten method exits the integration.
    /// </summary>
    internal const string EndMarker = "CALLTARGET_AOT_INTEGRATION_END:1";

    /// <summary>
    /// Defines the marker emitted by the argument-aware begin callback when the rewritten method enters the integration.
    /// </summary>
    internal const string BeginWithValueMarker = "CALLTARGET_AOT_INTEGRATION_BEGIN_WITH_VALUE:1";

    /// <summary>
    /// Defines the marker emitted by the slow begin callback when the rewritten method enters through the object-array path.
    /// </summary>
    internal const string SlowBeginMarker = "CALLTARGET_AOT_INTEGRATION_SLOW_BEGIN:1";

    /// <summary>
    /// Defines the marker emitted by the value-returning end callback when the rewritten method exits the integration.
    /// </summary>
    internal const string EndWithValueMarker = "CALLTARGET_AOT_INTEGRATION_END_WITH_VALUE:1";

    /// <summary>
    /// Defines the marker emitted by the async continuation callback for Task-returning target methods.
    /// </summary>
    internal const string AsyncEndMarker = "CALLTARGET_AOT_INTEGRATION_ASYNC_END:1";

    /// <summary>
    /// Defines the marker emitted by the async continuation callback for Task{TResult}-returning target methods.
    /// </summary>
    internal const string AsyncEndWithValueMarker = "CALLTARGET_AOT_INTEGRATION_ASYNC_END_WITH_VALUE:1";

    /// <summary>
    /// Emits the begin marker and returns the default state for the sample integration.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <returns>The default calltarget state.</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        Console.WriteLine(BeginMarker);
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Emits the argument-aware begin marker and returns the default state for the value-returning sample path.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <typeparam name="TArg1">The first target method argument type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="arg1">The first target method argument.</param>
    /// <returns>The default calltarget state.</returns>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1>(TTarget instance, TArg1 arg1)
    {
        Console.WriteLine(BeginWithValueMarker);
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Emits the slow-begin marker for methods whose argument count requires the object-array CallTarget path.
    /// </summary>
    internal static CallTargetState OnMethodBegin<TTarget, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9>(
        TTarget instance,
        TArg1 arg1,
        TArg2 arg2,
        TArg3 arg3,
        TArg4 arg4,
        TArg5 arg5,
        TArg6 arg6,
        TArg7 arg7,
        TArg8 arg8,
        TArg9 arg9)
    {
        Console.WriteLine($"{SlowBeginMarker}:{arg1}:{arg9}");
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Emits the end marker and leaves the original void return path unchanged.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="exception">The exception thrown by the target, if any.</param>
    /// <param name="state">The calltarget state captured at method begin.</param>
    /// <returns>The default void calltarget return wrapper.</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine(EndMarker);
        return CallTargetReturn.GetDefault();
    }

    /// <summary>
    /// Emits the value-returning end marker and preserves the original target method return value.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <typeparam name="TReturn">The target method return type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="returnValue">The target method return value.</param>
    /// <param name="exception">The exception thrown by the target, if any.</param>
    /// <param name="state">The calltarget state captured at method begin.</param>
    /// <returns>The original target method return value wrapped in the CallTarget return container.</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine(EndWithValueMarker);
        return new CallTargetReturn<TReturn>(returnValue);
    }

    /// <summary>
    /// Emits the async end marker for Task-returning target methods and preserves the continuation result.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="returnValue">The placeholder async return value for the continuation callback.</param>
    /// <param name="exception">The exception thrown by the target task, if any.</param>
    /// <param name="state">The calltarget state captured at method begin.</param>
    /// <returns>The original continuation result placeholder.</returns>
    internal static object? OnAsyncMethodEnd<TTarget>(TTarget instance, object? returnValue, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine(AsyncEndMarker);
        return returnValue;
    }

    /// <summary>
    /// Emits the async value marker for Task{TResult}-returning target methods and preserves the continuation result.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <typeparam name="TReturn">The completed task result type.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="returnValue">The completed task result value.</param>
    /// <param name="exception">The exception thrown by the target task, if any.</param>
    /// <param name="state">The calltarget state captured at method begin.</param>
    /// <returns>The original task result value.</returns>
    [PreserveContext]
    internal static async Task<TReturn> OnAsyncMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, CallTargetState state)
    {
        await Task.Yield();
        Console.WriteLine(AsyncEndWithValueMarker);
        return returnValue;
    }
}

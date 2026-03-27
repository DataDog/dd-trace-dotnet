// <copyright file="SampleCallTargetNativeAotReferenceIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.CallTargetNativeAot;

/// <summary>
/// Provides a dedicated integration for a referenced class library so the NativeAOT workflow proves it can rewrite
/// and publish selected reference assemblies, not only the application assembly.
/// </summary>
[InstrumentMethod(
    IntegrationName = nameof(SampleCallTargetNativeAotIntegration),
    MethodName = "ExecuteReference",
    ReturnTypeName = "System.Void",
    ParameterTypeNames = [],
    AssemblyName = "SampleCallTargetNativeAotLibrary",
    TypeName = "SampleCallTargetNativeAotLibrary.ReferencedTarget",
    MinimumVersion = "1.0.0",
    MaximumVersion = "9.0.0")]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class SampleCallTargetNativeAotReferenceIntegration
{
    /// <summary>
    /// Defines the marker emitted by the begin callback for the referenced-library sample method.
    /// </summary>
    internal const string BeginMarker = "CALLTARGET_AOT_REFERENCE_BEGIN:1";

    /// <summary>
    /// Defines the marker emitted by the end callback for the referenced-library sample method.
    /// </summary>
    internal const string EndMarker = "CALLTARGET_AOT_REFERENCE_END:1";

    /// <summary>
    /// Emits the begin marker for the referenced-library sample binding.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <returns>The default CallTarget state.</returns>
    internal static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
    {
        Console.WriteLine(BeginMarker);
        return CallTargetState.GetDefault();
    }

    /// <summary>
    /// Emits the end marker for the referenced-library sample binding.
    /// </summary>
    /// <typeparam name="TTarget">The target type being instrumented.</typeparam>
    /// <param name="instance">The instrumented target instance.</param>
    /// <param name="exception">The exception thrown by the target, if any.</param>
    /// <param name="state">The CallTarget state captured at method begin.</param>
    /// <returns>The default void CallTarget return container.</returns>
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
    {
        Console.WriteLine(EndMarker);
        return CallTargetReturn.GetDefault();
    }
}

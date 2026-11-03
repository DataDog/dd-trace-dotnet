// <copyright file="ContainerMetadataTryGetInodeIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.VersionConflict;

/// <summary>
/// System.Boolean Datadog.Trace.PlatformHelpers.ContainerMetadata::TryGetInode(System.String,System.Int64&amp;) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace",
    TypeName = "Datadog.Trace.PlatformHelpers.ContainerMetadata",
    MethodName = "TryGetInode",
    ReturnTypeName = ClrNames.Bool,
    ParameterTypeNames = [ClrNames.String, "System.Int64&"],
    MinimumVersion = "2.46.0", // The inode lookup started shelling out to stat in 2.46.0.
    MaximumVersion = "2.48.*", // From 2.49.0, ProcessHelpersStartWithDoNotTraceIntegration handles suppression.
    IntegrationName = nameof(IntegrationId.DatadogTraceVersionConflict))]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ContainerMetadataTryGetInodeIntegration
{
    private static readonly object SuppressionEnabled = new();

    internal static CallTargetState OnMethodBegin<TTarget>(ref string? path, ref long result)
    {
        // These 2.x versions call Process.Start while initializing their tracer, without StartWithDoNotTrace.
        // Tracing that call with the 3.x tracer asks the 2.x tracer for its sampling priority and recursively
        // enters its initialization. Suppress process instrumentation until the inode lookup completes.
        var state = new CallTargetState(scope: null, state: ProcessHelpers.ShouldTraceProcessStart() ? null : SuppressionEnabled);
        ProcessHelpers.ForceDoNotTrace(true);
        return state;
    }

    internal static CallTargetReturn<bool> OnMethodEnd<TTarget>(bool returnValue, Exception? exception, in CallTargetState state)
    {
        ProcessHelpers.ForceDoNotTrace(state.State is not null);
        return new CallTargetReturn<bool>(returnValue);
    }
}

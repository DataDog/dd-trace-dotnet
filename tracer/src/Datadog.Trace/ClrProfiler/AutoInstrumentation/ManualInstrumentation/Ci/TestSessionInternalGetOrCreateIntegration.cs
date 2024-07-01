// <copyright file="TestSessionInternalGetOrCreateIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Ci;

/// <summary>
/// Datadog.Trace.Ci.TestSession Datadog.Trace.Ci.TestSession::InternalGetOrCreate(System.String,System.String,System.String,System.Nullable`1[System.DateTimeOffset],System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Ci.TestSession",
    MethodName = "InternalGetOrCreate",
    ReturnTypeName = "Datadog.Trace.Ci.ITestSession",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, "System.Nullable`1[System.DateTimeOffset]", ClrNames.Bool],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestSessionInternalGetOrCreateIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string command, string workingDirectory, string framework, in DateTimeOffset? startDate, bool propagateEnvironmentVariables)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Session);
        var automaticSession = TestSession.InternalGetOrCreate(command, workingDirectory, framework, startDate, propagateEnvironmentVariables);
        return new(scope: null, state: automaticSession);
    }

    /// <summary>
    /// OnMethodEnd callback
    /// </summary>
    /// <typeparam name="TTarget">Type of the target</typeparam>
    /// <typeparam name="TReturn">Type of the return value (Datadog.Trace.Ci.TestSession)</typeparam>
    /// <param name="returnValue">Instance of Datadog.Trace.Ci.TestSession</param>
    /// <param name="exception">Exception instance in case the original code threw an exception.</param>
    /// <param name="state">Calltarget state value</param>
    /// <returns>A return value, in an async scenario will be T of Task of T</returns>
    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state)
    {
        // Duck cast TestSession as an ITestSession
        return new CallTargetReturn<TReturn>(state.State.DuckCast<TReturn>());
    }
}

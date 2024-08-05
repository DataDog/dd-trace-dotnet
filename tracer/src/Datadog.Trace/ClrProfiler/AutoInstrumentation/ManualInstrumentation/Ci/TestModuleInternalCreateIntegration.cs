// <copyright file="TestModuleInternalCreateIntegration.cs" company="Datadog">
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
/// Datadog.Trace.Ci.TestModule Datadog.Trace.Ci.TestModule::InternalCreate(System.String,System.String,System.String,System.DateTimeOffset) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.Manual",
    TypeName = "Datadog.Trace.Ci.TestModule",
    MethodName = "InternalCreate",
    ReturnTypeName = "Datadog.Trace.Ci.ITestModule",
    ParameterTypeNames = [ClrNames.String, ClrNames.String, ClrNames.String, "System.Nullable`1[System.DateTimeOffset]"],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class TestModuleInternalCreateIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(string name, string framework, string frameworkVersion, in DateTimeOffset? startDate)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityManualApiEvent(MetricTags.CIVisibilityTestingEventType.Module);
        var automaticModule = TestModule.InternalCreate(name, framework, frameworkVersion, startDate);
        return new(scope: null, state: automaticModule);
    }

    internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TReturn returnValue, Exception exception, in CallTargetState state)
    {
        // Duck cast TestModule as an ITestModule
        return new CallTargetReturn<TReturn>(state.State.DuckCast<TReturn>());
    }
}

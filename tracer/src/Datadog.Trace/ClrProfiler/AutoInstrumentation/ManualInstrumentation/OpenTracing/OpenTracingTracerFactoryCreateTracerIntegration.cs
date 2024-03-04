// <copyright file="OpenTracingTracerFactoryCreateTracerIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenTracing;

/// <summary>
/// OpenTracing.ITracer Datadog.Trace.OpenTracing.OpenTracingTracerFactory::CreateTracer(System.Uri,System.String,System.Boolean) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Datadog.Trace.OpenTracing",
    TypeName = "Datadog.Trace.OpenTracing.OpenTracingTracerFactory",
    MethodName = "CreateTracer",
    ReturnTypeName = "OpenTracing.ITracer",
    ParameterTypeNames = ["System.Uri", ClrNames.String, ClrNames.Bool],
    MinimumVersion = ManualInstrumentationConstants.MinVersion,
    MaximumVersion = ManualInstrumentationConstants.MaxVersion,
    IntegrationName = ManualInstrumentationConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class OpenTracingTracerFactoryCreateTracerIntegration
{
    internal static CallTargetState OnMethodBegin<TTarget>(Uri agentEndpoint, string defaultServiceName, bool isDebugEnabled)
    {
        TelemetryFactory.Metrics.Record(PublicApiUsage.OpenTracingTracerFactory_CreateTracer);
        return CallTargetState.GetDefault();
    }
}

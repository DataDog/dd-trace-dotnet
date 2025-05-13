// <copyright file="DatadogTraceManualInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using ManualTracer = DatadogTraceManual::Datadog.Trace.Tracer;

namespace Datadog.Trace.Tests.ManualInstrumentation;

public class DatadogTraceManualInstrumentationTests : InstrumentationTests<ManualTracer>
{
}

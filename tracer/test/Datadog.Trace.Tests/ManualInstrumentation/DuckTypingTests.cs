// <copyright file="DuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

extern alias DatadogTraceManual;

using System;
using Datadog.Trace.Agent;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.Proxies;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using BenchmarkDiscreteStats = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkDiscreteStats;
using BenchmarkHostInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkHostInfo;
using BenchmarkJobInfo = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkJobInfo;
using BenchmarkMeasureType = DatadogTraceManual::Datadog.Trace.Ci.BenchmarkMeasureType;
using ManualIScope = DatadogTraceManual::Datadog.Trace.IScope;
using ManualISpan = DatadogTraceManual::Datadog.Trace.ISpan;
using ManualISpanContext = DatadogTraceManual::Datadog.Trace.ISpanContext;
using ManualTestSession = DatadogTraceManual::Datadog.Trace.Ci.ManualTestSession;
using TestParameters = DatadogTraceManual::Datadog.Trace.Ci.TestParameters;
using TestStatus = DatadogTraceManual::Datadog.Trace.Ci.TestStatus;

namespace Datadog.Trace.Tests.ManualInstrumentation;

[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class DuckTypingTests
{
    private const string Skip = "We can't test these as-is because we rely on automatic instrumentation for ducktyping. To test these, import the DuckTyping folder into Datadog.Trace.Manual";
    private readonly AsyncLocalScopeManager _scopeManager = new();
    private readonly TracerSettings _settings = new() { StartupDiagnosticLogEnabled = false };
    private readonly Tracer _tracer;

    public DuckTypingTests()
    {
        _tracer = new Tracer(_settings, new Mock<IAgentWriter>().Object, new Mock<ITraceSampler>().Object, scopeManager: _scopeManager, statsd: null);
    }

    [Fact]
    public void CanDuckTypeScopeAsManualIScope()
    {
        var scope = _tracer.StartActiveInternal("manual");
        var span = scope.Span;
        var spanContext = span.Context;

        var manualScope = scope.DuckCast<ManualIScope>();

        // call all the properties to check for duck typing issues
        var manualSpan = manualScope.Span.Should().BeAssignableTo<ManualISpan>().Subject;
        manualSpan.SpanId.Should().Be(span.SpanId);
        manualSpan.SetException(new Exception("MyException"));
        manualSpan.Error.Should().Be(span.Error).And.BeTrue();
        manualSpan.Type.Should().Be(span.Type);
        manualSpan.OperationName.Should().Be(span.OperationName);
        manualSpan.ResourceName.Should().Be(span.ResourceName);
        manualSpan.ServiceName.Should().Be(span.ServiceName);
        manualSpan.TraceId.Should().Be(span.TraceId);
        manualSpan.SetTag("Test", "SomeValue");
        manualSpan.GetTag("Test").Should().Be("SomeValue");
        span.GetTag("Test").Should().Be("SomeValue"); // check it was mirrored

        var manualSpanContext = manualSpan.Context.Should().BeAssignableTo<ManualISpanContext>().Subject;
        manualSpanContext.SpanId.Should().Be(spanContext.SpanId);
        manualSpanContext.ServiceName.Should().Be(spanContext.ServiceName);
        manualSpanContext.TraceId.Should().Be(spanContext.TraceId);

        manualScope.Close();
        manualScope.Dispose();
    }

    [Fact(Skip = Skip)]
    public void CanDuckTypeManualTestSessionAsISession()
    {
        var session = TestSession.GetOrCreate("blah");
        var manualSession = new ManualTestSession();
        // This is normally done by the automatic instrumentation
        manualSession.SetAutomatic(session);
        manualSession.StartTime.Should().Be(session.StartTime);
        manualSession.Command.Should().Be(session.Command);
        manualSession.WorkingDirectory.Should().Be(manualSession.WorkingDirectory);

        // call the methods to make sure it works
        var module = manualSession.CreateModule("somemodule");
        module.Should().NotBeNull();

        var suite = module.GetOrCreateSuite("mysuite");
        suite.Should().NotBeNull();

        var test = suite.CreateTest("mytest");
        test.Should().NotBeNull();

        test.SetParameters(new TestParameters { Arguments = new(), Metadata = new() });
        test.SetBenchmarkMetadata(new BenchmarkHostInfo() { RuntimeVersion = "123" }, new BenchmarkJobInfo() { Description = "weeble" });
        var stats = new BenchmarkDiscreteStats(100, 100, 100, 100, 100, 0, 0, 0, 0, 100, 100, 100);
        test.AddBenchmarkData(BenchmarkMeasureType.ApplicationLaunch, info: "something", in stats);

        test.Close(TestStatus.Pass);
        suite.Close();
        module.Close();
        manualSession.Close(TestStatus.Pass);
    }
}
